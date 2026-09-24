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
        // Checked before anything is merged, so a conflict leaves the target untouched.
        ThrowOnVariableTypeConflict(targetDefinition, incomingQuery);

        // Named fragments are operation-scoped, not field-scoped: merge them even when the incoming
        // query declares nothing but fragments (an unusual but valid shape — the renderer emits
        // declared-but-unused fragment definitions).
        MergeNamedFragments(targetDefinition, incomingQuery);
        MergeVariables(targetDefinition, incomingQuery);

        if (incomingQuery._fields == null || incomingQuery._fields.Count == 0) return;

        var beforeCount = targetDefinition.Fields.Count;
        ApplyFieldMerge(targetDefinition, incomingQuery, targetDefinition.MergingStrategy, queryMap);

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

    private static void ThrowOnVariableTypeConflict(QueryDefinition targetDefinition, in QueryDefinition incomingQuery)
    {
        if (targetDefinition._variables is not { Count: > 0 } declared || incomingQuery._variables is not { Count: > 0 } incoming)
        {
            return;
        }

        foreach (var variable in incoming)
        {
            if (Helpers.FindTypeConflict(declared, variable) is { } existing)
            {
                throw new QueryMergeException(
                    $"Cannot merge query '{incomingQuery.Name}': {Helpers.VariableTypeConflictMessage(existing, variable)}");
            }
        }
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
    /// Mutates <paramref name="targetDefinition"/>'s root fields in place by applying every field
    /// from the incoming query according to the resolved merging strategy. Avoids the O(N)
    /// copy-out / copy-back of the existing approach so a chain of <c>Include()</c>s is O(K) per
    /// call instead of O(N+K).
    /// </summary>
    private static void ApplyFieldMerge(
        QueryDefinition targetDefinition,
        QueryDefinition incomingQuery,
        MergingStrategy rootStrategy,
        QueryMap queryMap)
    {
        var fields = targetDefinition.FieldsInternal;
        var strategy = GetEffectiveMergingStrategy(rootStrategy, incomingQuery.MergingStrategy);
        var queryName = incomingQuery.Name;

        foreach (var (originalFieldKey, incomingField) in incomingQuery.FieldsInternal)
        {
            switch (strategy)
            {
                case MergingStrategy.MergeByDefault:
                    // FieldBuilder.Include may insert a brand-new root key (when no existing
                    // field shares its Name) — resync the merge index by count on next use rather
                    // than tracking this path's insertions individually, since MergeByDefault
                    // never consults FindMergeTarget/the fingerprint index itself.
                    queryMap.SetMapping(queryName, FieldBuilder.Include(fields, incomingField));
                    break;

                case MergingStrategy.NeverMerge:
                    AddFieldWithUniqueKey(targetDefinition, originalFieldKey, MarkAsNeverMerge(incomingField), queryMap, queryName);
                    break;

                case MergingStrategy.MergeByFieldPath:
                    ApplyMergeByFieldPath(targetDefinition, originalFieldKey, incomingField, queryMap, queryName);
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(rootStrategy), $"Merging strategy {strategy} is not implemented");
            }
        }
    }

    private static void ApplyMergeByFieldPath(
        QueryDefinition targetDefinition,
        string originalFieldKey,
        FieldDefinition incomingField,
        QueryMap queryMap,
        string queryName)
    {
        var fields = targetDefinition.FieldsInternal;
        var mergeTarget = FindMergeTarget(targetDefinition, fields, incomingField);

        if (mergeTarget == null)
        {
            AddFieldWithUniqueKey(targetDefinition, originalFieldKey, incomingField, queryMap, queryName);
            return;
        }

        try
        {
            // Mutate the existing field in place — we own the reference (we're about to overwrite the
            // dictionary entry with the same instance). Avoids cloning the entire subtree on every Include.
            FieldDefinitionExtensions.MergeFieldsInPlace(mergeTarget.Value.Field, incomingField);

            // The merge can change the target's deep fingerprint (new argument-bearing content
            // merged deeper into an already-significant subtree — see MergeFieldsInPlace's memo
            // invalidation). The fingerprint-bucketed index must be told explicitly: the field kept
            // its identity (same reference, same key) but may now belong under a different
            // fingerprint bucket, and nothing else would ever notice the move.
            targetDefinition.MergeIndex.ReindexFingerprint(fields, incomingField.Name, mergeTarget.Value.Key);
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
        QueryDefinition targetDefinition,
        string originalFieldKey,
        FieldDefinition incomingField,
        QueryMap queryMap,
        string queryName)
    {
        var fields = targetDefinition.FieldsInternal;
        var mergeIndex = targetDefinition.MergeIndex;
        var uniqueKey = KeyGenerator.GenerateUniqueKey(mergeIndex, fields, incomingField._effectiveName);

        // Deep-clone so the target dictionary owns its subtree exclusively. Subsequent in-place
        // merges (MergeFieldsInPlace below) must not leak field additions back into the source
        // QueryBuilder that supplied incomingField.
        var fieldToAdd = incomingField.DeepClone();
        if (!string.Equals(uniqueKey, originalFieldKey, StringComparison.OrdinalIgnoreCase))
        {
            fieldToAdd = fieldToAdd with { Alias = uniqueKey };
        }

        fields[uniqueKey] = fieldToAdd;
        mergeIndex.IndexAdd(uniqueKey, fieldToAdd);
        queryMap.SetMapping(queryName, uniqueKey);
    }

    private static (string Key, FieldDefinition Field)? FindMergeTarget(
        QueryDefinition targetDefinition,
        Dictionary<string, FieldDefinition> existingFields,
        FieldDefinition incomingField)
    {
        // Deep (subtree) fingerprint — strictly refines the old own-arguments-only fingerprint by
        // also folding in every descendant whose subtree carries an argument anywhere (see
        // FieldDefinitionExtensions.ComputeDeepFingerprint for the full conservatism proof). This is
        // what lets the production shape (a root field with zero arguments, whose only discriminator
        // is a filter several levels down) get bucketed correctly instead of every candidate
        // colliding into one fingerprint and falling through to a full CanMergeFields scan.
        var incomingFingerprint = FieldDefinitionExtensions.ComputeDeepFingerprint(incomingField);

        // Sub-bucketed by (Name, fingerprint): narrows the candidate set to just the handful of
        // existing fields that could actually match, instead of every field sharing incomingField's
        // Name (which is exactly what makes the production shape — N fragments, one shared root
        // Name, N distinct deep filters — quadratic under Name-only bucketing). The index
        // self-heals any entry whose live fingerprint has drifted since it was bucketed (see
        // FieldMergeIndex remarks), so this can never produce a false split: every candidate
        // returned here is confirmed, from its LIVE field, to share incomingFingerprint.
        var candidateKeys = targetDefinition.MergeIndex.GetMergeCandidatesByFingerprint(
            existingFields, incomingField.Name, incomingFingerprint);
        if (candidateKeys is null) return null;

        foreach (var key in candidateKeys)
        {
            if (!existingFields.TryGetValue(key, out var existingField))
                continue;

            if (existingField.IsNeverMerge)
                continue;

            // CanMergeFields remains the sole source of truth — the fingerprint match above is a
            // conservative pre-filter only. A match does NOT prove equality (a hash collision costs
            // one extra, unmodified CanMergeFields call here; it is never treated as sufficient on
            // its own).
            if (FieldDefinitionExtensions.CanMergeFields(existingField, incomingField))
                return (key, existingField);
        }

        return null;
    }

    private static FieldDefinition MarkAsNeverMerge(FieldDefinition field)
        => field.IsNeverMerge ? field : field with { IsNeverMerge = true };
}
