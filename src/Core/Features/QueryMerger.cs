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
        if (incomingQuery._fields == null || incomingQuery._fields.Count == 0) return;

        MergeVariables(targetDefinition, incomingQuery);

        var beforeCount = targetDefinition.Fields.Count;
        ApplyFieldMerge(targetDefinition.Fields, incomingQuery, targetDefinition.MergingStrategy, queryMap);

        if (targetDefinition.Fields.Count != beforeCount)
        {
            queryMap.UpdateRootMapping(targetDefinition);
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

        foreach (var (originalFieldKey, incomingField) in incomingQuery.Fields)
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
