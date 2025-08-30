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
    /// Merges an incoming query definition into existing fields using the specified strategy
    /// </summary>
    /// <param name="existingFields">Current field definitions</param>
    /// <param name="incomingQuery">Query definition to merge</param>
    /// <param name="rootStrategy">Merging strategy to use</param>
    /// <returns>Merge result containing updated mappings and fields</returns>
    private static MergeResult MergeQuery(SortedDictionary<string, FieldDefinition> existingFields, QueryDefinition incomingQuery, MergingStrategy rootStrategy)
    {
        var strategy = GetEffectiveMergingStrategy(rootStrategy, incomingQuery.MergingStrategy);

        return strategy switch
        {
            MergingStrategy.MergeByDefault => HandleMergeByDefault(existingFields, incomingQuery),
            MergingStrategy.NeverMerge => HandleNeverMerge(existingFields, incomingQuery),
            MergingStrategy.MergeByFieldPath => HandleMergeByFieldPath(existingFields, incomingQuery),
            _ => throw new ArgumentOutOfRangeException(nameof(rootStrategy), $"Merging strategy {strategy} is not implemented"),
        };
    }

    /// <summary>
    /// Merges an incoming query definition into the target query definition, updating all related state.
    /// </summary>
    /// <param name="targetDefinition">The target query definition to merge into</param>
    /// <param name="queryMap">The query map to update with merge results</param>
    /// <param name="incomingQuery">Query definition to merge</param>
    public static void MergeQuery(QueryDefinition targetDefinition, QueryMap queryMap, in QueryDefinition incomingQuery)
    {
        // Merge variables
        targetDefinition.Variables = new SortedSet<Variable>((targetDefinition._variables ?? []).Union(incomingQuery._variables ?? []));

        // Perform the field merge
        var mergeResult = MergeQuery(targetDefinition.Fields, incomingQuery, targetDefinition.MergingStrategy);

        // Apply the merge results to fields
        targetDefinition.Fields.Clear();
        foreach (var (key, field) in mergeResult.UpdatedFields)
        {
            targetDefinition.Fields[key] = field;
        }

        // Update QueryMap with merge results (only for incoming query)
        queryMap.UpdateMappings(mergeResult.QueryMap);

        // Update root query mapping if fields changed
        queryMap.UpdateRootMapping(targetDefinition);
    }

    private static MergingStrategy GetEffectiveMergingStrategy(MergingStrategy rootStrategy, MergingStrategy childStrategy)
    {
        // Child NeverMerge always takes precedence
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

    private static MergeResult HandleMergeByDefault(SortedDictionary<string, FieldDefinition> existingFields, QueryDefinition incomingQuery)
    {
        return ProcessFieldMerge(existingFields, incomingQuery, (updatedFields, originalFieldKey, incomingField, queryMap) =>
        {
            FieldBuilder.Include(updatedFields, incomingField);
            queryMap[incomingQuery.Name] = originalFieldKey;
        });
    }

    private static MergeResult HandleNeverMerge(SortedDictionary<string, FieldDefinition> existingFields, QueryDefinition incomingQuery)
    {
        return ProcessFieldMerge(existingFields, incomingQuery, (updatedFields, originalFieldKey, incomingField, queryMap) =>
        {
            AddFieldWithUniqueKey(updatedFields, originalFieldKey, incomingField, queryMap, incomingQuery.Name);
        });
    }

    private static MergeResult HandleMergeByFieldPath(SortedDictionary<string, FieldDefinition> existingFields, QueryDefinition incomingQuery)
    {
        return ProcessFieldMerge(existingFields, incomingQuery, (updatedFields, originalFieldKey, incomingField, queryMap) =>
        {
            var mergeTarget = FindMergeTarget(updatedFields, incomingField);

            if (mergeTarget != null)
            {
                try
                {
                    var mergedField = FieldDefinitionExtensions.MergeFields(mergeTarget.Value.Field, incomingField);
                    updatedFields[mergeTarget.Value.Key] = mergedField;
                    queryMap[incomingQuery.Name] = mergeTarget.Value.Key;
                }
                catch (QueryMergeException ex)
                {
                    throw new QueryMergeException($"Cannot merge query '{incomingQuery.Name}' due to type conflicts in field '{incomingField.Name}'", ex);
                }
            }
            else
            {
                AddFieldWithUniqueKey(updatedFields, originalFieldKey, incomingField, queryMap, incomingQuery.Name);
            }
        });
    }

    private static MergeResult ProcessFieldMerge(
        SortedDictionary<string, FieldDefinition> existingFields, 
        QueryDefinition incomingQuery,
        Action<SortedDictionary<string, FieldDefinition>, string, FieldDefinition, Dictionary<string, string>> fieldProcessor)
    {
        var updatedFields = new SortedDictionary<string, FieldDefinition>(existingFields, StringComparer.OrdinalIgnoreCase);
        var queryMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (originalFieldKey, incomingField) in incomingQuery.Fields)
        {
            fieldProcessor(updatedFields, originalFieldKey, incomingField, queryMap);
        }

        return new MergeResult(queryMap, updatedFields);
    }

    private static void AddFieldWithUniqueKey(
        SortedDictionary<string, FieldDefinition> updatedFields,
        string originalFieldKey,
        FieldDefinition incomingField,
        Dictionary<string, string> queryMap,
        string queryName)
    {
        var mappingTarget = incomingField.GetEffectiveName();
        var uniqueKey = GenerateUniqueKey(mappingTarget, updatedFields.Keys);

        var fieldToAdd = !string.Equals(uniqueKey, originalFieldKey, StringComparison.OrdinalIgnoreCase)
            ? incomingField with { Alias = uniqueKey }
            : incomingField;

        updatedFields[uniqueKey] = fieldToAdd;
        queryMap[queryName] = uniqueKey;
    }

    private static (string Key, FieldDefinition Field)? FindMergeTarget(SortedDictionary<string, FieldDefinition> existingFields, FieldDefinition incomingField)
    {
        foreach (var (key, existingField) in existingFields)
        {
            // First check if the root field names match
            if (!string.Equals(existingField.Name, incomingField.Name, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            // Check if the field structures are compatible for merging
            if (FieldDefinitionExtensions.CanMergeFields(existingField, incomingField))
            {
                return (key, existingField);
            }
        }

        return null;
    }

    private static string GenerateUniqueKey(string baseKey, IEnumerable<string> existingKeys)
    {
        var existingKeySet = new HashSet<string>(existingKeys, StringComparer.OrdinalIgnoreCase);

        if (!existingKeySet.Contains(baseKey))
        {
            return baseKey;
        }

        var counter = 1;
        string uniqueKey;
        do
        {
            uniqueKey = $"{baseKey}_{counter}";
            counter++;
        }
        while (existingKeySet.Contains(uniqueKey));

        return uniqueKey;
    }
}
