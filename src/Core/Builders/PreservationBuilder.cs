using System.Linq;
using System.Linq.Expressions;
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
        return PreserveExpandedPaths(paths, nodePath, parameterName);
    }

    /// <summary>
    /// Preserves fields referenced in a typed selector expression.
    /// </summary>
    public PreservationBuilder PreserveFor<T>(Expression<Func<T, object>> selector, string? nodePath = null)
    {
        var paths = ExpressionFieldExtractor.ExtractFieldPaths(selector);
        var parameterName = GetParameterName(selector);
        return PreserveExpandedPaths(paths, nodePath, parameterName);
    }

    /// <summary>
    /// Preserves fields referenced in a typed expression.
    /// </summary>
    public PreservationBuilder PreserveFromExpression<T>(Expression<Func<T, bool>> expression, string? nodePath = null)
    {
        var paths = ExpressionFieldExtractor.ExtractFieldPaths(expression);
        var parameterName = GetParameterName(expression);
        return PreserveExpandedPaths(paths, nodePath, parameterName);
    }

    /// <summary>
    /// Preserves fields referenced in any expression.
    /// </summary>
    public PreservationBuilder PreserveFromExpression(Expression expression, string? nodePath = null)
    {
        var paths = ExpressionFieldExtractor.ExtractFieldPaths(expression);
        return PreserveExpandedPaths(paths, nodePath, null);
    }

    /// <summary>
    /// Expands extracted field paths using GetPathTo when a nodePath is provided.
    /// </summary>
    private PreservationBuilder PreserveExpandedPaths(HashSet<string> extractedPaths, string? nodePath, string? parameterName = null)
    {
        if (string.IsNullOrWhiteSpace(nodePath))
        {
            // No expansion needed
            return Preserve(extractedPaths.ToArray());
        }

        // Check if we have only parameter names (greedy preservation)
        var hasOnlyParameterNames = extractedPaths.All(path => IsParameterName(path, parameterName));
        
        if (hasOnlyParameterNames)
        {
            // For greedy preservation with nodePath, return the original query
            // This preserves the entire structure at the specified node path
            return this;
        }

        var expandedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var queryName = _sourceQuery.Definition.Name;

        foreach (var path in extractedPaths)
        {
            // Skip parameter names in mixed scenarios - they don't get expanded
            if (IsParameterName(path, parameterName))
                continue;
                
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
    private static bool IsParameterName(string path, string? actualParameterName)
    {
        // If we have the actual parameter name, use exact match
        if (!string.IsNullOrEmpty(actualParameterName))
        {
            return string.Equals(path, actualParameterName, StringComparison.OrdinalIgnoreCase);
        }

        // Fallback: A parameter name is a simple identifier without dots
        return !path.Contains('.') && IsValidIdentifier(path);
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

        // Separate parameter names from specific field paths
        // Note: Without the actual parameter name, we use the fallback logic
        var specificPaths = _pathsToPreserve.Where(path => !IsParameterName(path, null)).ToArray();
        
        // If we have only parameter names (greedy preservation), return original query
        if (specificPaths.Length == 0)
            return _sourceQuery;

        // Use only the specific paths for preservation
        return _sourceQuery.Preserve(specificPaths);
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
