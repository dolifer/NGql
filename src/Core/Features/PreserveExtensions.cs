using System.Runtime.CompilerServices;
using NGql.Core.Abstractions;
using NGql.Core.Builders;
using NGql.Core.Extensions;

namespace NGql.Core.Features;

/// <summary>
/// Extension methods for preserving specific QueryBuilder fields based on specified paths.
/// </summary>
internal static class PreserveExtensions
{
    /// <summary>
    /// Preserves only the fields that match or are descendants of the specified paths.
    /// </summary>
    /// <param name="query">The query builder to preserve fields from.</param>
    /// <param name="fieldPaths">The field paths to preserve.</param>
    /// <returns>A new QueryBuilder instance with only the preserved fields.</returns>
    public static QueryBuilder Preserve(this QueryBuilder query, params string[]? fieldPaths)
    {
        if (fieldPaths == null || fieldPaths.Length == 0)
            return query;

        var newQuery = QueryBuilder.CreateDefaultBuilder(query.Definition.Name);
        
        // Copy merging strategy and metadata
        newQuery.Definition.MergingStrategy = query.Definition.MergingStrategy;
        if (query.Definition._metadata != null)
        {
            foreach (var metadata in query.Definition.Metadata)
            {
                newQuery.Definition.Metadata[metadata.Key] = metadata.Value;
            }
        }

        foreach (var targetPath in fieldPaths)
        {
            ExtractMatchingFields(query.Definition.Fields, newQuery.Definition.Fields, targetPath.AsSpan());
        }

        // Carry over the named-fragment definitions the preserved tree still references —
        // spreads without their definitions render as broken GraphQL the server rejects.
        CopyReferencedNamedFragments(query.Definition, newQuery.Definition);

        // Extract variables from all preserved fields
        ExtractVariablesFromFields(newQuery.Definition.Fields, newQuery.Definition.Variables);

        return newQuery;
    }

    /// <summary>
    /// Copies into <paramref name="targetDefinition"/> every named-fragment definition that the
    /// preserved field tree references via spreads, following spreads transitively through the
    /// copied fragments themselves. Unreferenced definitions are skipped — GraphQL rejects
    /// operations with unused fragments. Undeclared spread names are left alone (NGql is
    /// schemaless; they render verbatim and the server reports them).
    /// </summary>
    private static void CopyReferencedNamedFragments(QueryDefinition sourceDefinition, QueryDefinition targetDefinition)
    {
        var declared = sourceDefinition._namedFragments;
        if (declared is not { Count: > 0 } || targetDefinition._fields is not { Count: > 0 }) return;

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var pending = new Stack<string>();
        foreach (var field in targetDefinition._fields.Values)
        {
            CollectSpreadNames(field, seen, pending);
        }

        while (pending.Count > 0)
        {
            var name = pending.Pop();
            if (!declared.TryGetValue(name, out var fragment)) continue;

            var clone = fragment.DeepClone();
            targetDefinition._namedFragments ??= new Dictionary<string, NamedFragmentDefinition>(StringComparer.Ordinal);
            targetDefinition._namedFragments[name] = clone;

            if (clone._fields is { Count: > 0 })
            {
                ExtractVariablesFromFields(clone._fields, targetDefinition.Variables);
            }
            CollectFragmentBodySpreadNames(clone._fields, clone._fragments, clone._spreadFragments, seen, pending);
        }
    }

    private static void CollectSpreadNames(FieldDefinition field, HashSet<string> seen, Stack<string> pending)
        => CollectFragmentBodySpreadNames(field._children, field._fragments, field._spreadFragments, seen, pending);

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Major Code Smell", "S3267:Loops should be simplified using the \"Where\" LINQ method",
        Justification = "HashSet.Add is the filter AND the side effect — a Where(seen.Add) would hide the mutation; plain loops also avoid enumerator allocations on the preserve path.")]
    private static void CollectFragmentBodySpreadNames(
        FieldChildren? fields,
        Dictionary<string, InlineFragmentDefinition>? fragments,
        List<string>? spreads,
        HashSet<string> seen,
        Stack<string> pending)
    {
        if (spreads is { Count: > 0 })
        {
            foreach (var name in spreads)
            {
                if (seen.Add(name)) pending.Push(name);
            }
        }

        if (fragments is { Count: > 0 })
        {
            foreach (var fragment in fragments.Values)
            {
                CollectFragmentBodySpreadNames(fragment._fields, fragment._fragments, fragment._spreadFragments, seen, pending);
            }
        }

        if (fields is { Count: > 0 })
        {
            foreach (var child in fields.AsSpan())
            {
                CollectSpreadNames(child, seen, pending);
            }
        }
    }

    private static void ExtractMatchingFields(Dictionary<string, FieldDefinition> sourceFields,
        Dictionary<string, FieldDefinition> targetFields, ReadOnlySpan<char> path)
    {
        var isLeafPath = SplitFirstSegment(path, out var currentSegment, out var remainingPath);

        var match = FindFieldByNameOrAlias(sourceFields, currentSegment);
        if (!match.HasValue) return;

        var (fieldKey, fieldDef) = match.Value;

        if (isLeafPath)
        {
            // Deep-clone the preserved subtree: storing the source reference would alias the
            // two builders, letting later mutations of either leak into the other (the
            // role-based filtering scenario hands the preserved query out while the source
            // keeps evolving).
            targetFields[fieldKey] = fieldDef.DeepClone();
            return;
        }

        if (fieldDef._children is null) return;

        var existing = GetOrAddTargetChild(targetFields, fieldKey, fieldDef);
        ExtractMatchingFields(fieldDef._children, existing._children!, remainingPath);
    }

    private static void ExtractMatchingFields(FieldChildren sourceFields, FieldChildren targetFields, ReadOnlySpan<char> path)
    {
        var isLeafPath = SplitFirstSegment(path, out var currentSegment, out var remainingPath);

        if (!TryFindByNameOrAlias(sourceFields, currentSegment, out var fieldKey, out var fieldDef)) return;

        if (isLeafPath)
        {
            // Same isolation rule as the Dictionary variant above: the preserved tree must
            // never share FieldDefinition instances with the source.
            targetFields.Set(fieldKey, fieldDef.DeepClone());
            return;
        }

        if (fieldDef._children is null) return;

        var targetChild = GetOrAddTargetChild(targetFields, fieldKey, fieldDef);
        ExtractMatchingFields(fieldDef._children, targetChild._children!, remainingPath);
    }

    private static bool SplitFirstSegment(ReadOnlySpan<char> path, out ReadOnlySpan<char> current, out ReadOnlySpan<char> remaining)
    {
        var dot = path.IndexOf('.');
        if (dot < 0)
        {
            current = path;
            remaining = ReadOnlySpan<char>.Empty;
            return true;
        }
        current = path[..dot];
        remaining = path[(dot + 1)..];
        return false;
    }

    private static bool TryFindByNameOrAlias(FieldChildren children, ReadOnlySpan<char> nameOrAlias, out string key, out FieldDefinition field)
    {
        foreach (var f in children.AsSpan())
        {
            if (f.Name.AsSpan().Equals(nameOrAlias, StringComparison.OrdinalIgnoreCase)
                || (f.Alias is { Length: > 0 } alias && alias.AsSpan().Equals(nameOrAlias, StringComparison.OrdinalIgnoreCase)))
            {
                key = f.Name;
                field = f;
                return true;
            }
        }
        key = string.Empty;
        field = null!;
        return false;
    }

    private static FieldDefinition GetOrAddTargetChild(Dictionary<string, FieldDefinition> target, string fieldKey, FieldDefinition source)
    {
        if (!target.TryGetValue(fieldKey, out var existing))
        {
            existing = CloneFieldWithoutChildren(source);
            target[fieldKey] = existing;
        }
        existing._children ??= new FieldChildren();
        return existing;
    }

    private static FieldDefinition GetOrAddTargetChild(FieldChildren targetFields, string fieldKey, FieldDefinition source)
    {
        if (!targetFields.TryGetValue(fieldKey, out var existing) || existing is null)
        {
            existing = CloneFieldWithoutChildren(source);
            targetFields.Set(fieldKey, existing);
        }
        existing._children ??= new FieldChildren();
        return existing;
    }

    /// <summary>
    /// Creates a shallow clone of a field without its children, preserving arguments and metadata.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static FieldDefinition CloneFieldWithoutChildren(FieldDefinition source)
    {
        // FieldDefinition._type is always set by every constructor; the null-coalesce was
        // a defensive guard for the rare `with { Type = null }` path and never fires here.
        // Arguments are copied, not referenced — intermediate path nodes must be isolated
        // from the source the same way leaf subtrees are (Where on a preserved intermediate
        // would otherwise mutate the source's argument dictionary).
        var cloned = new FieldDefinition(
            source.Name,
            source._type!,
            source.Alias,
            source._arguments is null ? null : new SortedDictionary<string, object?>(source._arguments, StringComparer.OrdinalIgnoreCase))
        {
            Path = source.Path
        };

        cloned.IsNeverMerge = source.IsNeverMerge;

        if (source._metadata != null)
        {
            foreach (var meta in source._metadata)
            {
                cloned.Metadata[meta.Key] = meta.Value;
            }
        }

        return cloned;
    }

    // All call sites pre-check the fields collection for null (NavigatePath, PreserveAtPath,
    // QueryDefinitionExtensions.NavigatePath); the parameter could safely be tightened but
    // is kept nullable to preserve the existing internal API signature.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static KeyValuePair<string, FieldDefinition>? FindFieldByNameOrAlias(IReadOnlyDictionary<string, FieldDefinition>? fields, ReadOnlySpan<char> nameOrAlias)
    {
        foreach (var kvp in fields!)
        {
            if (MatchesKeyNameOrAlias(kvp, nameOrAlias))
            {
                return kvp;
            }
        }

        return null;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool MatchesKeyNameOrAlias(KeyValuePair<string, FieldDefinition> kvp, ReadOnlySpan<char> nameOrAlias)
        => nameOrAlias.Equals(kvp.Key.AsSpan(), StringComparison.OrdinalIgnoreCase)
        || nameOrAlias.Equals(kvp.Value.Name.AsSpan(), StringComparison.OrdinalIgnoreCase)
        || (!string.IsNullOrEmpty(kvp.Value.Alias) && nameOrAlias.Equals(kvp.Value.Alias.AsSpan(), StringComparison.OrdinalIgnoreCase));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ExtractVariablesFromFields(FieldChildren children, SortedSet<Variable> variables)
    {
        foreach (var field in children.AsSpan())
        {
            ExtractVariablesFromField(field, variables);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ExtractVariablesFromFields(Dictionary<string, FieldDefinition> fields, SortedSet<Variable> variables)
    {
        foreach (var field in fields.Values)
        {
            ExtractVariablesFromField(field, variables);
        }
    }

    private static void ExtractVariablesFromField(FieldDefinition field, SortedSet<Variable> variables)
    {
        if (field._arguments?.Count > 0)
        {
            Helpers.ExtractVariablesFromValue(field._arguments, variables);
        }
        if (field._children is { Count: > 0 })
        {
            ExtractVariablesFromFields(field._children, variables);
        }
    }
}
