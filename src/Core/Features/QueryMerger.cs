using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using NGql.Core.Abstractions;
using NGql.Core.Builders;

namespace NGql.Core.Features;

/// <summary>
/// Result of a query merge operation
/// </summary>
/// <param name="QueryMap">Dictionary mapping original query names to their field paths</param>
/// <param name="UpdatedFields">Collection of field updates to apply</param>
internal record MergeResult(
    Dictionary<string, string> QueryMap,
    SortedDictionary<string, FieldDefinition> UpdatedFields
);

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
    /// <param name="strategy">Merging strategy to use</param>
    /// <returns>Merge result containing updated mappings and fields</returns>
    public static MergeResult MergeQuery(SortedDictionary<string, FieldDefinition> existingFields, QueryDefinition incomingQuery, MergingStrategy strategy)
    {
        return strategy switch
        {
            MergingStrategy.MergeByDefault => HandleMergeByDefault(existingFields, incomingQuery),
            MergingStrategy.NeverMerge => HandleNeverMerge(existingFields, incomingQuery),
            MergingStrategy.MergeByFieldPath => HandleMergeByFieldPath(existingFields, incomingQuery),
            _ => throw new NotImplementedException($"Merging strategy {strategy} is not implemented")
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
                    var mergedField = MergeFields(mergeTarget.Value.Field, incomingField);
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
            if (CanMergeFields(existingField, incomingField))
            {
                return (key, existingField);
            }
        }

        return null;
    }

    private static bool CanMergeFields(FieldDefinition existingField, FieldDefinition incomingField)
    {
        // For MergeByFieldPath strategy, fields can merge if:
        // 1. They have the same root field name
        // 2. Arguments at each segment of path must match exactly
        
        // First, check if arguments at the root level are compatible
        var existingArgs = NormalizeArguments(existingField.Arguments);
        var incomingArgs = NormalizeArguments(incomingField.Arguments);
        
        if (!AreArgumentsEqual(existingArgs, incomingArgs))
            return false;

        // Then check if nested field structures are compatible
        return AreNestedFieldsCompatible(existingField, incomingField);
    }

    private static bool AreNestedFieldsCompatible(FieldDefinition existingField, FieldDefinition incomingField)
    {
        // Rule: At each segment of path, the arguments must match exactly
        // This means if one field has arguments at any level and the other doesn't have that path,
        // or has different arguments at the same path, they cannot merge.
        
        // Check all incoming nested fields
        foreach (var (incomingKey, incomingNestedField) in incomingField.Fields)
        {
            if (existingField.Fields.TryGetValue(incomingKey, out var existingNestedField))
            {
                // If the nested field exists in both, their arguments must match exactly
                var existingNestedArgs = NormalizeArguments(existingNestedField.Arguments);
                var incomingNestedArgs = NormalizeArguments(incomingNestedField.Arguments);
                
                if (!AreArgumentsEqual(existingNestedArgs, incomingNestedArgs))
                    return false;

                // Recursively check deeper levels
                if (!AreNestedFieldsCompatible(existingNestedField, incomingNestedField))
                    return false;
            }
            else
            {
                // Incoming field has a path that doesn't exist in existing field
                // If the incoming nested field (or any of its descendants) has arguments,
                // then these fields are incompatible because we can't match arguments
                // at the same path segments
                if (HasAnyArguments(incomingNestedField))
                    return false;
            }
        }

        // Check existing nested fields
        foreach (var (existingKey, existingNestedField) in existingField.Fields)
        {
            if (!incomingField.Fields.ContainsKey(existingKey))
            {
                // Existing field has a path that doesn't exist in incoming field
                // If the existing nested field (or any of its descendants) has arguments,
                // then these fields are incompatible
                if (HasAnyArguments(existingNestedField))
                    return false;
            }
        }

        return true;
    }

    private static SortedDictionary<string, object?> NormalizeArguments(SortedDictionary<string, object?>? arguments)
        => arguments ?? new SortedDictionary<string, object?>();

    private static bool AreArgumentsEqual(SortedDictionary<string, object?> args1, SortedDictionary<string, object?> args2)
    {
        // For merging purposes, we need exact argument equality
        // Empty/null arguments should only match other empty/null arguments
        if (args1.Count != args2.Count)
            return false;

        // Both must have the same number of arguments (including 0)
        foreach (var (key, value1) in args1)
        {
            if (!args2.TryGetValue(key, out var value2))
                return false;

            if (!AreValuesEqual(value1, value2))
                return false;
        }

        return true;
    }

    private static bool AreValuesEqual(object? value1, object? value2)
    {
        if (ReferenceEquals(value1, value2))
            return true;

        if (value1 == null || value2 == null)
            return false;

        // Handle different types strictly - they should not be equal
        if (value1.GetType() != value2.GetType())
            return false;

        // For complex objects, serialize and compare with consistent ordering
        if (value1 is not string && value1.GetType().IsClass)
        {
            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.Never
            };
            
            var json1 = JsonSerializer.Serialize(value1, options);
            var json2 = JsonSerializer.Serialize(value2, options);
            return json1 == json2;
        }

        return value1.Equals(value2);
    }

    private static FieldDefinition MergeFields(FieldDefinition existing, FieldDefinition incoming)
    {
        // Check for type conflicts
        if (!string.IsNullOrEmpty(existing.Type) && !string.IsNullOrEmpty(incoming.Type) && existing.Type != incoming.Type)
        {
            throw new QueryMergeException($"Type conflict: existing field has type '{existing.Type}', incoming field has type '{incoming.Type}'");
        }

        // Create merged field definition
        var mergedFields = new SortedDictionary<string, FieldDefinition>(existing.Fields, StringComparer.OrdinalIgnoreCase);

        // Merge nested fields recursively
        foreach (var (key, incomingNestedField) in incoming.Fields)
        {
            if (mergedFields.TryGetValue(key, out var existingNestedField))
            {
                // Recursive merge for nested fields
                var mergedNestedField = MergeFields(existingNestedField, incomingNestedField);
                mergedFields[key] = mergedNestedField;
            }
            else
            {
                mergedFields[key] = incomingNestedField;
            }
        }

        return new FieldDefinition(
            existing.Name,
            existing.Type ?? incoming.Type ?? Constants.DefaultFieldType,
            existing.Alias,
            existing.Arguments ?? new SortedDictionary<string, object?>(),
            mergedFields
        );
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

    private static bool HasAnyArguments(FieldDefinition field)
    {
        // Check if the field or any of its nested fields have arguments
        if (field.Arguments != null && field.Arguments.Count > 0)
            return true;

        foreach (var nestedField in field.Fields.Values)
        {
            if (HasAnyArguments(nestedField))
                return true;
        }

        return false;
    }
}
