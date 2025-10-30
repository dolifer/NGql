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
    /// <returns>The current PreservationBuilder instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// var result = PreservationBuilder
    ///     .Create(query)
    ///     .PreserveWhere&lt;MyModel&gt;(x => x.user.profile.age > 10)
    ///     .PreserveWhere&lt;MyModel&gt;(x => x.user.email != null)
    ///     .Build();
    /// // Preserves only: user.profile.age, user.email
    /// </code>
    /// </example>
    public PreservationBuilder PreserveWhere<T>(Expression<Func<T, bool>> predicate)
    {
        var paths = ExpressionFieldExtractor.ExtractFieldPaths(predicate);
        return Preserve(paths.ToArray());
    }

    /// <summary>
    /// Preserves fields referenced in a typed selector expression.
    /// Useful for selecting specific fields without a predicate.
    /// Supports anonymous types for flexible field selection.
    /// </summary>
    /// <typeparam name="T">The type being queried</typeparam>
    /// <param name="selector">The selector expression (e.g., x => new { x.user.name, x.user.email })</param>
    /// <returns>The current PreservationBuilder instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// var result = PreservationBuilder
    ///     .Create(query)
    ///     .PreserveFor&lt;MyModel&gt;(x => new { x.user.name, x.user.email })
    ///     .Build();
    /// // Preserves only: user.name, user.email
    /// </code>
    /// </example>
    public PreservationBuilder PreserveFor<T>(Expression<Func<T, object>> selector)
    {
        var paths = ExpressionFieldExtractor.ExtractFieldPaths(selector);
        return Preserve(paths.ToArray());
    }

    /// <summary>
    /// Preserves fields referenced in any expression.
    /// Works with runtime-parsed expressions (e.g., from DynamicExpresso).
    /// </summary>
    /// <param name="expression">The expression to analyze</param>
    /// <returns>The current PreservationBuilder instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// // With DynamicExpresso:
    /// var interpreter = new Interpreter();
    /// var expr = interpreter.Parse("user.profile.email != null");
    ///
    /// var result = PreservationBuilder
    ///     .Create(query)
    ///     .PreserveFromExpression(expr)
    ///     .Build();
    /// // Preserves only: user.profile.email
    /// </code>
    /// </example>
    public PreservationBuilder PreserveFromExpression(Expression expression)
    {
        var paths = ExpressionFieldExtractor.ExtractFieldPaths(expression);
        return Preserve(paths.ToArray());
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

        // Delegate to the existing PreserveExtensions logic
        return _sourceQuery.Preserve(_pathsToPreserve.ToArray());
    }
}
