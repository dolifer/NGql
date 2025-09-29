using System.Runtime.CompilerServices;
using NGql.Core.Abstractions;
using NGql.Core.Exceptions;
using NGql.Core.Features;

namespace NGql.Core.Extensions;

/// <summary>
/// Extension methods and utilities for FieldDefinition operations.
/// </summary>
internal static class FieldDefinitionExtensions
{
    /// <summary>
    /// Gets the effective name for the field, preferring an alias over name.
    /// This method is already optimized - no span version is needed as it returns existing strings without manipulation.
    /// </summary>
    /// <param name="field">The field definition</param>
    /// <returns>The alias if available, otherwise the field name</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static string GetEffectiveName(this FieldDefinition field)
        => !string.IsNullOrEmpty(field._alias) ? field._alias : field.Name;

    /// <summary>
    /// Determines if two fields can be merged based on their structure and arguments.
    /// </summary>
    /// <param name="existingField">The existing field definition</param>
    /// <param name="incomingField">The incoming field definition to merge</param>
    /// <returns>True if fields can be merged, false otherwise</returns>
    internal static bool CanMergeFields(FieldDefinition existingField, FieldDefinition incomingField)
    {
        // For MergeByFieldPath strategy, fields can merge if:
        // 1. They have the same root field name
        // 2. Arguments at each segment of a path must match exactly

        // First, check if arguments at the root level are compatible
        if (!Helpers.AreArgumentsEqual(existingField._arguments, incomingField._arguments))
        {
            return false;
        }

        // Then check if nested field structures are compatible
        return AreNestedFieldsCompatible(existingField, incomingField);
    }

    /// <summary>
    /// Checks if nested field structures are compatible for merging.
    /// </summary>
    /// <param name="existingField">The existing field definition</param>
    /// <param name="incomingField">The incoming field definition</param>
    /// <returns>True if nested fields are compatible, false otherwise</returns>
    private static bool AreNestedFieldsCompatible(FieldDefinition existingField, FieldDefinition incomingField)
    {
        // Rule: At each segment of a path, the arguments must match exactly
        // This means if one field has arguments at any level and the other doesn't have that path,
        // or has different arguments at the same path, they cannot merge.

        // Check all incoming nested fields
        foreach (var (incomingKey, incomingNestedField) in incomingField.Fields)
        {
            if (existingField.Fields.TryGetValue(incomingKey, out var existingNestedField))
            {
                // If the nested field exists in both, their arguments must match exactly
                if (!Helpers.AreArgumentsEqual(existingNestedField._arguments, incomingNestedField._arguments))
                {
                    return false;
                }

                // Recursively check deeper levels
                if (!AreNestedFieldsCompatible(existingNestedField, incomingNestedField))
                {
                    return false;
                }
            }
            else
            {
                // Incoming field has a path that doesn't exist in an existing field
                // If the incoming nested field (or any of its descendants) has arguments,
                // then these fields are incompatible because we can't match arguments
                // at the same path segments
                if (HasAnyArguments(incomingNestedField))
                {
                    return false;
                }
            }
        }

        // Check existing nested fields
        foreach (var (existingKey, existingNestedField) in existingField.Fields)
        {
            // Existing field has a path that doesn't exist in an incoming field
            if (!incomingField.Fields.ContainsKey(existingKey) && HasAnyArguments(existingNestedField))
            {
                // If the existing nested field (or any of its descendants) has arguments,
                // then these fields are incompatible
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Merges two field definitions into a single field definition.
    /// </summary>
    /// <param name="existing">The existing field definition</param>
    /// <param name="incoming">The incoming field definition to merge</param>
    /// <returns>A new merged field definition</returns>
    /// <exception cref="QueryMergeException">Thrown when there are type conflicts</exception>
    internal static FieldDefinition MergeFields(FieldDefinition existing, FieldDefinition incoming)
    {
        // Check for type conflicts - optimized with span comparison
        if (!string.IsNullOrEmpty(existing._type) && !string.IsNullOrEmpty(incoming._type) &&
            !existing._type.AsSpan().Equals(incoming._type.AsSpan(), StringComparison.OrdinalIgnoreCase))
        {
            throw new QueryMergeException($"Type conflict: existing field has type '{existing._type}', incoming field has type '{incoming._type}'");
        }

        // Create a merged field definition
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
                // Check for alias conflicts at a nested level
                var effectiveName = incomingNestedField.GetEffectiveName();

                // Check if any existing field has the same effective name
                var conflictingField = mergedFields.Values.FirstOrDefault(f =>
                    string.Equals(f.GetEffectiveName(), effectiveName, StringComparison.OrdinalIgnoreCase));

                if (conflictingField != null)
                {
                    // Generate unique alias to resolve conflict
                    var existingEffectiveNames = mergedFields.Values.Select(f => f.GetEffectiveName());
                    var uniqueAlias = KeyGenerator.GenerateUniqueKey(effectiveName, existingEffectiveNames);

                    var fieldToAdd = incomingNestedField with { Alias = uniqueAlias };
                    mergedFields[key] = fieldToAdd;
                }
                else
                {
                    mergedFields[key] = incomingNestedField;
                }
            }
        }

        return new FieldDefinition(
            existing.Name,
            existing._type ?? incoming._type ?? Constants.DefaultFieldType,
            existing._alias,
            existing.Arguments,
            mergedFields
        ).MergeFieldArguments(incoming.Arguments);
    }

    /// <summary>
    /// Checks if a field or any of its nested fields have arguments.
    /// </summary>
    /// <param name="field">The field definition to check</param>
    /// <returns>True if the field or any nested field has arguments, false otherwise</returns>
    private static bool HasAnyArguments(FieldDefinition field)
        => field.Arguments is { Count: > 0 } || field.Fields.Values.Any(HasAnyArguments);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static FieldDefinition MergeFieldArguments(this FieldDefinition existingField, SortedDictionary<string, object?>? newArguments)
    {
        // FAST PATH: If no new arguments to merge, return as-is
        if (newArguments is not { Count: > 0 })
        {
            return existingField;
        }

        // FAST PATH: If an existing field has no arguments, just set the new arguments
        if (existingField._arguments is null || existingField._arguments.Count == 0)
        {
            return existingField with { Arguments = newArguments };
        }

        // MERGE PATH: Create a completely new case-insensitive dictionary to ensure proper behavior
        var mergedArguments = new SortedDictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        
        // Copy existing arguments into a new case-insensitive dictionary
        foreach (var (key, value) in existingField._arguments)
        {
            mergedArguments[key] = value;
        }

        // Process new arguments - they will overwrite existing keys due to case-insensitive comparer
        foreach (var (key, newValue) in newArguments)
        {
            // Handle nested dictionary merging if needed
            if (mergedArguments.TryGetValue(key, out var existingValue) &&
                existingValue is IDictionary<string, object?> existingDict && 
                newValue is IDictionary<string, object?> newDict)
            {
                // Use efficient generic merge without extra allocations
                mergedArguments[key] = Helpers.MergeNullableDictionaries(existingDict, newDict);
                continue;
            }
            
            // For all other cases (new keys or non-dictionary values), overwrite with new value
            mergedArguments[key] = newValue;
        }

        return existingField with { Arguments = mergedArguments };
    }
}
