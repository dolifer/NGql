using NGql.Core.Abstractions;
using NGql.Core.Builders;
using NGql.Core.Exceptions;
using NGql.Core.Extensions;

namespace NGql.Core.Features;

/// <summary>
/// Handles merging of query definitions using different strategies
/// </summary>
internal static class QueryMerger
{
    /// <summary>
    /// Merges an incoming query definition into the target query definition, updating all related state.
    /// </summary>
    public static void MergeQuery(
        QueryDefinition targetDefinition,
        QueryMap queryMap,
        QueryBuilder? queryBuilder,
        in QueryDefinition incomingQuery)
    {
        // Named fragments are operation-scoped, not field-scoped: merge them even when the incoming
        // query declares nothing but fragments (an unusual but valid shape — the renderer emits
        // declared-but-unused fragment definitions).
        MergeNamedFragments(targetDefinition, incomingQuery);
        MergeVariables(targetDefinition, incomingQuery);

        if (incomingQuery._fields == null || incomingQuery._fields.Count == 0) return;

        var beforeCount = targetDefinition.Fields.Count;
        ApplyFieldMerge(targetDefinition.FieldsInternal, incomingQuery, targetDefinition.MergingStrategy, queryMap);

        if (targetDefinition.Fields.Count != beforeCount)
        {
            queryMap.UpdateRootMapping(targetDefinition);
        }
    }

    /// <summary>
    /// Merges every named fragment the incoming query declares into <paramref name="targetDefinition"/>
    /// using the target's <see cref="QueryDefinition.GetOrAddNamedFragment"/> collision semantics:
    /// same name + same <c>onType</c> reuses the existing fragment (and merges its body); same name +
    /// a DIFFERENT <c>onType</c> throws <see cref="InvalidOperationException"/>. Each fragment's body
    /// (fields, nested inline fragments, and spreads) is deep-cloned before entering the target so the
    /// merged operation never aliases the source's fragment definitions.
    /// </summary>
    private static void MergeNamedFragments(QueryDefinition targetDefinition, in QueryDefinition incomingQuery)
    {
        var incomingFragments = incomingQuery._namedFragments;
        if (incomingFragments is not { Count: > 0 }) return;

        foreach (var incoming in incomingFragments.Values)
        {
            // Throws InvalidOperationException on a name collision with a conflicting onType —
            // reuses the existing declaration semantics rather than inventing new merge rules.
            var target = targetDefinition.GetOrAddNamedFragment(incoming.Name, incoming.OnType);
            MergeNamedFragmentBody(target, incoming);
        }
    }

    private static void MergeNamedFragmentBody(NamedFragmentDefinition target, NamedFragmentDefinition incoming)
    {
        if (incoming._fields is { Count: > 0 })
        {
            var targetFields = target.GetOrCreateFieldsStore();
            foreach (var child in incoming._fields.AsSpan())
            {
                FieldBuilder.Include(targetFields, child);
            }
        }

        FieldDefinitionExtensions.MergeInlineFragmentsInto(ref target._fragments, incoming._fragments);
        FieldDefinitionExtensions.MergeSpreadNamesInto(ref target._spreadFragments, incoming._spreadFragments);
    }

    private static void MergeVariables(QueryDefinition targetDefinition, in QueryDefinition incomingQuery)
    {
        var incomingVars = incomingQuery._variables;
        if (incomingVars is null || incomingVars.Count == 0) return;

        var targetVars = targetDefinition._variables;
        if (targetVars is null)
        {
            targetDefinition.Variables = new SortedSet<Variable>(incomingVars);
            return;
        }

        foreach (var v in incomingVars)
            targetVars.Add(v);
    }

    /// <summary>
    /// Mutates <paramref name="fields"/> in place by applying every field from the incoming query
    /// according to the resolved merging strategy. Avoids the O(N) copy-out / copy-back of the
    /// existing approach so a chain of <c>Include()</c>s is O(K) per call instead of O(N+K).
    /// </summary>
    private static void ApplyFieldMerge(
        Dictionary<string, FieldDefinition> fields,
        QueryDefinition incomingQuery,
        MergingStrategy rootStrategy,
        QueryMap queryMap)
    {
        var strategy = GetEffectiveMergingStrategy(rootStrategy, incomingQuery.MergingStrategy);
        var queryName = incomingQuery.Name;

        foreach (var (originalFieldKey, incomingField) in incomingQuery.FieldsInternal)
        {
            switch (strategy)
            {
                case MergingStrategy.MergeByDefault:
                    FieldBuilder.Include(fields, incomingField);
                    queryMap.SetMapping(queryName, originalFieldKey);
                    break;

                case MergingStrategy.NeverMerge:
                    AddFieldWithUniqueKey(fields, originalFieldKey, MarkAsNeverMerge(incomingField), queryMap, queryName);
                    break;

                case MergingStrategy.MergeByFieldPath:
                    ApplyMergeByFieldPath(fields, originalFieldKey, incomingField, queryMap, queryName);
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(rootStrategy), $"Merging strategy {strategy} is not implemented");
            }
        }
    }

    private static void ApplyMergeByFieldPath(
        Dictionary<string, FieldDefinition> fields,
        string originalFieldKey,
        FieldDefinition incomingField,
        QueryMap queryMap,
        string queryName)
    {
        var mergeTarget = FindMergeTarget(fields, incomingField);

        if (mergeTarget == null)
        {
            AddFieldWithUniqueKey(fields, originalFieldKey, incomingField, queryMap, queryName);
            return;
        }

        try
        {
            // Mutate the existing field in place — we own the reference (we're about to overwrite the
            // dictionary entry with the same instance). Avoids cloning the entire subtree on every Include.
            FieldDefinitionExtensions.MergeFieldsInPlace(mergeTarget.Value.Field, incomingField);
            queryMap.SetMapping(queryName, mergeTarget.Value.Key);
        }
        catch (QueryMergeException ex)
        {
            throw new QueryMergeException($"Cannot merge query '{queryName}' due to type conflicts in field '{incomingField.Name}'", ex);
        }
    }

    private static MergingStrategy GetEffectiveMergingStrategy(MergingStrategy rootStrategy, MergingStrategy childStrategy)
    {
        if (childStrategy == MergingStrategy.NeverMerge)
        {
            return MergingStrategy.NeverMerge;
        }

        return rootStrategy switch
        {
            MergingStrategy.NeverMerge => MergingStrategy.NeverMerge,
            MergingStrategy.MergeByDefault => childStrategy,
            _ => rootStrategy,
        };
    }

    private static void AddFieldWithUniqueKey(
        Dictionary<string, FieldDefinition> fields,
        string originalFieldKey,
        FieldDefinition incomingField,
        QueryMap queryMap,
        string queryName)
    {
        var uniqueKey = KeyGenerator.GenerateUniqueKey(incomingField._effectiveName, fields.Keys);

        // Deep-clone so the target dictionary owns its subtree exclusively. Subsequent in-place
        // merges (MergeFieldsInPlace below) must not leak field additions back into the source
        // QueryBuilder that supplied incomingField.
        var fieldToAdd = incomingField.DeepClone();
        if (!string.Equals(uniqueKey, originalFieldKey, StringComparison.OrdinalIgnoreCase))
        {
            fieldToAdd = fieldToAdd with { Alias = uniqueKey, _effectiveName = uniqueKey };
        }

        fields[uniqueKey] = fieldToAdd;
        queryMap.SetMapping(queryName, uniqueKey);
    }

    private static (string Key, FieldDefinition Field)? FindMergeTarget(Dictionary<string, FieldDefinition> existingFields, FieldDefinition incomingField)
    {
        foreach (var (key, existingField) in existingFields)
        {
            if (!string.Equals(existingField.Name, incomingField.Name, StringComparison.OrdinalIgnoreCase))
                continue;

            if (existingField.IsNeverMerge)
                continue;

            if (FieldDefinitionExtensions.CanMergeFields(existingField, incomingField))
                return (key, existingField);
        }

        return null;
    }

    private static FieldDefinition MarkAsNeverMerge(FieldDefinition field)
        => field.IsNeverMerge ? field : field with { IsNeverMerge = true };
}
