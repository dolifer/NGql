using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using NGql.Core.Abstractions;
using NGql.Core.Extensions;
using NGql.Core.Features;

namespace NGql.Core.Builders;

/// <summary>
/// Processes expressions to extract and preserve field paths from lambda expressions.
/// Optimized for hotpath usage with minimal allocations.
/// </summary>
internal sealed class ExpressionPreservationProcessor(QueryBuilder sourceQuery, Action<string> preserveCallback)
{
    /// <summary>
    /// Processes an expression and preserves the referenced fields.
    /// </summary>
    public void ProcessExpression(
        Expression expression,
        string? nodePath,
        Dictionary<string, string[]>? localMap,
        Type? parameterType,
        string[]? alwaysPreserveFields)
    {
        var extractedPaths = ExpressionFieldExtractor.ExtractFieldPaths(expression);

        // Fast path: no nodePath means preserve directly
        if (string.IsNullOrWhiteSpace(nodePath))
        {
            foreach (var path in extractedPaths)
            {
                preserveCallback(path);
            }
            return;
        }

        // Get parameter info once
        var parameterNames = GetParameterNames(expression);
        var parameterTypes = GetParameterTypes(expression);

        // Choose strategy based on localMap availability
        if (localMap != null && parameterNames != null)
        {
            PreserveWithLocalMap(extractedPaths, parameterNames, nodePath, localMap, parameterTypes, alwaysPreserveFields);
        }
        else
        {
            PreserveWithGetPathTo(extractedPaths, parameterNames?.FirstOrDefault(), nodePath, parameterType);
        }
    }

    private void PreserveWithLocalMap(
        HashSet<string> extractedPaths,
        string[] parameterNames,
        string nodePath,
        Dictionary<string, string[]> localMap,
        Dictionary<string, Type> parameterTypes,
        string[]? alwaysPreserveFields)
    {
        var basePathCache = new Dictionary<object, string>();
        var paramsByBasePath = GroupParametersByBasePath(parameterNames, localMap, basePathCache);
        EnsureAllBasePathsForAlwaysPreserve(localMap, alwaysPreserveFields, basePathCache, paramsByBasePath);

        if (paramsByBasePath.Count == 0) return;

        foreach (var (basePathKey, paramsForPath) in paramsByBasePath)
        {
            ProcessBasePath(extractedPaths, basePathKey, paramsForPath, nodePath, parameterTypes, alwaysPreserveFields);
        }
    }

    private static Dictionary<string, List<string>> GroupParametersByBasePath(
        string[] parameterNames,
        Dictionary<string, string[]> localMap,
        Dictionary<object, string> basePathCache)
    {
        var paramsByBasePath = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var paramName in parameterNames)
        {
            if (!localMap.TryGetValue(paramName, out var basePath)) continue;
            var basePathKey = GetOrAddBasePathKey(basePathCache, basePath);
            GetOrAddList(paramsByBasePath, basePathKey).Add(paramName);
        }
        return paramsByBasePath;
    }

    private static void EnsureAllBasePathsForAlwaysPreserve(
        Dictionary<string, string[]> localMap,
        string[]? alwaysPreserveFields,
        Dictionary<object, string> basePathCache,
        Dictionary<string, List<string>> paramsByBasePath)
    {
        if (alwaysPreserveFields is not { Length: > 0 }) return;

        foreach (var (_, basePath) in localMap)
        {
            var basePathKey = GetOrAddBasePathKey(basePathCache, basePath);
            if (!paramsByBasePath.ContainsKey(basePathKey))
            {
                paramsByBasePath[basePathKey] = new List<string>();
            }
        }
    }

    private static string GetOrAddBasePathKey(Dictionary<object, string> cache, string[] basePath)
    {
        if (!cache.TryGetValue(basePath, out var key))
        {
            key = string.Join(".", basePath);
            cache[basePath] = key;
        }
        return key;
    }

    private static List<string> GetOrAddList(Dictionary<string, List<string>> map, string key)
    {
        if (!map.TryGetValue(key, out var list))
        {
            list = new List<string>();
            map[key] = list;
        }
        return list;
    }

    private void ProcessBasePath(
        HashSet<string> extractedPaths,
        string basePathKey,
        List<string> paramsForPath,
        string nodePath,
        Dictionary<string, Type> parameterTypes,
        string[]? alwaysPreserveFields)
    {
        var fullPath = $"{basePathKey}.{nodePath}";
        var nodeField = QueryDefinitionExtensions.NavigatePath(sourceQuery.Definition._fields, fullPath.AsSpan(), out _);
        if (nodeField is not { HasFields: true }) return;

        var fieldsToPreserve = CollectFields(extractedPaths, paramsForPath, parameterTypes, alwaysPreserveFields);
        PreserveFields(nodeField._children!, fieldsToPreserve, fullPath);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private HashSet<string> CollectFields(
        HashSet<string> extractedPaths,
        List<string> paramsForPath,
        Dictionary<string, Type> parameterTypes,
        string[]? alwaysPreserveFields)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (ShouldExpandWithoutPrefixes(extractedPaths, paramsForPath))
        {
            ExpandPerParameterType(extractedPaths, paramsForPath, parameterTypes, result);
        }
        else
        {
            FilterPerParameter(extractedPaths, paramsForPath, parameterTypes, result);
        }

        AppendAlwaysPreserveFields(alwaysPreserveFields, result);
        return result;
    }

    /// <summary>
    /// True when the extracted paths carry no parameter-name prefix AND there are multiple
    /// parameters in scope — falls into the "expand per type" branch instead of per-parameter
    /// filtering.
    /// </summary>
    private static bool ShouldExpandWithoutPrefixes(HashSet<string> extractedPaths, List<string> paramsForPath)
    {
        if (paramsForPath.Count <= 1) return false;

        // Manual short-circuit loop — LINQ allocations would be heavier on the hot path.
#pragma warning disable S3267
        foreach (var p in extractedPaths)
        {
            foreach (var param in paramsForPath)
            {
                if (p.StartsWith(param + ".", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }
        }
#pragma warning restore S3267
        return true;
    }

    private static void ExpandPerParameterType(
        HashSet<string> extractedPaths,
        List<string> paramsForPath,
        Dictionary<string, Type> parameterTypes,
        HashSet<string> result)
    {
        foreach (var paramName in paramsForPath)
        {
            parameterTypes.TryGetValue(paramName, out var specificParameterType);
            AddFieldsToPreserve(extractedPaths, null, specificParameterType, result);
        }
    }

    private static void FilterPerParameter(
        HashSet<string> extractedPaths,
        List<string> paramsForPath,
        Dictionary<string, Type> parameterTypes,
        HashSet<string> result)
    {
        foreach (var paramName in paramsForPath)
        {
            var parameterPaths = SelectPathsForParameter(extractedPaths, paramName, paramsForPath.Count == 1);
            if (parameterPaths == null) continue;

            parameterTypes.TryGetValue(paramName, out var specificParameterType);
            AddFieldsToPreserve(parameterPaths, paramName, specificParameterType, result);
        }
    }

    /// <summary>
    /// Returns the subset of <paramref name="extractedPaths"/> that target <paramref name="paramName"/>.
    /// Falls back to the full set when no prefixed paths match AND this is a single-parameter lambda
    /// (so an unprefixed expression like <c>x =&gt; x.field</c> still preserves something).
    /// </summary>
    private static HashSet<string>? SelectPathsForParameter(
        HashSet<string> extractedPaths,
        string paramName,
        bool singleParameterFallback)
    {
        var parameterPaths = CollectParameterPrefixedPaths(extractedPaths, paramName);
        if ((parameterPaths is null || parameterPaths.Count == 0)
            && extractedPaths.Count > 0
            && singleParameterFallback)
        {
            return extractedPaths;
        }
        return parameterPaths;
    }

    // Manual loop with lazy init beats LINQ Where().ToHashSet() when no paths match.
#pragma warning disable S3267
    private static HashSet<string>? CollectParameterPrefixedPaths(HashSet<string> extractedPaths, string paramName)
    {
        HashSet<string>? parameterPaths = null;
        var prefix = paramName + ".";
        foreach (var p in extractedPaths)
        {
            if (!IsParameterMatch(p, paramName, prefix)) continue;
            parameterPaths ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            parameterPaths.Add(p);
        }
        return parameterPaths;
    }

    private static bool IsParameterMatch(string path, string paramName, string prefix)
    {
        if (string.Equals(path, paramName, StringComparison.OrdinalIgnoreCase)) return true;
        return path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }
#pragma warning restore S3267

    private static void AppendAlwaysPreserveFields(string[]? alwaysPreserveFields, HashSet<string> result)
    {
        if (alwaysPreserveFields == null) return;
        foreach (var field in alwaysPreserveFields)
        {
            result.Add(field);
        }
    }

    private static void AddFieldsToPreserve(
        HashSet<string> extractedPaths,
        string? paramName,
        Type? parameterType,
        HashSet<string> result)
    {
        var hasOnlyParameterName = ExpandPathsIntoResult(extractedPaths, paramName, parameterType, result);

        if (result.Count > 1)
        {
            ApplyMostSpecificWins(result);
        }

        if (hasOnlyParameterName && result.Count == 0 && parameterType != null)
        {
            AddAllPropertiesAsFallback(parameterType, result);
        }
    }

    /// <summary>
    /// Walks <paramref name="extractedPaths"/> and adds the (parameter-prefix-stripped, navigation-expanded)
    /// fields to <paramref name="result"/>. Returns true when at least one path was just the parameter
    /// name itself, signalling the "greedy preserve everything" fallback below.
    /// </summary>
    private static bool ExpandPathsIntoResult(
        HashSet<string> extractedPaths,
        string? paramName,
        Type? parameterType,
        HashSet<string> result)
    {
        var hasOnlyParameterName = false;
        foreach (var path in extractedPaths)
        {
            if (IsBareParameterReference(path, paramName))
            {
                hasOnlyParameterName = true;
                continue;
            }

            var field = StripParameterPrefix(path, paramName);
            if (string.IsNullOrEmpty(field)) continue;

            foreach (var expandedField in NavigationPropertyExpander.ExpandNavigationProperty(field, parameterType))
            {
                result.Add(expandedField);
            }
        }
        return hasOnlyParameterName;
    }

    private static bool IsBareParameterReference(string path, string? paramName)
        => !string.IsNullOrEmpty(paramName)
        && string.Equals(path, paramName, StringComparison.OrdinalIgnoreCase);

    private static string StripParameterPrefix(string path, string? paramName)
        => !string.IsNullOrEmpty(paramName)
           && path.StartsWith(paramName + ".", StringComparison.OrdinalIgnoreCase)
            ? path.Substring(paramName.Length + 1)
            : path;

    /// <summary>Removes broader paths from <paramref name="result"/> when a more-specific child path exists.</summary>
    private static void ApplyMostSpecificWins(HashSet<string> result)
    {
        result.RemoveWhere(path => HasMoreSpecificDescendant(path, result));
    }

    private static bool HasMoreSpecificDescendant(string path, HashSet<string> all)
    {
        var prefix = path + ".";
        foreach (var other in all)
        {
            if (other.Length > path.Length && other.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private static void AddAllPropertiesAsFallback(Type parameterType, HashSet<string> result)
    {
        foreach (var prop in parameterType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            result.Add(prop.Name);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void PreserveFields(
        IReadOnlyDictionary<string, FieldDefinition> nodeFields,
        HashSet<string> fieldsToPreserve,
        string basePathStr)
    {
        foreach (var field in fieldsToPreserve)
        {
            // Fast path: nested field
            if (field.Contains('.'))
            {
                PreserveNestedField(nodeFields, field, basePathStr);
                continue;
            }

            // Fast path: direct match
            var match = PreserveExtensions.FindFieldByNameOrAlias(nodeFields, field.AsSpan());
            if (match.HasValue)
            {
                PreserveMatchedField(match.Value, basePathStr);
                continue;
            }

            // Slow path: recursive search
            PreserveRecursiveMatches(nodeFields, field, basePathStr);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void PreserveNestedField(IReadOnlyDictionary<string, FieldDefinition> nodeFields, string field, string basePathStr)
    {
        if (QueryDefinitionExtensions.NavigatePath(nodeFields, field.AsSpan(), out var resolvedPath, basePathStr) != null)
        {
            preserveCallback(resolvedPath!);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void PreserveMatchedField(KeyValuePair<string, FieldDefinition> match, string basePathStr)
    {
        var matchingKey = match.Key;

        // Fast path: leaf field
        if (!match.Value.HasFields)
        {
            preserveCallback($"{basePathStr}.{matchingKey}");
            return;
        }

        // Object field: preserve all sub-fields using fast span iteration
        var objectPath = $"{basePathStr}.{matchingKey}";
        foreach (var child in match.Value._children!.AsSpan())
        {
            preserveCallback($"{objectPath}.{child.Name}");
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void PreserveRecursiveMatches(IReadOnlyDictionary<string, FieldDefinition> nodeFields, string field, string basePathStr)
    {
        var recursiveMatches = QueryDefinitionExtensions.FindFieldRecursively(nodeFields, field, "");
        foreach (var recursivePath in recursiveMatches)
        {
            preserveCallback($"{basePathStr}.{recursivePath}");
        }
    }

    private void PreserveWithGetPathTo(
        HashSet<string> extractedPaths,
        string? paramName,
        string nodePath,
        Type? parameterType)
    {
        var fieldsToPreserve = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        AddFieldsToPreserve(extractedPaths, paramName, parameterType, fieldsToPreserve);

        var lastSegment = LastSegmentOf(nodePath);
        foreach (var rootField in sourceQuery.Definition.Fields.Values)
        {
            PreserveFromRoot(rootField, nodePath, lastSegment, fieldsToPreserve);
        }
    }

    private static string LastSegmentOf(string nodePath)
    {
        var nodePathSpan = nodePath.AsSpan();
        var lastSegmentStart = nodePathSpan.LastIndexOf('.');
        return (lastSegmentStart >= 0 ? nodePathSpan[(lastSegmentStart + 1)..] : nodePathSpan).ToString();
    }

    private void PreserveFromRoot(FieldDefinition rootField, string nodePath, string lastSegment, HashSet<string> fieldsToPreserve)
    {
        var pathToNode = sourceQuery.GetPathTo(rootField.Alias ?? rootField.Name, nodePath);
        if (pathToNode.Length == 0) return;

        var fullNodePath = $"{string.Join(".", pathToNode)}.{lastSegment}";
        var nodeField = QueryDefinitionExtensions.NavigatePath(sourceQuery.Definition._fields, fullNodePath.AsSpan(), out _);
        if (nodeField is null) return;
        if (!nodeField.HasFields) return;

        foreach (var field in fieldsToPreserve)
        {
            if (QueryDefinitionExtensions.NavigatePath(nodeField.Fields, field.AsSpan(), out var resolvedPath, fullNodePath) is not null)
            {
                preserveCallback(resolvedPath!);
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string[]? GetParameterNames(Expression expression)
    {
        if (expression is not LambdaExpression { Parameters.Count: > 0 } lambda)
            return null;

        // Lambdas authored in C# always carry parameter names. The BCL allows null names via
        // Expression.Parameter(type, null) but those don't reach here through the public API.
#pragma warning disable S3267
        var names = new string[lambda.Parameters.Count];
        for (int i = 0; i < lambda.Parameters.Count; i++)
        {
            names[i] = lambda.Parameters[i].Name!;
        }
#pragma warning restore S3267
        return names;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Dictionary<string, Type> GetParameterTypes(Expression expression)
    {
        if (expression is not LambdaExpression lambda || lambda.Parameters.Count == 0)
            return new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);

        var dict = new Dictionary<string, Type>(lambda.Parameters.Count, StringComparer.OrdinalIgnoreCase);
        foreach (var p in lambda.Parameters)
        {
            if (p.Name != null)
                dict[p.Name] = p.Type;
        }

        return dict;
    }
}
