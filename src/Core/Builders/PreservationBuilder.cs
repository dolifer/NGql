using System.Linq.Expressions;
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
        var lastIndex = nodePath.LastIndexOf('.');
        var lastSegment = lastIndex == -1 ? nodePath : nodePath.Substring(lastIndex + 1);
        var fieldPathHasDot = fieldPath.Contains('.');

        foreach (var rootField in _sourceQuery.Definition.Fields.Values)
        {
            PreserveAtPathForRoot(rootField.Alias ?? rootField.Name, fieldPath, nodePath, lastSegment, fieldPathHasDot);
        }
        return this;
    }

    private void PreserveAtPathForRoot(string rootName, string fieldPath, string nodePath, string lastSegment, bool fieldPathHasDot)
    {
        var pathToNode = _sourceQuery.GetPathTo(rootName, nodePath);
        if (pathToNode.Length == 0) return;

        var fullNodePath = JoinPath(pathToNode, lastSegment);
        var nodeField = QueryDefinitionExtensions.NavigatePath(_sourceQuery.Definition.Fields, fullNodePath.AsSpan(), out _);
        if (nodeField is null) return;
        if (!nodeField.HasFields) return;

        if (fieldPathHasDot)
        {
            PreserveResolvedNestedPath(nodeField.Fields, fieldPath, fullNodePath);
        }
        else
        {
            PreserveDirectMatch(nodeField.Fields, fieldPath, fullNodePath);
        }
    }

    private void PreserveResolvedNestedPath(IReadOnlyDictionary<string, Abstractions.FieldDefinition> nodeFields, string fieldPath, string fullNodePath)
    {
        if (QueryDefinitionExtensions.NavigatePath(nodeFields, fieldPath.AsSpan(), out var resolved, fullNodePath) != null
            && resolved is not null)
        {
            Preserve(resolved);
        }
    }

    private void PreserveDirectMatch(IReadOnlyDictionary<string, Abstractions.FieldDefinition> nodeFields, string fieldPath, string fullNodePath)
    {
        var match = PreserveExtensions.FindFieldByNameOrAlias(nodeFields, fieldPath.AsSpan());
        if (match.HasValue)
        {
            Preserve(string.Concat(fullNodePath, ".", match.Value.Key));
        }
    }

    /// <summary>
    /// Builds <c>{segments[0]}.{segments[1]}...{segments[N-1]}.{tail}</c> in a single allocation —
    /// replaces the prior <c>string.Join + "." + tail</c> two-step.
    /// </summary>
    private static string JoinPath(string[] segments, string tail)
    {
        var totalLength = tail.Length + segments.Length; // dots between segments + before tail
        for (int i = 0; i < segments.Length; i++) totalLength += segments[i].Length;

        return string.Create(totalLength, (segments, tail), static (span, st) =>
        {
            var pos = 0;
            for (int i = 0; i < st.segments.Length; i++)
            {
                var seg = st.segments[i].AsSpan();
                seg.CopyTo(span[pos..]);
                pos += seg.Length;
                span[pos++] = '.';
            }
            st.tail.AsSpan().CopyTo(span[pos..]);
        });
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
