using System.Linq.Expressions;
using System.Reflection;
using NGql.Core.Abstractions;
using NGql.Core.Features;

namespace NGql.Core.Builders;

/// <summary>
/// Builder for preserving specific fields from a QueryBuilder using functional composition.
/// </summary>
public sealed class PreservationBuilder
{
    private readonly QueryBuilder _sourceQuery;
    private readonly HashSet<string> _pathsToPreserve;

    private PreservationBuilder(QueryBuilder sourceQuery)
    {
        _sourceQuery = sourceQuery ?? throw new ArgumentNullException(nameof(sourceQuery));
        _pathsToPreserve = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    }

    public static PreservationBuilder Create(QueryBuilder query) => new(query);

    /// <summary>
    /// Core method: adds paths to preserve with automatic cleanup of parent/child relationships.
    /// </summary>
    public PreservationBuilder Preserve(params string[] fieldPaths)
    {
        if (fieldPaths == null || fieldPaths.Length == 0) return this;

        foreach (var path in fieldPaths.Where(p => !string.IsNullOrWhiteSpace(p)))
        {
            // Remove parent paths when adding more specific child
            _pathsToPreserve.RemoveWhere(existing => 
                path.StartsWith(existing + ".", StringComparison.OrdinalIgnoreCase));
            
            _pathsToPreserve.Add(path);
        }

        return this;
    }

    /// <summary>
    /// Preserve fields at a specific node path.
    /// </summary>
    public PreservationBuilder PreserveAtPath(string fieldPath, string nodePath)
    {
        // For merged queries with multiple roots, preserve in all of them
        foreach (var rootField in _sourceQuery.Definition.Fields.Values)
        {
            var pathToNode = _sourceQuery.GetPathTo(rootField.Alias ?? rootField.Name, nodePath);
            if (pathToNode.Length == 0) continue;

            var fullNodePath = string.Join(".", pathToNode) + "." + nodePath.Split('.')[^1];
            var (nodeField, _) = FindNodeByPath(_sourceQuery.Definition.Fields, fullNodePath);
            if (nodeField?.Fields != null)
            {
                var match = PreserveExtensions.FindFieldByNameOrAlias(nodeField.Fields, fieldPath.AsSpan());
                if (match.HasValue)
                {
                    Preserve($"{fullNodePath}.{match.Value.Key}");
                }
            }
        }
        return this;
    }

    private static (FieldDefinition?, string) FindNodeByPath(SortedDictionary<string, FieldDefinition> fields, string nodePath)
    {
        var segments = nodePath.Split('.');
        var current = fields;
        FieldDefinition? lastField = null;
        var pathSegments = new List<string>();

        foreach (var segment in segments)
        {
            var match = PreserveExtensions.FindFieldByNameOrAlias(current, segment.AsSpan());
            if (!match.HasValue || match.Value.Value.Fields == null) return (null, string.Empty);
            lastField = match.Value.Value;
            pathSegments.Add(match.Value.Key);
            current = lastField.Fields;
        }

        return (lastField, string.Join(".", pathSegments));
    }

    /// <summary>
    /// Preserve fields from typed expression.
    /// </summary>
    public PreservationBuilder PreserveFromExpression<T>(Expression<Func<T, bool>> expression, string? nodePath = null)
        => PreserveFromExpressionCore(expression, nodePath, null, typeof(T), null);

    public PreservationBuilder PreserveFromExpression<T>(Expression<Func<T, bool>> expression, string nodePath, Dictionary<string, string[]> localMap)
        => PreserveFromExpressionCore(expression, nodePath, localMap, typeof(T), null);

    public PreservationBuilder PreserveFromExpression(Expression expression, string? nodePath = null)
        => PreserveFromExpressionCore(expression, nodePath, null, InferTypeFromLambda(expression), null);

    public PreservationBuilder PreserveFromExpression(Expression expression, string nodePath, Dictionary<string, string[]> localMap)
        => PreserveFromExpressionCore(expression, nodePath, localMap, InferTypeFromLambda(expression), null);

    public PreservationBuilder PreserveFromExpression(Expression expression, string nodePath, Dictionary<string, string[]> localMap, params string[] alwaysPreserveFields)
        => PreserveFromExpressionCore(expression, nodePath, localMap, InferTypeFromLambda(expression), alwaysPreserveFields);

    public QueryBuilder Build()
        => _pathsToPreserve.Count == 0 
            ? _sourceQuery 
            : _sourceQuery.Preserve(_pathsToPreserve.ToArray());

    private static Type? InferTypeFromLambda(Expression expression)
        => expression is LambdaExpression lambda && lambda.Parameters.Count > 0 
            ? lambda.Parameters[0].Type 
            : null;

    private static string[]? GetParameterNames(Expression expression)
        => expression is LambdaExpression lambda && lambda.Parameters.Count > 0
            ? lambda.Parameters.Where(n => n.Name != null).Select(p => p.Name!).ToArray()
            : null;

    private string? ResolveFullPath(string fieldPath, string nodePath)
    {
        var pathToNode = _sourceQuery.GetPathTo(_sourceQuery.Definition.Name, nodePath);
        if (pathToNode == null || pathToNode.Length == 0)
        {
            return null;
        }

        var lastSegment = nodePath.Split('.')[^1];
        return $"{string.Join(".", pathToNode)}.{lastSegment}.{fieldPath}";
    }

    private PreservationBuilder PreserveFromExpressionCore(Expression expression, string? nodePath, Dictionary<string, string[]>? localMap, Type? parameterType, string[]? alwaysPreserveFields)
    {
        var extractedPaths = ExpressionFieldExtractor.ExtractFieldPaths(expression);
        var parameterNames = GetParameterNames(expression);
        
        if (string.IsNullOrWhiteSpace(nodePath))
        {
            // No nodePath: preserve extracted paths directly
            return Preserve(extractedPaths.ToArray());
        }

        if (localMap != null && parameterNames != null)
        {
            // Use localMap to resolve starting point
            return PreserveWithLocalMap(extractedPaths, parameterNames, nodePath, localMap, parameterType, alwaysPreserveFields);
        }

        // Fallback: use GetPathTo
        return PreserveWithGetPathTo(extractedPaths, parameterNames?.FirstOrDefault(), nodePath, parameterType);
    }

    private PreservationBuilder PreserveWithLocalMap(
        HashSet<string> extractedPaths,
        string[] parameterNames,
        string nodePath,
        Dictionary<string, string[]> localMap,
        Type? parameterType,
        string[]? alwaysPreserveFields)
    {
        // Group parameters by their base path to handle multiple parameters mapping to same path
        var paramsByBasePath = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        
        foreach (var paramName in parameterNames)
        {
            if (!localMap.TryGetValue(paramName, out var basePath)) continue;
            
            var basePathKey = string.Join(".", basePath);
            if (!paramsByBasePath.ContainsKey(basePathKey))
            {
                paramsByBasePath[basePathKey] = new List<string>();
            }
            paramsByBasePath[basePathKey].Add(paramName);
        }

        // If alwaysPreserveFields is provided, ensure ALL localMap entries are processed
        if (alwaysPreserveFields != null && alwaysPreserveFields.Length > 0)
        {
            foreach (var (paramName, basePath) in localMap)
            {
                var basePathKey = string.Join(".", basePath);
                if (!paramsByBasePath.ContainsKey(basePathKey))
                {
                    paramsByBasePath[basePathKey] = new List<string>();
                }
            }
        }

        // Process each unique base path
        foreach (var (basePathKey, paramsForPath) in paramsByBasePath)
        {
            var basePath = basePathKey.Split('.');
            
            // Collect ALL fields from ALL parameters that map to this base path
            var allFieldsForPath = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var paramName in paramsForPath)
            {
                var fieldsToPreserve = GetFieldsToPreserve(extractedPaths, paramName, parameterType);
                foreach (var field in fieldsToPreserve)
                {
                    allFieldsForPath.Add(field);
                }
            }
            
            // Add always-preserve fields
            if (alwaysPreserveFields != null)
            {
                foreach (var field in alwaysPreserveFields)
                {
                    allFieldsForPath.Add(field);
                }
            }
            
            // Navigate to node
            var nodeField = NavigateToNode(basePath, nodePath);
            if (nodeField?.Fields == null) continue;

            var basePathStr = string.Join(".", basePath.Concat(nodePath.Split('.')));

            foreach (var field in allFieldsForPath)
            {
                // Handle nested paths (e.g., "RegData.Region")
                if (field.Contains('.'))
                {
                    var resolvedPath = ResolveNestedPath(nodeField.Fields, field, basePathStr);
                    if (resolvedPath != null) Preserve(resolvedPath);
                }
                else
                {
                    // Simple field - find matching key
                    var match = PreserveExtensions.FindFieldByNameOrAlias(nodeField.Fields, field.AsSpan());
                    if (match.HasValue)
                    {
                        var matchingKey = match.Value.Key;
                        // Check if this field has sub-fields (is an object)
                        if (match.Value.Value.Fields != null && match.Value.Value.Fields.Count > 0)
                        {
                            // Object field: preserve all its sub-fields (greedy for this object)
                            PreserveObjectFields(match.Value.Value.Fields, $"{basePathStr}.{matchingKey}");
                        }
                        else
                        {
                            // Leaf field: preserve just this field
                            var fullPath = $"{basePathStr}.{matchingKey}";
                            Preserve(fullPath);
                        }
                    }
                }
            }
        }

        return this;
    }

    private void PreserveObjectFields(SortedDictionary<string, FieldDefinition> fields, string basePath)
    {
        foreach (var (key, fieldDef) in fields)
        {
            var fullPath = $"{basePath}.{key}";
            Preserve(fullPath);
        }
    }

    private static string? ResolveNestedPath(SortedDictionary<string, FieldDefinition> fields, string nestedPath, string basePath)
    {
        var segments = nestedPath.Split('.');
        var currentFields = fields;
        var resolvedSegments = new List<string>();

        foreach (var segment in segments)
        {
            var match = PreserveExtensions.FindFieldByNameOrAlias(currentFields, segment.AsSpan());
            if (!match.HasValue) return null;

            resolvedSegments.Add(match.Value.Key);

            // Navigate to next level
            if (match.Value.Value.Fields == null) return null;
            currentFields = match.Value.Value.Fields;
        }

        return $"{basePath}.{string.Join(".", resolvedSegments)}";
    }

    private PreservationBuilder PreserveWithGetPathTo(
        HashSet<string> extractedPaths,
        string? paramName,
        string nodePath,
        Type? parameterType)
    {
        var fieldsToPreserve = GetFieldsToPreserve(extractedPaths, paramName, parameterType);
        
        foreach (var field in fieldsToPreserve)
        {
            // For nested paths, resolve each segment
            if (field.Contains('.'))
            {
                var segments = field.Split('.');
                var firstSegment = segments[0];
                var remainingPath = string.Join(".", segments.Skip(1));
                
                // Preserve the first segment at nodePath, then the rest
                var fullPath = ResolveFullPath(firstSegment, nodePath);
                if (fullPath != null)
                {
                    Preserve($"{fullPath}.{remainingPath}");
                }
            }
            else
            {
                PreserveAtPath(field, nodePath);
            }
        }

        return this;
    }

    private static HashSet<string> GetFieldsToPreserve(HashSet<string> extractedPaths, string? paramName, Type? parameterType)
    {
        HashSet<string>? result = null;
        bool hasOnlyParameterName = false;

        foreach (var path in extractedPaths)
        {
            // Check if this is just the parameter name (e.g., "user" in "user != null")
            if (IsParameterName(path, paramName))
            {
                hasOnlyParameterName = true;
                continue;
            }

            // Strip parameter prefix (e.g., "playerProfile.RegData.Region" => "RegData.Region")
            var field = !string.IsNullOrEmpty(paramName) && path.StartsWith(paramName + ".", StringComparison.OrdinalIgnoreCase)
                ? path.Substring(paramName.Length + 1)
                : path;

            if (!string.IsNullOrEmpty(field))
            {
                result ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                result.Add(field);
            }
        }

        // Apply "most specific wins" rule: remove broader paths if more specific ones exist
        if (result != null && result.Count > 1)
        {
            result = FilterToMostSpecific(result);
        }

        // Greedy: if only parameter name was checked (e.g., "user != null"), preserve all type fields
        if (hasOnlyParameterName && (result == null || result.Count == 0) && parameterType != null)
        {
            result ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var prop in parameterType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                result.Add(prop.Name);
            }
        }

        return result ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    }

    private static HashSet<string> FilterToMostSpecific(HashSet<string> paths)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var pathList = paths.ToList();

        foreach (var path in pathList)
        {
            // Check if any other path is more specific (starts with this path + ".")
            bool hasMoreSpecific = pathList.Any(other => 
                other.Length > path.Length && 
                other.StartsWith(path + ".", StringComparison.OrdinalIgnoreCase));

            // Only add if no more specific path exists
            if (!hasMoreSpecific)
            {
                result.Add(path);
            }
        }

        return result;
    }

    private static bool IsParameterName(string path, string? paramName)
        => !string.IsNullOrEmpty(paramName) && 
           string.Equals(path, paramName, StringComparison.OrdinalIgnoreCase);

    private FieldDefinition? NavigateToNode(string[] basePath, string nodePath)
    {
        var current = _sourceQuery.Definition.Fields;
        
        // Navigate through base path - first segment might be an alias
        if (basePath.Length > 0)
        {
            var firstSegment = basePath[0];
            // Find field by alias first (for merged queries)
            var field = current.Values.FirstOrDefault(f => 
                string.Equals(f.Alias, firstSegment, StringComparison.OrdinalIgnoreCase));
            
            // Fallback to name match
            if (field == null && !TryNavigateToField(current, firstSegment, out field))
            {
                return null;
            }
            
            if (field?.Fields == null) return null;
            current = field.Fields;
            
            // Navigate remaining base path segments
            for (int i = 1; i < basePath.Length; i++)
            {
                if (!TryNavigateToField(current, basePath[i], out field) || field?.Fields == null) return null;
                current = field.Fields;
            }
        }

        // Navigate through node path
        FieldDefinition? lastField = null;
        foreach (var segment in nodePath.Split('.'))
        {
            if (!TryNavigateToField(current, segment, out lastField) || lastField?.Fields == null) return null;
            current = lastField.Fields;
        }

        return lastField;
    }

    private static bool TryNavigateToField(SortedDictionary<string, FieldDefinition> fields, string segment, out FieldDefinition? field)
    {
        if (fields.TryGetValue(segment, out field))
        {
            return true;
        }

        var match = PreserveExtensions.FindFieldByNameOrAlias(fields, segment.AsSpan());
        if (!match.HasValue)
        {
            field = null;
            return false;
        }

        field = match.Value.Value;
        return true;
    }
}