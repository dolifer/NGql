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
        var fullPath = ResolveFullPath(fieldPath, nodePath);
        return fullPath != null ? Preserve(fullPath) : this;
    }

    /// <summary>
    /// Preserve fields from typed expression.
    /// </summary>
    public PreservationBuilder PreserveFromExpression<T>(Expression<Func<T, bool>> expression, string? nodePath = null)
        => PreserveFromExpressionCore(expression, nodePath, null, typeof(T));

    public PreservationBuilder PreserveFromExpression<T>(Expression<Func<T, bool>> expression, string? nodePath, Dictionary<string, string[]> localMap)
        => PreserveFromExpressionCore(expression, nodePath, localMap, typeof(T));

    public PreservationBuilder PreserveFromExpression(Expression expression, string? nodePath = null)
        => PreserveFromExpressionCore(expression, nodePath, null, InferTypeFromLambda(expression));

    public PreservationBuilder PreserveFromExpression(Expression expression, string? nodePath, Dictionary<string, string[]> localMap)
        => PreserveFromExpressionCore(expression, nodePath, localMap, InferTypeFromLambda(expression));

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
            ? lambda.Parameters.Select(p => p.Name).Where(n => n != null).ToArray()
            : null;

    private string? ResolveFullPath(string fieldPath, string nodePath)
    {
        var pathToNode = _sourceQuery.GetPathTo(_sourceQuery.Definition.Name, nodePath);
        if (pathToNode == null || pathToNode.Length == 0) return null;

        var lastSegment = nodePath.Split('.')[^1];
        return $"{string.Join(".", pathToNode)}.{lastSegment}.{fieldPath}";
    }

    private PreservationBuilder PreserveFromExpressionCore(Expression expression, string? nodePath, Dictionary<string, string[]>? localMap, Type? parameterType)
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
            return PreserveWithLocalMap(extractedPaths, parameterNames, nodePath, localMap, parameterType);
        }

        // Fallback: use GetPathTo
        return PreserveWithGetPathTo(extractedPaths, parameterNames?.FirstOrDefault(), nodePath, parameterType);
    }

    private PreservationBuilder PreserveWithLocalMap(
        HashSet<string> extractedPaths, 
        string[] parameterNames, 
        string nodePath, 
        Dictionary<string, string[]> localMap, 
        Type? parameterType)
    {
        foreach (var paramName in parameterNames)
        {
            if (!localMap.TryGetValue(paramName, out var basePath)) continue;

            // Get fields to preserve (strip parameter prefix)
            var fieldsToPreserve = GetFieldsToPreserve(extractedPaths, paramName, parameterType);
            
            // Navigate to node and preserve fields
            var nodeField = NavigateToNode(basePath, nodePath);
            if (nodeField?.Fields == null) continue;

            foreach (var field in fieldsToPreserve)
            {
                var matchingKey = FindFieldKey(nodeField.Fields, field);
                if (matchingKey != null)
                {
                    var fullPath = $"{string.Join(".", basePath.Concat(nodePath.Split('.')))}.{matchingKey}";
                    Preserve(fullPath);
                }
            }
        }

        return this;
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
            PreserveAtPath(field, nodePath);
        }

        return this;
    }

    private static HashSet<string> GetFieldsToPreserve(HashSet<string> extractedPaths, string? paramName, Type? parameterType)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var path in extractedPaths)
        {
            // Skip parameter names
            if (IsParameterName(path, paramName)) continue;

            // Strip parameter prefix (e.g., "playerProfile.RegData" => "RegData")
            var field = path.StartsWith(paramName + ".", StringComparison.OrdinalIgnoreCase)
                ? path.Substring(paramName.Length + 1)
                : path;

            result.Add(field);
        }

        // If no specific fields and we have a type, use all type properties (greedy)
        if (result.Count == 0 && parameterType != null)
        {
            var typeProperties = parameterType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Select(p => p.Name);
            foreach (var prop in typeProperties) result.Add(prop);
        }

        return result;
    }

    private static bool IsParameterName(string path, string? paramName)
        => !string.IsNullOrEmpty(paramName) && 
           string.Equals(path, paramName, StringComparison.OrdinalIgnoreCase);

    private FieldDefinition? NavigateToNode(string[] basePath, string nodePath)
    {
        var current = _sourceQuery.Definition.Fields;
        FieldDefinition? field = null;

        // Navigate through base path
        foreach (var segment in basePath)
        {
            // Try direct key match first, then alias/name match
            if (!current.TryGetValue(segment, out field))
            {
                var match = current.FirstOrDefault(kvp =>
                    string.Equals(kvp.Value.Alias, segment, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(kvp.Value.Name, segment, StringComparison.OrdinalIgnoreCase));
                
                if (match.Key == null) return null;
                field = match.Value;
            }
            current = field.Fields ?? new SortedDictionary<string, FieldDefinition>();
        }

        // Navigate through node path
        foreach (var segment in nodePath.Split('.'))
        {
            if (!current.TryGetValue(segment, out field))
            {
                var match = current.FirstOrDefault(kvp =>
                    string.Equals(kvp.Value.Alias, segment, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(kvp.Value.Name, segment, StringComparison.OrdinalIgnoreCase));
                
                if (match.Key == null) return null;
                field = match.Value;
            }
            current = field.Fields ?? new SortedDictionary<string, FieldDefinition>();
        }

        return field;
    }

    private static string? FindFieldKey(SortedDictionary<string, FieldDefinition> fields, string fieldName)
        => fields.FirstOrDefault(kvp =>
            string.Equals(kvp.Key, fieldName, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(kvp.Value.Alias, fieldName, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(kvp.Value.Name, fieldName, StringComparison.OrdinalIgnoreCase)
        ).Key;
}
