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
        // Group parameters by base path (minimal allocation)
        var paramsByBasePath = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var paramName in parameterNames)
        {
            if (!localMap.TryGetValue(paramName, out var basePath)) continue;

            var basePathKey = string.Join(".", basePath);
            if (!paramsByBasePath.TryGetValue(basePathKey, out var list))
            {
                list = new List<string>();
                paramsByBasePath[basePathKey] = list;
            }
            list.Add(paramName);
        }

        // Handle alwaysPreserveFields case: ensure all localMap base paths are included
        if (alwaysPreserveFields is { Length: > 0 })
        {
            foreach (var (_, basePath) in localMap)
            {
                var basePathKey = string.Join(".", basePath);
                if (!paramsByBasePath.ContainsKey(basePathKey))
                {
                    paramsByBasePath[basePathKey] = new List<string>();
                }
            }
        }

        if (paramsByBasePath.Count == 0) return;

        // Process each base path
        foreach (var (basePathKey, paramsForPath) in paramsByBasePath)
        {
            // Navigate: basePath + nodePath
            var fullPath = $"{basePathKey}.{nodePath}";
            var nodeField = QueryDefinitionExtensions.NavigatePath(sourceQuery.Definition._fields, fullPath.AsSpan(), out _);
            if (nodeField == null || !nodeField.HasFields) continue;

            var fieldsToPreserve = CollectFields(extractedPaths, paramsForPath, parameterTypes, alwaysPreserveFields);
            PreserveFields(nodeField._children, fieldsToPreserve, fullPath);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private HashSet<string> CollectFields(
        HashSet<string> extractedPaths,
        List<string> paramsForPath,
        Dictionary<string, Type> parameterTypes,
        string[]? alwaysPreserveFields)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Check if paths have parameter prefixes
        var hasParameterPrefixes = false;
        foreach (var p in extractedPaths)
        {
            if (paramsForPath.Any(param => p.StartsWith(param + ".", StringComparison.OrdinalIgnoreCase)))
            {
                hasParameterPrefixes = true;
            }

            if (hasParameterPrefixes) break;
        }

        // Use "no prefix" mode for multi-parameter comparison without prefixes
        var useNoPrefixMode = !hasParameterPrefixes && paramsForPath.Count > 1;

        if (useNoPrefixMode)
        {
            // Multi-parameter without prefixes: expand for each parameter type
            foreach (var paramName in paramsForPath)
            {
                parameterTypes.TryGetValue(paramName, out var specificParameterType);
                AddFieldsToPreserve(extractedPaths, null, specificParameterType, result);
            }
        }
        else
        {
            // Single parameter or has prefixes: filter per parameter
            foreach (var paramName in paramsForPath)
            {
                // Filter paths for this parameter
                HashSet<string>? parameterPaths = null;
                foreach (var p in extractedPaths.Where(p => string.Equals(p, paramName, StringComparison.OrdinalIgnoreCase) ||
                                                            p.StartsWith(paramName + ".", StringComparison.OrdinalIgnoreCase)))
                {
                    parameterPaths ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    parameterPaths.Add(p);
                }

                // Fallback: if no paths matched with prefix but we have paths and one parameter
                if ((parameterPaths == null || parameterPaths.Count == 0) &&
                    extractedPaths.Count > 0 &&
                    paramsForPath.Count == 1)
                {
                    parameterPaths = extractedPaths;
                }

                if (parameterPaths != null)
                {
                    parameterTypes.TryGetValue(paramName, out var specificParameterType);
                    AddFieldsToPreserve(parameterPaths, paramName, specificParameterType, result);
                }
            }
        }

        // Add always-preserve fields
        if (alwaysPreserveFields != null)
        {
            foreach (var field in alwaysPreserveFields)
            {
                result.Add(field);
            }
        }

        return result;
    }

    private static void AddFieldsToPreserve(
        HashSet<string> extractedPaths,
        string? paramName,
        Type? parameterType,
        HashSet<string> result)
    {
        var hasOnlyParameterName = false;

        foreach (var path in extractedPaths)
        {
            // Check if this is just the parameter name (e.g., "user" in "user != null")
            if (!string.IsNullOrEmpty(paramName) && string.Equals(path, paramName, StringComparison.OrdinalIgnoreCase))
            {
                hasOnlyParameterName = true;
                continue;
            }

            // Strip parameter prefix
            var field = !string.IsNullOrEmpty(paramName) && path.StartsWith(paramName + ".", StringComparison.OrdinalIgnoreCase)
                ? path.Substring(paramName.Length + 1)
                : path;

            if (!string.IsNullOrEmpty(field))
            {
                // Expand navigation properties
                var expanded = NavigationPropertyExpander.ExpandNavigationProperty(field, parameterType);
                foreach (var expandedField in expanded)
                {
                    result.Add(expandedField);
                }
            }
        }

        // Apply "most specific wins" if we have multiple paths
        if (result.Count > 1)
        {
            // Remove broader paths inline (avoids allocation)
            var toRemove = new List<string>();
            foreach (var path in result)
            {
                var prefix = path + ".";
                foreach (var other in result)
                {
                    if (other.Length > path.Length && other.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    {
                        toRemove.Add(path);
                        break;
                    }
                }
            }
            foreach (var path in toRemove)
            {
                result.Remove(path);
            }
        }

        // Greedy: if only parameter name was checked, preserve all type fields
        if (hasOnlyParameterName && result.Count == 0 && parameterType != null)
        {
            foreach (var prop in parameterType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                result.Add(prop.Name);
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void PreserveFields(
        IReadOnlyDictionary<string, FieldDefinition>? nodeFields,
        HashSet<string> fieldsToPreserve,
        string basePathStr)
    {
        if (nodeFields == null) return;

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

        // Object field: preserve all sub-fields
        var objectPath = $"{basePathStr}.{matchingKey}";
        foreach (var (key, _) in match.Value.Fields)
        {
            preserveCallback($"{objectPath}.{key}");
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

        // For merged queries with multiple roots, preserve in all of them
        foreach (var rootField in sourceQuery.Definition.Fields.Values)
        {
            var pathToNode = sourceQuery.GetPathTo(rootField.Alias ?? rootField.Name, nodePath);
            if (pathToNode.Length == 0) continue;

            // Build full node path once (avoid repeated string.Split)
            ReadOnlySpan<char> nodePathSpan = nodePath.AsSpan();
            var lastSegmentStart = nodePathSpan.LastIndexOf('.');
            var lastSegment = lastSegmentStart >= 0 ? nodePathSpan[(lastSegmentStart + 1)..] : nodePathSpan;
            var fullNodePath = $"{string.Join(".", pathToNode)}.{lastSegment.ToString()}";

            var nodeField = QueryDefinitionExtensions.NavigatePath(sourceQuery.Definition._fields, fullNodePath.AsSpan(), out _);
            if (nodeField is not { HasFields: true }) continue;

            // Preserve all fields for this root
            foreach (var field in fieldsToPreserve)
            {
                // Try path navigation first (handles both simple and nested paths)
                if (QueryDefinitionExtensions.NavigatePath(nodeField.Fields, field.AsSpan(), out var resolvedPath, fullNodePath) != null)
                {
                    preserveCallback(resolvedPath!);
                }
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string[]? GetParameterNames(Expression expression)
        => expression is LambdaExpression { Parameters.Count: > 0 } lambda
            ? lambda.Parameters.Where(n => n.Name != null).Select(p => p.Name!).ToArray()
            : null;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Dictionary<string, Type> GetParameterTypes(Expression expression)
    {
        if (expression is not LambdaExpression lambda || lambda.Parameters.Count == 0)
            return new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);

        return lambda.Parameters
            .Where(p => p.Name != null)
            .ToDictionary(p => p.Name!, p => p.Type, StringComparer.OrdinalIgnoreCase);
    }
}
