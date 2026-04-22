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
    private const int MaxFieldDepth = 200;

    /// <summary>
    /// Determines if two fields can be merged based on their structure and arguments.
    /// </summary>
    /// <param name="existingField">The existing field definition</param>
    /// <param name="incomingField">The incoming field definition to merge</param>
    /// <returns>True if fields can be merged, false otherwise</returns>
    internal static bool CanMergeFields(FieldDefinition existingField, FieldDefinition incomingField)
    {
        if (!Helpers.AreArgumentsEqual(existingField._arguments, incomingField._arguments))
            return false;
        return AreNestedFieldsCompatible(existingField, incomingField, 0);
    }

    private static bool AreNestedFieldsCompatible(FieldDefinition existingField, FieldDefinition incomingField, int depth)
    {
        if (depth > MaxFieldDepth)
            throw new InvalidOperationException($"Field tree depth exceeds maximum allowed depth of {MaxFieldDepth}. Possible circular reference in field definitions.");

        foreach (var (incomingKey, incomingNestedField) in incomingField._children ?? (IEnumerable<KeyValuePair<string, FieldDefinition>>)[])
        {
            if (existingField._children?.TryGetValue(incomingKey, out var existingNestedField) == true)
            {
                if (!Helpers.AreArgumentsEqual(existingNestedField._arguments, incomingNestedField._arguments))
                    return false;
                if (!AreNestedFieldsCompatible(existingNestedField, incomingNestedField, depth + 1))
                    return false;
            }
            else
            {
                if (HasAnyArguments(incomingNestedField, depth + 1))
                    return false;
            }
        }

        foreach (var (existingKey, existingNestedField) in existingField._children ?? (IEnumerable<KeyValuePair<string, FieldDefinition>>)[])
        {
            if (incomingField._children?.Find(existingKey.AsSpan()) == null && HasAnyArguments(existingNestedField, depth + 1))
                return false;
        }

        return true;
    }

    private static bool HasAnyArguments(FieldDefinition field, int depth)
    {
        if (depth > MaxFieldDepth)
            throw new InvalidOperationException($"Field tree depth exceeds maximum allowed depth of {MaxFieldDepth}.");
        if (field._arguments is { Count: > 0 }) return true;
        if (field._children is null) return false;
        foreach (var child in field._children.AsSpan())
        {
            if (HasAnyArguments(child, depth + 1)) return true;
        }
        return false;
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
        // Create a merged FieldChildren — clone existing then merge incoming on top
        var mergedChildren = existing._children?.Clone() ?? new FieldChildren();

        // Merge nested fields recursively
        var incomingSpan = incoming._children != null ? incoming._children.AsSpan() : ReadOnlySpan<FieldDefinition>.Empty;
        foreach (var incomingNestedField in incomingSpan)
        {
            if (mergedChildren.TryGetValue(incomingNestedField.Name, out var existingNestedField))
            {
                // Recursive merge for nested fields
                var mergedNestedField = MergeFields(existingNestedField!, incomingNestedField);
                mergedChildren.Set(incomingNestedField.Name, mergedNestedField);
            }
            else
            {
                // Check if any existing field has the same effective name
                FieldDefinition? conflictingField = null;
                foreach (var f in mergedChildren.AsSpan())
                {
                    if (string.Equals(f._effectiveName, incomingNestedField._effectiveName, StringComparison.OrdinalIgnoreCase))
                    {
                        conflictingField = f;
                        break;
                    }
                }

                if (conflictingField != null)
                {
                    // Generate unique alias to resolve conflict
                    var existingEffectiveNames = mergedChildren.AsSpan().ToArray().Select(f => f._effectiveName);
                    var uniqueAlias = KeyGenerator.GenerateUniqueKey(incomingNestedField._effectiveName, existingEffectiveNames);

                    var fieldToAdd = incomingNestedField with { Alias = uniqueAlias };
                    mergedChildren.Append(fieldToAdd);
                }
                else
                {
                    mergedChildren.Append(incomingNestedField);
                }
            }
        }

        var result = new FieldDefinition(
            existing.Name,
            existing._type ?? incoming._type ?? Constants.DefaultFieldType,
            existing._alias,
            existing._arguments)
        {
            _children = mergedChildren
        };
        return result.MergeFieldArguments(incoming._arguments);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static FieldDefinition MergeFieldArguments(this FieldDefinition existingField, IDictionary<string, object?>? newArguments)
    {
        // FAST PATH: If no new arguments to merge, return as-is
        if (newArguments is not { Count: > 0 })
        {
            return existingField;
        }

        // FAST PATH: If an existing field has no arguments, just set the new arguments
        if (existingField._arguments is null || existingField._arguments.Count == 0)
        {
            var sortedNew = newArguments is SortedDictionary<string, object?> sd
                ? sd
                : new SortedDictionary<string, object?>(newArguments, StringComparer.OrdinalIgnoreCase);
            return existingField with { _arguments = sortedNew };
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

        return existingField with { _arguments = mergedArguments };
    }
}
