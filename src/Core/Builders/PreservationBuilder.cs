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
    /// Preserves fields referenced in a typed selector expression.
    /// </summary>
    public PreservationBuilder PreserveFor<T>(Expression<Func<T, object>> selector, string? nodePath = null)
    {
        var paths = ExpressionFieldExtractor.ExtractFieldPaths(selector);
        var parameterName = GetParameterName(selector);
        return PreserveExpandedPaths(paths, nodePath, parameterName, typeof(T));
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
    /// Preserves fields referenced in any expression.
    /// </summary>
    public PreservationBuilder PreserveFromExpression(Expression expression, string? nodePath = null)
    {
        var paths = ExpressionFieldExtractor.ExtractFieldPaths(expression);
        return PreserveExpandedPaths(paths, nodePath, null, null);
    }

    /// <summary>
    /// Expands extracted field paths using GetPathTo when a nodePath is provided.
    /// </summary>
    private PreservationBuilder PreserveExpandedPaths(HashSet<string> extractedPaths, string? nodePath, string? parameterName = null, Type? parameterType = null)
    {
        if (string.IsNullOrWhiteSpace(nodePath))
        {
            // Filter out parameter names if we have specific field paths
            var specificPaths = extractedPaths.Where(path => !IsParameterName(path, parameterName, parameterType)).ToArray();
            return specificPaths.Length > 0 ? Preserve(specificPaths) : Preserve(extractedPaths.ToArray());
        }

        // Filter out parameter names from extracted paths before processing
        var filteredPaths = extractedPaths.Where(path => !IsParameterName(path, parameterName, parameterType)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        
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
    /// Extracts the parameter name from a lambda expression.
    /// </summary>
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
            return !properties.Any(p => string.Equals(p.Name, path, StringComparison.OrdinalIgnoreCase));
        }

        // Fallback: A parameter name is a simple identifier without dots
        // But we need to be more specific - common parameter names are lowercase or camelCase
        // Field names from expressions are usually PascalCase (like ProfileData)
        if (path.Contains('.'))
            return false;
            
        if (!IsValidIdentifier(path))
            return false;
            
        // If the path is PascalCase (starts with uppercase), it's likely a field name, not a parameter
        // Parameter names are typically camelCase (start with lowercase)
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
