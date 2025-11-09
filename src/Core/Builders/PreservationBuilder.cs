using System.Linq.Expressions;
using NGql.Core.Abstractions;
using NGql.Core.Extensions;
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
    public PreservationBuilder Preserve(params string[]? fieldPaths)
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
            var nodeField = QueryDefinitionExtensions.NavigatePath(_sourceQuery.Definition.Fields, fullNodePath.AsSpan(), out _);
            if (nodeField == null || !nodeField.HasFields) continue;

            if (fieldPath.Contains('.'))
            {
                var resolvedPath = QueryDefinitionExtensions.NavigatePath(nodeField.Fields, fieldPath.AsSpan(), out var resolved, fullNodePath)
                    != null ? resolved : null;
                if (resolvedPath != null)
                {
                    Preserve(resolvedPath);
                }
            }
            else
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

    private PreservationBuilder PreserveFromExpressionCore(
        Expression expression,
        string? nodePath,
        Dictionary<string, string[]>? localMap,
        Type? parameterType,
        string[]? alwaysPreserveFields)
    {
        var processor = new ExpressionPreservationProcessor(_sourceQuery, path => Preserve(path));
        processor.ProcessExpression(expression, nodePath, localMap, parameterType, alwaysPreserveFields);
        return this;
    }
}
