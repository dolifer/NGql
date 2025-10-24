using System.Linq.Expressions;
using System.Reflection;
using NGql.Core.Features;

namespace NGql.Core.Builders;

/// <summary>
/// Builder for preserving specific fields from a QueryBuilder.
/// Provides a fluent API for accumulating multiple field paths before applying the preservation.
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

    /// <summary>
    /// Creates a new preservation builder for the specified query.
    /// </summary>
    /// <param name="query">The source query to preserve fields from.</param>
    /// <returns>A new PreservationBuilder instance.</returns>
    /// <example>
    /// <code>
    /// var result = PreservationBuilder
    ///     .Create(query)
    ///     .Preserve("user.profile")
    ///     .Preserve("user.posts")
    ///     .Build();
    /// </code>
    /// </example>
    public static PreservationBuilder Create(QueryBuilder query)
        => new(query);

    /// <summary>
    /// Adds one or more field paths to preserve.
    /// Can be called multiple times to accumulate paths.
    /// </summary>
    /// <param name="fieldPaths">The field paths to preserve.</param>
    /// <returns>The current PreservationBuilder instance for method chaining.</returns>
    public PreservationBuilder Preserve(params string[] fieldPaths)
    {
        if (fieldPaths == null || fieldPaths.Length == 0)
            return this;

        foreach (var path in fieldPaths)
        {
            if (!string.IsNullOrWhiteSpace(path))
            {
                _pathsToPreserve.Add(path);
            }
        }

        return this;
    }

    /// <summary>
    /// Preserves fields referenced in a typed predicate expression.
    /// Automatically extracts field paths from the expression.
    /// </summary>
    /// <typeparam name="T">The type being queried</typeparam>
    /// <param name="predicate">The predicate expression (e.g., x => x.user.profile.age > 10)</param>
    /// <param name="nodePath">Optional node path for resolving short notation paths (e.g., "edges.node")</param>
    /// <returns>The current PreservationBuilder instance for method chaining.</returns>
    public PreservationBuilder PreserveWhere<T>(Expression<Func<T, bool>> predicate, string? nodePath = null)
    {
        var paths = ExpressionFieldExtractor.ExtractFieldPaths(predicate);
        var parameterName = GetParameterName(predicate);
        return PreserveExpandedPaths(paths, nodePath, parameterName, typeof(T));
    }

    /// <summary>
    /// Preserves fields referenced in a typed predicate expression with local parameter mapping.
    /// </summary>
    public PreservationBuilder PreserveWhere<T>(Expression<Func<T, bool>> predicate, string? nodePath, Dictionary<string, string[]> localMap)
    {
        var paths = ExpressionFieldExtractor.ExtractFieldPaths(predicate);
        var parameterName = GetParameterName(predicate);
        return PreserveExpandedPaths(paths, nodePath, parameterName, typeof(T), localMap);
    }

    /// <summary>
    /// Preserves fields referenced in a typed selector expression.
    /// </summary>
    public PreservationBuilder PreserveFor<T>(Expression<Func<T, object>> selector, string? nodePath = null)
    {
        var paths = ExpressionFieldExtractor.ExtractFieldPaths(selector);
        var parameterName = GetParameterName(selector);
        return PreserveExpandedPaths(paths, nodePath, parameterName, typeof(T));
    }

    /// <summary>
    /// Preserves fields referenced in a typed selector expression with local parameter mapping.
    /// </summary>
    public PreservationBuilder PreserveFor<T>(Expression<Func<T, object>> selector, string? nodePath, Dictionary<string, string[]> localMap)
    {
        var paths = ExpressionFieldExtractor.ExtractFieldPaths(selector);
        var parameterName = GetParameterName(selector);
        return PreserveExpandedPaths(paths, nodePath, parameterName, typeof(T), localMap);
    }

    /// <summary>
    /// Preserves fields referenced in a typed expression.
    /// </summary>
    public PreservationBuilder PreserveFromExpression<T>(Expression<Func<T, bool>> expression, string? nodePath = null)
    {
        var paths = ExpressionFieldExtractor.ExtractFieldPaths(expression);
        var parameterName = GetParameterName(expression);
        return PreserveExpandedPaths(paths, nodePath, parameterName, typeof(T));
    }

    /// <summary>
    /// Preserves fields referenced in a typed expression with local parameter mapping.
    /// </summary>
    public PreservationBuilder PreserveFromExpression<T>(Expression<Func<T, bool>> expression, string? nodePath, Dictionary<string, string[]> localMap)
    {
        var paths = ExpressionFieldExtractor.ExtractFieldPaths(expression);
        var parameterName = GetParameterName(expression);
        return PreserveExpandedPaths(paths, nodePath, parameterName, typeof(T), localMap);
    }

    /// <summary>
    /// Preserves fields referenced in any expression.
    /// </summary>
    public PreservationBuilder PreserveFromExpression(Expression expression, string? nodePath = null)
    {
        var paths = ExpressionFieldExtractor.ExtractFieldPaths(expression);
        return PreserveExpandedPaths(paths, nodePath, null, null);
    }

    /// <summary>
    /// Preserves fields referenced in any expression with local parameter mapping.
    /// </summary>
    public PreservationBuilder PreserveFromExpression(Expression expression, string? nodePath, Dictionary<string, string[]> localMap)
    {
        var paths = ExpressionFieldExtractor.ExtractFieldPaths(expression);
        return PreserveExpandedPaths(paths, nodePath, null, null, localMap);
    }

    /// <summary>
    /// Expands extracted field paths using GetPathTo when a nodePath is provided.
    /// </summary>
    private PreservationBuilder PreserveExpandedPaths(HashSet<string> extractedPaths, string? nodePath, string? parameterName = null, Type? parameterType = null, Dictionary<string, string[]>? localMap = null)
    {
        if (string.IsNullOrWhiteSpace(nodePath))
        {
            // Check localMap first for parameter mapping
            if (localMap != null && parameterName != null && localMap.TryGetValue(parameterName, out var mappedPaths))
            {
                return Preserve(mappedPaths);
            }

            // Filter out parameter names if we have specific field paths
            var specificPaths = extractedPaths.Where(path => !IsParameterName(path, parameterName, parameterType)).ToArray();
            return specificPaths.Length > 0 ? Preserve(specificPaths) : Preserve(extractedPaths.ToArray());
        }

        // Filter out parameter names from extracted paths before processing
        var filteredPaths = extractedPaths.Where(path => !IsParameterName(path, parameterName, parameterType)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        
        // Check localMap first for parameter mapping
        if (localMap != null && parameterName != null && localMap.TryGetValue(parameterName, out var localMappedPaths) && localMappedPaths.Length > 0)
        {
            var basePath = string.Join(".", localMappedPaths);
            var expandedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            
            foreach (var path in filteredPaths)
            {
                var fullPath = $"{basePath}.{nodePath}.{path}";
                expandedPaths.Add(fullPath);
            }
            return Preserve(expandedPaths.ToArray());
        }

        // If no specific paths remain after filtering, return original query (greedy preservation)
        if (filteredPaths.Count == 0)
        {
            return this;
        }

        var expandedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var queryName = _sourceQuery.Definition.Name;

        foreach (var path in filteredPaths)
        {
            // Resolve the path to the node using GetPathTo
            var resolvedNodePath = _sourceQuery.GetPathTo(queryName, nodePath);

            if (resolvedNodePath != null && resolvedNodePath.Length > 0)
            {
                // The GetPathTo method returns the path up to the parent of the target
                // For "edges.node", it returns path to "edges", so we need to append "node"
                var fullNodePath = string.Join(".", resolvedNodePath) + "." + nodePath.Split('.')[^1];
                var finalPath = fullNodePath + "." + path;
                expandedPaths.Add(finalPath);
            }
            else
            {
                // If GetPathTo fails, use the original path
                expandedPaths.Add(path);
            }
        }

        return Preserve(expandedPaths.ToArray());
    }

    /// <summary>
    /// Preserves a field using GetPathTo to resolve the full path dynamically.
    /// </summary>
    /// <param name="fieldPath">The field path to preserve</param>
    /// <param name="nodePath">The node path to resolve (e.g., "edges.node")</param>
    /// <returns>The current PreservationBuilder instance for method chaining.</returns>
    public PreservationBuilder PreserveAtPath(string fieldPath, string nodePath)
    {
        var resolvedPath = _sourceQuery.GetPathTo(_sourceQuery.Definition.Name, nodePath);
        if (resolvedPath != null && resolvedPath.Length > 0)
        {
            var fullPath = string.Join(".", resolvedPath) + "." + nodePath.Split('.')[^1] + "." + fieldPath;
            return Preserve(fullPath);
        }
        return Preserve($"{nodePath}.{fieldPath}");
    }

    /// <summary>
    /// Preserves fields from expression using GetPathTo to resolve the full path dynamically.
    /// </summary>
    public PreservationBuilder PreserveAtPathWhere<T>(Expression<Func<T, bool>> predicate, string nodePath)
    {
        var paths = ExpressionFieldExtractor.ExtractFieldPaths(predicate);
        var parameterName = GetParameterName(predicate);
        return PreserveExpandedPaths(paths, nodePath, parameterName, typeof(T));
    }
    private static string? GetParameterName(LambdaExpression lambda)
    {
        return lambda.Parameters.FirstOrDefault()?.Name;
    }

    /// <summary>
    /// Determines if a path represents a parameter name (greedy preservation)
    /// vs a specific field path.
    /// </summary>
    private static bool IsParameterName(string path, string? actualParameterName, Type? parameterType = null)
    {
        // If we have the actual parameter name, use exact match
        if (!string.IsNullOrEmpty(actualParameterName))
        {
            return string.Equals(path, actualParameterName, StringComparison.OrdinalIgnoreCase);
        }

        // If we have parameter type, check if path matches any property name
        if (parameterType != null && !path.Contains('.') && IsValidIdentifier(path))
        {
            var properties = parameterType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            // If the path matches a property name, it's NOT a parameter name
            return !properties.Any(p => string.Equals(p.Name, path, StringComparison.OrdinalIgnoreCase));
        }

        // For paths with dots, they're definitely field paths, not parameter names
        if (path.Contains('.'))
            return false;
            
        if (!IsValidIdentifier(path))
            return false;
            
        // Only single lowercase identifiers are likely parameter names
        return char.IsLower(path[0]);
    }

    /// <summary>
    /// Builds and returns a new QueryBuilder containing only the preserved fields.
    /// </summary>
    /// <returns>A new QueryBuilder instance with only the fields matching the accumulated paths.</returns>
    public QueryBuilder Build()
    {
        // If no paths were specified, return the original query
        if (_pathsToPreserve.Count == 0)
            return _sourceQuery;

        // For explicit Preserve() calls, use all paths as-is without filtering
        // The IsParameterName filtering is only relevant for expression-based methods
        return _sourceQuery.Preserve(_pathsToPreserve.ToArray());
    }

    /// <summary>
    /// Checks if a string is a valid C# identifier.
    /// </summary>
    private static bool IsValidIdentifier(string name)
    {
        if (string.IsNullOrEmpty(name))
            return false;

        // Must start with letter or underscore
        if (!char.IsLetter(name[0]) && name[0] != '_')
            return false;

        // Rest must be letters, digits, or underscores
        return name.Skip(1).All(c => char.IsLetterOrDigit(c) || c == '_');
    }
}
