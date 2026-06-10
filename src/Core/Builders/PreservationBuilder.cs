using System.Linq.Expressions;
using NGql.Core.Extensions;
using NGql.Core.Features;

namespace NGql.Core.Builders;

/// <summary>
/// Builds a derived <see cref="QueryBuilder"/> that contains only a chosen subset of an
/// existing query's fields.
/// </summary>
/// <remarks>
/// Use <see cref="Create(QueryBuilder)"/> to start, accumulate paths via <see cref="Preserve(string[])"/>
/// or <see cref="PreserveFromExpression{T}(Expression{Func{T, bool}}, string?)"/>, then call
/// <see cref="Build"/> to materialize the trimmed query. The source builder is never mutated.
/// <para>
/// Typical use: role-based field filtering — start from a "full" query and emit a smaller
/// projection per caller without re-building from scratch.
/// </para>
/// </remarks>
public sealed class PreservationBuilder
{
    private readonly QueryBuilder _sourceQuery;
    private readonly HashSet<string> _pathsToPreserve;

    private PreservationBuilder(QueryBuilder sourceQuery)
    {
        _sourceQuery = sourceQuery ?? throw new ArgumentNullException(nameof(sourceQuery));
        _pathsToPreserve = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Creates a new <see cref="PreservationBuilder"/> over <paramref name="query"/>.</summary>
    /// <param name="query">The source query to project from. Not mutated.</param>
    /// <exception cref="ArgumentNullException"><paramref name="query"/> is null.</exception>
    public static PreservationBuilder Create(QueryBuilder query) => new(query);

    /// <summary>
    /// Adds dotted field paths to the preservation set. When a more-specific child path is
    /// added, any previously-added parent paths are dropped automatically (you don't end up
    /// preserving both <c>user</c> and <c>user.name</c> — only the more specific wins).
    /// </summary>
    /// <param name="fieldPaths">Dot-separated field paths to preserve. Null/whitespace entries are skipped.</param>
    /// <returns>This builder, for chaining.</returns>
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Major Code Smell", "S3267:Loops should be simplified using the \"Where\" LINQ method",
        Justification = "Plain foreach avoids the Where enumerator; the span-based prefix check below avoids one string concat per (path, existing) pair.")]
    public PreservationBuilder Preserve(params string[]? fieldPaths)
    {
        if (fieldPaths == null || fieldPaths.Length == 0) return this;

        foreach (var path in fieldPaths)
        {
            if (string.IsNullOrWhiteSpace(path)) continue;

            // Remove parent paths when adding more specific child. The check is equivalent to
            // path.StartsWith(existing + ".") without allocating the concatenated prefix.
            if (_pathsToPreserve.Count > 0)
            {
                _pathsToPreserve.RemoveWhere(existing =>
                    path.Length > existing.Length
                    && path[existing.Length] == '.'
                    && path.AsSpan(0, existing.Length).Equals(existing, StringComparison.OrdinalIgnoreCase));
            }

            _pathsToPreserve.Add(path);
        }

        return this;
    }

    /// <summary>
    /// Preserves <paramref name="fieldPath"/> within the subtree at <paramref name="nodePath"/>.
    /// Useful when the same field name appears multiple times in the tree and you want to
    /// scope the preservation to one specific parent.
    /// </summary>
    /// <param name="fieldPath">Field name (or dotted relative path) inside the node.</param>
    /// <param name="nodePath">Dotted path identifying which parent node to scope to.</param>
    /// <returns>This builder, for chaining.</returns>
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
    /// Preserves every field touched by the predicate <paramref name="expression"/>. The
    /// expression is walked (not executed) to collect member access chains; comparisons,
    /// logical operators, ternaries, null-coalescing, and LINQ method calls (<c>Any</c>,
    /// <c>Where</c>, <c>First</c>) are all supported.
    /// </summary>
    /// <typeparam name="T">CLR type whose property hierarchy mirrors the query tree.</typeparam>
    /// <param name="expression">Predicate expression whose member access chains identify fields to preserve.</param>
    /// <param name="nodePath">Optional dotted path to scope preservation to a specific subtree.</param>
    /// <returns>This builder, for chaining.</returns>
    public PreservationBuilder PreserveFromExpression<T>(Expression<Func<T, bool>> expression, string? nodePath = null)
        => PreserveFromExpressionCore(expression, nodePath, null, typeof(T), null);

    /// <summary>
    /// Same as <see cref="PreserveFromExpression{T}(Expression{Func{T, bool}}, string?)"/>
    /// but adds a <paramref name="localMap"/> that translates expression parameter names
    /// to relative dotted paths inside <paramref name="nodePath"/>'s subtree. Useful when
    /// the predicate references multiple sibling fields.
    /// </summary>
    /// <param name="expression">Predicate expression.</param>
    /// <param name="nodePath">Dotted path to scope preservation to.</param>
    /// <param name="localMap">Map of expression-parameter name to relative path segments.</param>
    /// <returns>This builder, for chaining.</returns>
    public PreservationBuilder PreserveFromExpression<T>(Expression<Func<T, bool>> expression, string nodePath, Dictionary<string, string[]> localMap)
        => PreserveFromExpressionCore(expression, nodePath, localMap, typeof(T), null);

    /// <summary>
    /// Untyped overload of <see cref="PreserveFromExpression{T}(Expression{Func{T, bool}}, string?)"/>.
    /// Useful when the predicate is constructed dynamically (e.g. via DynamicExpresso) and
    /// you don't have a compile-time <c>T</c>.
    /// </summary>
    /// <param name="expression">Lambda expression whose body is walked for member-access chains.</param>
    /// <param name="nodePath">Optional dotted path to scope preservation to.</param>
    /// <returns>This builder, for chaining.</returns>
    public PreservationBuilder PreserveFromExpression(Expression expression, string? nodePath = null)
        => PreserveFromExpressionCore(expression, nodePath, null, InferTypeFromLambda(expression), null);

    /// <summary>Untyped overload accepting a parameter-name-to-path <paramref name="localMap"/>.</summary>
    /// <param name="expression">Lambda expression.</param>
    /// <param name="nodePath">Dotted path to scope preservation to.</param>
    /// <param name="localMap">Map of expression-parameter name to relative path segments.</param>
    /// <returns>This builder, for chaining.</returns>
    public PreservationBuilder PreserveFromExpression(Expression expression, string nodePath, Dictionary<string, string[]> localMap)
        => PreserveFromExpressionCore(expression, nodePath, localMap, InferTypeFromLambda(expression), null);

    /// <summary>
    /// Untyped overload that, in addition to expression-derived paths, unconditionally
    /// preserves <paramref name="alwaysPreserveFields"/> at <paramref name="nodePath"/> —
    /// useful for IDs and tracking columns that callers always want regardless of predicate.
    /// </summary>
    /// <param name="expression">Lambda expression.</param>
    /// <param name="nodePath">Dotted path to scope preservation to.</param>
    /// <param name="localMap">Map of expression-parameter name to relative path segments.</param>
    /// <param name="alwaysPreserveFields">Field names always added under <paramref name="nodePath"/>.</param>
    /// <returns>This builder, for chaining.</returns>
    public PreservationBuilder PreserveFromExpression(Expression expression, string nodePath, Dictionary<string, string[]> localMap, params string[] alwaysPreserveFields)
        => PreserveFromExpressionCore(expression, nodePath, localMap, InferTypeFromLambda(expression), alwaysPreserveFields);

    /// <summary>
    /// Materializes a new <see cref="QueryBuilder"/> containing only the accumulated
    /// preserved paths. Returns the source builder unchanged when no paths were added.
    /// The source builder is never mutated.
    /// </summary>
    /// <returns>A new builder containing the preserved subset, or the source if no paths were added.</returns>
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
