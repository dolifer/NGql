using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
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
            _ => throw new NotImplementedException($"Merging strategy {strategy} is not implemented")
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
        targetDefinition.Variables = new SortedSet<Variable>(targetDefinition.Variables.Union(incomingQuery.Variables));

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
            return MergingStrategy.NeverMerge;

        return rootStrategy switch
        {
            MergingStrategy.NeverMerge => MergingStrategy.NeverMerge,
            MergingStrategy.MergeByDefault => childStrategy,
            _ => rootStrategy
        };
    }

    private static MergeResult HandleMergeByDefault(SortedDictionary<string, FieldDefinition> existingFields, QueryDefinition incomingQuery)
    {
        var updatedFields = new SortedDictionary<string, FieldDefinition>(existingFields, StringComparer.OrdinalIgnoreCase);
        var queryMap = new Dictionary<string, string>();

        foreach (var (originalFieldKey, incomingField) in incomingQuery.Fields)
        {
            FieldBuilder.Include(updatedFields, incomingField);
            queryMap[incomingQuery.Name] = originalFieldKey;
        }

        return new MergeResult(queryMap, updatedFields);
    }

    private static MergeResult HandleNeverMerge(SortedDictionary<string, FieldDefinition> existingFields, QueryDefinition incomingQuery)
    {
        var updatedFields = new SortedDictionary<string, FieldDefinition>(existingFields, StringComparer.OrdinalIgnoreCase);
        var queryMap = new Dictionary<string, string>();

        foreach (var (originalFieldKey, incomingField) in incomingQuery.Fields)
        {
            var mappingTarget = !string.IsNullOrEmpty(incomingField.Alias) ? incomingField.Alias : incomingField.Name;
            var uniqueKey = GenerateUniqueKey(mappingTarget, updatedFields.Keys);

            // Create field with preserved original alias
            var fieldToAdd = uniqueKey != originalFieldKey
                ? incomingField with{ Alias = uniqueKey}
                : incomingField;

            updatedFields[uniqueKey] = fieldToAdd;

            // For NeverMerge, always map to the original field key (which should be the alias when present)
            queryMap[incomingQuery.Name] = uniqueKey;
        }

        return new MergeResult(queryMap, updatedFields);
    }

    private static MergeResult HandleMergeByFieldPath(SortedDictionary<string, FieldDefinition> existingFields, QueryDefinition incomingQuery)
    {
        var updatedFields = new SortedDictionary<string, FieldDefinition>(existingFields, StringComparer.OrdinalIgnoreCase);
        var queryMap = new Dictionary<string, string>();

        foreach (var (originalFieldKey, incomingField) in incomingQuery.Fields)
        {
            var mappingTarget = !string.IsNullOrEmpty(incomingField.Alias) ? incomingField.Alias : incomingField.Name;
            var mergeTarget = FindMergeTarget(updatedFields, incomingField);
            
            if (mergeTarget != null)
            {
                try
                {
                    var mergedField = FieldDefinitionExtensions.MergeFields(mergeTarget.Value.Field, incomingField);
                    updatedFields[mergeTarget.Value.Key] = mergedField;
                    // Map to the merge target key (where the field is actually stored)
                    queryMap[incomingQuery.Name] = mergeTarget.Value.Key;
                }
                catch (QueryMergeException ex)
                {
                    throw new QueryMergeException($"Cannot merge query '{incomingQuery.Name}' due to type conflicts in field '{incomingField.Name}'", ex);
                }
            }
            else
            {
                // No merge target found - preserve the original field key (alias)
                var uniqueKey = GenerateUniqueKey(mappingTarget, updatedFields.Keys);
                
                // Create field with preserved original alias
                var fieldToAdd = uniqueKey != originalFieldKey 
                    ? incomingField with{ Alias = uniqueKey }
                    : incomingField;

                updatedFields[uniqueKey] = fieldToAdd;
                queryMap[incomingQuery.Name] = uniqueKey;
            }
        }

        return new MergeResult(queryMap, updatedFields);
    }

    private static (string Key, FieldDefinition Field)? FindMergeTarget(SortedDictionary<string, FieldDefinition> existingFields, FieldDefinition incomingField)
    {
        foreach (var (key, existingField) in existingFields)
        {
            // First check if the root field names match
            if (!string.Equals(existingField.Name, incomingField.Name, StringComparison.OrdinalIgnoreCase))
                continue;

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
            return baseKey;

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
