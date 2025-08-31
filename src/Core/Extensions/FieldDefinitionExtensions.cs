using NGql.Core.Abstractions;
using NGql.Core.Exceptions;

namespace NGql.Core.Extensions;

/// <summary>
/// Extension methods and utilities for FieldDefinition operations.
/// </summary>
internal static class FieldDefinitionExtensions
{
    /// <summary>
    /// Gets the effective name for the field, preferring alias over name.
    /// </summary>
    /// <param name="field">The field definition</param>
    /// <returns>The alias if available, otherwise the field name</returns>
    internal static string GetEffectiveName(this FieldDefinition field)
        => !string.IsNullOrEmpty(field.Alias) ? field.Alias : field.Name;

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
        // 2. Arguments at each segment of path must match exactly

        // First, check if arguments at the root level are compatible
        var existingArgs = existingField._arguments ?? null;
        var incomingArgs = incomingField._arguments ?? null;

        if (!Helpers.AreArgumentsEqual(existingArgs, incomingArgs))
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
        // Rule: At each segment of path, the arguments must match exactly
        // This means if one field has arguments at any level and the other doesn't have that path,
        // or has different arguments at the same path, they cannot merge.

        // Check all incoming nested fields
        foreach (var (incomingKey, incomingNestedField) in incomingField.Fields)
        {
            if (existingField.Fields.TryGetValue(incomingKey, out var existingNestedField))
            {
                // If the nested field exists in both, their arguments must match exactly
                var existingNestedArgs = existingNestedField._arguments ?? null;
                var incomingNestedArgs = incomingNestedField._arguments ?? null;

                if (!Helpers.AreArgumentsEqual(existingNestedArgs, incomingNestedArgs))
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
                // Incoming field has a path that doesn't exist in existing field
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
            // Existing field has a path that doesn't exist in incoming field
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
        // Check for type conflicts
        if (!string.IsNullOrEmpty(existing.Type) && !string.IsNullOrEmpty(incoming.Type) && 
            !string.Equals(existing.Type, incoming.Type, StringComparison.OrdinalIgnoreCase))
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
            existing.Arguments,
            mergedFields
        ).MergeFieldArguments(incoming.Arguments ?? null);
    }

    /// <summary>
    /// Checks if a field or any of its nested fields have arguments.
    /// </summary>
    /// <param name="field">The field definition to check</param>
    /// <returns>True if the field or any nested field has arguments, false otherwise</returns>
    private static bool HasAnyArguments(FieldDefinition field)
        => field.Arguments is { Count: > 0 } || field.Fields.Values.Any(HasAnyArguments);

    internal static FieldDefinition MergeFieldArguments(this FieldDefinition existingField, SortedDictionary<string, object?>? newArguments)
    {
        // FAST PATH: If no new arguments to merge, return as-is
        if (newArguments is not { Count: > 0 })
        {
            return existingField;
        }

        // FAST PATH: If existing field has no arguments, just set the new arguments
        if (existingField._arguments is null || existingField._arguments.Count == 0)
        {
            return existingField with { Arguments = newArguments };
        }

        // SLOW PATH: Need to merge arguments
        var mergedArguments = Helpers.CreateArgumentDictionary(existingField._arguments);
        foreach (var (key, newValue) in newArguments)
        {
            if (!mergedArguments.TryGetValue(key, out var existingValue))
            {
                mergedArguments[key] = newValue;
                continue;
            }

            if (existingValue is IDictionary<string, object> existingDict && newValue is IDictionary<string, object> newDict)
            {
                // Merge nested dictionaries
                mergedArguments[key] = Helpers.MergeDictionaries(existingDict, newDict);
                continue;
            }

            // For non-dictionary values, the new value overrides existing
            mergedArguments[key] = newValue;
        }

        return existingField with { Arguments = mergedArguments };
    }
}
