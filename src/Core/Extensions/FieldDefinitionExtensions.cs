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

        var incomingChildren = incomingField._children;
        var existingChildren = existingField._children;

        if (incomingChildren is { Count: > 0 })
        {
            var incomingSpan = incomingChildren.AsSpan();
            for (int i = 0; i < incomingSpan.Length; i++)
            {
                var incomingNestedField = incomingSpan[i];
                if (existingChildren?.TryGetValue(incomingNestedField.Name, out var existingNestedField) == true)
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
        }

        // Skip the existing-not-in-incoming check entirely when nothing in the existing subtree
        // could ever fail it. This was an O(N) scan per call; we early-out when no field in the
        // existing subtree carries arguments.
        if (existingChildren is { Count: > 0 } && SubtreeHasAnyArguments(existingField, depth))
        {
            var existingSpan = existingChildren.AsSpan();
            for (int i = 0; i < existingSpan.Length; i++)
            {
                var existingNestedField = existingSpan[i];
                if ((incomingChildren is null || incomingChildren.Find(existingNestedField.Name) == null)
                    && HasAnyArguments(existingNestedField, depth + 1))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static bool SubtreeHasAnyArguments(FieldDefinition field, int depth)
    {
        if (field._subtreeHasAnyArguments is { } cached) return cached;

        if (depth > MaxFieldDepth)
            throw new InvalidOperationException($"Field tree depth exceeds maximum allowed depth of {MaxFieldDepth}.");

        bool result;
        if (field._arguments is { Count: > 0 })
        {
            result = true;
        }
        else if (field._children is null || field._children.Count == 0)
        {
            result = false;
        }
        else
        {
            result = false;
            var span = field._children.AsSpan();
            for (int i = 0; i < span.Length; i++)
            {
                if (SubtreeHasAnyArguments(span[i], depth + 1))
                {
                    result = true;
                    break;
                }
            }
        }

        field._subtreeHasAnyArguments = result;
        return result;
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
    /// <summary>
    /// Merges <paramref name="incoming"/> INTO <paramref name="existing"/>, mutating its child collection
    /// and argument dictionary in place. The caller must own <paramref name="existing"/> exclusively
    /// (no external aliases) — used by <see cref="QueryMerger"/> when the result will overwrite the
    /// dictionary entry that holds <paramref name="existing"/>.
    /// </summary>
    /// <returns><paramref name="existing"/> after mutation.</returns>
    internal static FieldDefinition MergeFieldsInPlace(FieldDefinition existing, FieldDefinition incoming)
    {
        if (!string.IsNullOrEmpty(existing._type) && !string.IsNullOrEmpty(incoming._type) &&
            !existing._type.AsSpan().Equals(incoming._type.AsSpan(), StringComparison.OrdinalIgnoreCase))
        {
            throw new QueryMergeException($"Type conflict: existing field has type '{existing._type}', incoming field has type '{incoming._type}'");
        }

        var incomingChildren = incoming._children;
        if (incomingChildren is { Count: > 0 })
        {
            var existingChildren = existing._children ??= new FieldChildren();
            var span = incomingChildren.AsSpan();
            for (int i = 0; i < span.Length; i++)
            {
                MergeChildInPlace(existingChildren, span[i]);
            }
            // Subtree changed — invalidate the cached "has any arguments" flag on this node.
            existing._subtreeHasAnyArguments = null;
        }

        if (incoming._arguments is { Count: > 0 })
        {
            existing.MergeFieldArgumentsInPlace(incoming._arguments);
            existing._subtreeHasAnyArguments = null;
        }

        return existing;
    }

    private static void MergeChildInPlace(FieldChildren existingChildren, FieldDefinition incomingChild)
    {
        if (existingChildren.TryGetValue(incomingChild.Name, out var existingNested) && existingNested != null)
        {
            MergeFieldsInPlace(existingNested, incomingChild);
            return;
        }

        // Effective-name conflict can only occur when the incoming field carries an alias
        // distinct from its name; otherwise the Name lookup above would have caught it.
        if (!ReferenceEquals(incomingChild._effectiveName, incomingChild.Name) &&
            HasEffectiveNameConflict(existingChildren, incomingChild._effectiveName))
        {
            var uniqueAlias = KeyGenerator.GenerateUniqueKey(incomingChild._effectiveName, existingChildren.AsSpan());
            existingChildren.Append(incomingChild with { Alias = uniqueAlias });
        }
        else
        {
            existingChildren.Append(incomingChild);
        }
    }

    private static bool HasEffectiveNameConflict(FieldChildren children, string effectiveName)
    {
        foreach (var f in children.AsSpan())
        {
            if (string.Equals(f._effectiveName, effectiveName, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void MergeFieldArgumentsInPlace(this FieldDefinition existingField, IDictionary<string, object?> newArguments)
    {
        if (newArguments.Count == 0) return;

        if (existingField._arguments is null || existingField._arguments.Count == 0)
        {
            existingField._arguments = newArguments is SortedDictionary<string, object?> sd
                ? sd
                : new SortedDictionary<string, object?>(newArguments, StringComparer.OrdinalIgnoreCase);
            return;
        }

        var target = existingField._arguments;
        foreach (var (key, newValue) in newArguments)
        {
            if (target.TryGetValue(key, out var existingValue) &&
                existingValue is IDictionary<string, object?> existingDict &&
                newValue is IDictionary<string, object?> newDict)
            {
                target[key] = Helpers.MergeNullableDictionaries(existingDict, newDict);
                continue;
            }
            target[key] = newValue;
        }
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
