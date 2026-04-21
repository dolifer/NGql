using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text;
using NGql.Core.Abstractions;

namespace NGql.Core.Extensions;

[SuppressMessage("Minor Code Smell", "S3267:Loops should be simplified with \"LINQ\" expressions")]
internal static class Helpers
{
    internal static void ExtractVariablesFromValue(object? value, SortedSet<Variable> variables)
    {
        if (value == null)
        {
            return;
        }

        if (value is Variable variable)
        {
            variables.Add(variable);
            return;
        }

        if (value is IDictionary dict)
        {
            ExtractVariablesFromDictionary(dict, variables);
            return;
        }

        if (value is IList list)
        {
            ExtractVariablesFromList(list, variables);
            return;
        }

        if (ShouldExtractFromObjectProperties(value))
        {
            ExtractVariablesFromObjectProperties(value, variables);
        }
    }

    private static void ExtractVariablesFromDictionary(IDictionary dict, SortedSet<Variable> variables)
    {
        foreach (var val in dict.Values)
        {
            ExtractVariablesFromValue(val, variables);
        }
    }

    private static void ExtractVariablesFromList(IList list, SortedSet<Variable> variables)
    {
        foreach (var item in list)
        {
            ExtractVariablesFromValue(item, variables);
        }
    }

    private static bool ShouldExtractFromObjectProperties(object obj)
    {
        return obj is not string &&
               obj is not Variable &&
               obj is not QueryBlock &&
               obj is not IDictionary &&
               obj is not IList &&
               !ValueFormatter.IsPrimitiveType(obj);
    }

    private static void ExtractVariablesFromObjectProperties(object obj, SortedSet<Variable> variables)
    {
        var properties = obj.GetType().GetProperties();
        foreach (var property in properties)
        {
            var propertyValue = property.GetValue(obj);
            if (propertyValue != null)
            {
                ExtractVariablesFromValue(propertyValue, variables);
            }
        }
    }

    /// <summary>
    /// Generic dictionary merging with recursive support for nested dictionaries.
    /// </summary>
    private static TDict MergeDictionariesCore<TDict, TValue>(
        IDictionary<string, TValue>? existing,
        IDictionary<string, TValue> update,
        Func<TDict> createResult,
        Func<IDictionary<string, TValue>, IDictionary<string, TValue>, TValue> mergeNested)
        where TDict : IDictionary<string, TValue>
    {
        var result = createResult();

        // Add existing entries if present
        if (existing != null)
        {
            foreach (var (key, value) in existing)
            {
                result[key] = value;
            }
        }

        // Merge or add update entries
        foreach (var (key, updateValue) in update)
        {
            if (result.TryGetValue(key, out var existingValue) &&
                existingValue is IDictionary<string, TValue> existingDict &&
                updateValue is IDictionary<string, TValue> updateDict)
            {
                result[key] = mergeNested(existingDict, updateDict);
            }
            else
            {
                result[key] = updateValue;
            }
        }

        return result;
    }

    /// <summary>
    /// Merges two dictionaries with support for nullable values and recursive merging of nested dictionaries.
    /// This is the primary efficient implementation that avoids extra allocations.
    /// </summary>
    /// <param name="existing">The existing dictionary</param>
    /// <param name="update">The dictionary to merge in</param>
    /// <returns>A merged SortedDictionary with nullable values</returns>
    internal static SortedDictionary<string, object?> MergeNullableDictionaries(
        IDictionary<string, object?> existing,
        IDictionary<string, object?> update)
    {
        var result = new SortedDictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        
        // Copy existing entries
        foreach (var kvp in existing)
        {
            result[kvp.Key] = kvp.Value;
        }

        // Merge update entries with recursive handling for nested dictionaries
        foreach (var (key, newValue) in update)
        {
            if (result.TryGetValue(key, out var existingValue) &&
                existingValue is IDictionary<string, object?> existingDict && 
                newValue is IDictionary<string, object?> newDict)
            {
                // Recursively merge nested dictionaries
                result[key] = MergeNullableDictionaries(existingDict, newDict);
            }
            else
            {
                // Override with new value for non-dictionary values or new keys
                result[key] = newValue;
            }
        }

        return result;
    }

    /// <summary>
    /// Merges two non-nullable dictionaries by delegating to the core generic implementation.
    /// </summary>
    internal static SortedDictionary<string, object> MergeDictionaries(
        IDictionary<string, object> existing,
        IDictionary<string, object> update)
    {
        return MergeDictionariesCore<SortedDictionary<string, object>, object>(
            existing, 
            update,
            () => new SortedDictionary<string, object>(StringComparer.OrdinalIgnoreCase),
            (e, u) => MergeDictionaries(e, u));
    }

    /// <summary>
    /// Merges metadata dictionaries, handling nullable values appropriately with deep/recursive merging for nested dictionaries.
    /// </summary>
    /// <param name="existing">The existing metadata dictionary</param>
    /// <param name="update">The metadata dictionary to merge in</param>
    /// <returns>A merged Dictionary with nullable values suitable for metadata</returns>
    internal static Dictionary<string, object?> MergeMetadata(
        Dictionary<string, object?>? existing,
        Dictionary<string, object> update)
    {
        // ULTRA FAST PATH: If no existing metadata, just convert update to nullable with pre-sized dictionary
        if (existing is null || existing.Count == 0)
        {
            var converted = new Dictionary<string, object?>(update.Count, StringComparer.OrdinalIgnoreCase);
            foreach (var (key, value) in update)
            {
                converted[key] = value;
            }
            return converted;
        }

        // ULTRA FAST PATH: If no update metadata, return existing as-is
        if (update.Count == 0)
        {
            return existing;
        }

        // FAST PATH: Pre-size result dictionary to avoid resizing
        var result = new Dictionary<string, object?>(existing.Count + update.Count, StringComparer.OrdinalIgnoreCase);
        
        // Copy existing entries
        foreach (var (key, value) in existing)
        {
            result[key] = value;
        }

        // Merge update entries with deep merging for nested dictionaries
        foreach (var (key, newValue) in update)
        {
            if (result.TryGetValue(key, out var existingValue) && 
                existingValue is Dictionary<string, object?> existingDict && 
                newValue is Dictionary<string, object> newDict)
            {
                // Deep merge nested dictionaries
                result[key] = MergeMetadataDictionaries(existingDict, newDict);
            }
            else
            {
                // For non-dictionary values or when key doesn't exist, override with new value
                result[key] = newValue;
            }
        }

        return result;
    }

    /// <summary>
    /// Merges metadata dictionaries where both existing and update can have nullable values.
    /// </summary>
    /// <param name="existing">The existing metadata dictionary</param>
    /// <param name="update">The metadata dictionary to merge in</param>
    /// <returns>A merged Dictionary with nullable values suitable for metadata</returns>
    internal static Dictionary<string, object?> MergeNullableMetadata(
        Dictionary<string, object?>? existing,
        Dictionary<string, object?>? update)
    {
        // ULTRA FAST PATH: If no update metadata, return existing or empty
        if (update is null || update.Count == 0)
        {
            return existing ?? new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        }

        // ULTRA FAST PATH: If no existing metadata, return copy of update
        if (existing is null || existing.Count == 0)
        {
            return new Dictionary<string, object?>(update, StringComparer.OrdinalIgnoreCase);
        }

        // MERGE PATH: Both dictionaries have content
        var result = new Dictionary<string, object?>(existing.Count + update.Count, StringComparer.OrdinalIgnoreCase);
        
        // Copy existing entries
        foreach (var (key, value) in existing)
        {
            result[key] = value;
        }

        // Merge update entries with deep merging for nested dictionaries
        foreach (var (key, newValue) in update)
        {
            if (result.TryGetValue(key, out var existingValue) && 
                existingValue is Dictionary<string, object?> existingDict && 
                newValue is Dictionary<string, object?> newDict)
            {
                // Deep merge nested dictionaries - both nullable
                result[key] = MergeNullableMetadataDictionaries(existingDict, newDict);
            }
            else
            {
                // For non-dictionary values or when key doesn't exist, override with new value
                result[key] = newValue;
            }
        }

        return result;
    }

    /// <summary>
    /// Deep merges two metadata dictionaries recursively.
    /// </summary>
    /// <param name="existing">The existing dictionary</param>
    /// <param name="update">The dictionary to merge in</param>
    /// <returns>A merged dictionary with deep merging of nested dictionaries</returns>
    private static Dictionary<string, object?> MergeMetadataDictionaries(
        Dictionary<string, object?> existing,
        Dictionary<string, object> update)
    {
        var result = new Dictionary<string, object?>(existing.Count + update.Count, StringComparer.OrdinalIgnoreCase);
        
        // Copy existing entries
        foreach (var (key, value) in existing)
        {
            result[key] = value;
        }

        // Merge update entries with recursive merging for nested dictionaries
        foreach (var (key, newValue) in update)
        {
            if (result.TryGetValue(key, out var existingValue) && 
                existingValue is Dictionary<string, object?> existingNestedDict && 
                newValue is Dictionary<string, object> newNestedDict)
            {
                // Recursively merge nested dictionaries
                result[key] = MergeMetadataDictionaries(existingNestedDict, newNestedDict);
            }
            else
            {
                // Override with new value for non-dictionary values or when key doesn't exist
                result[key] = newValue;
            }
        }

        return result;
    }

    /// <summary>
    /// Deep merges two nullable metadata dictionaries recursively.
    /// </summary>
    /// <param name="existing">The existing dictionary</param>
    /// <param name="update">The dictionary to merge in</param>
    /// <returns>A merged dictionary with deep merging of nested dictionaries</returns>
    private static Dictionary<string, object?> MergeNullableMetadataDictionaries(
        Dictionary<string, object?> existing,
        Dictionary<string, object?> update)
    {
        var result = new Dictionary<string, object?>(existing.Count + update.Count, StringComparer.OrdinalIgnoreCase);
        
        // Copy existing entries
        foreach (var (key, value) in existing)
        {
            result[key] = value;
        }

        // Merge update entries with recursive merging for nested dictionaries
        foreach (var (key, newValue) in update)
        {
            if (result.TryGetValue(key, out var existingValue) && 
                existingValue is Dictionary<string, object?> existingNestedDict && 
                newValue is Dictionary<string, object?> newNestedDict)
            {
                // Recursively merge nested dictionaries - both nullable
                result[key] = MergeNullableMetadataDictionaries(existingNestedDict, newNestedDict);
            }
            else
            {
                // Override with new value for non-dictionary values or when key doesn't exist
                result[key] = newValue;
            }
        }

        return result;
    }

    internal static object? SortArgumentValue(object? value)
    {
        // Handle null values
        if (value is null)
        {
            return value;
        }

        // Check if it's a primitive type that can be formatted
        if (ValueFormatter.IsPrimitiveType(value))
        {
            return value;
        }

        return value switch
        {
            IDictionary<string, object?> dict => new SortedDictionary<string, object?>(
                dict.ToDictionary(
                    kvp => kvp.Key,
                    elementSelector: kvp => SortArgumentValue(kvp.Value),
                    comparer: StringComparer.OrdinalIgnoreCase),
                comparer: StringComparer.OrdinalIgnoreCase),

            IEnumerable<object> list when !value.GetType().IsArray => list.Select(SortArgumentValue).ToList(),
            Array arr => arr.Cast<object>().Select(SortArgumentValue).ToArray(),

            // Handle objects by decomposing them into dictionaries
            { } obj when obj is not string &&
                         obj is not Variable &&
                         obj is not QueryBlock &&
                         obj is not IDictionary &&
                         obj is not IList =>
                new SortedDictionary<string, object?>(
                    obj.GetType().GetProperties()
                        .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
                        .ToDictionary(
                            p => p.Name,
                            p => SortArgumentValue(p.GetValue(obj)),
                            comparer: StringComparer.OrdinalIgnoreCase),
                    comparer: StringComparer.OrdinalIgnoreCase),

            _ => value
        };
    }

    /// <summary>
    /// Compares two argument dictionaries for equality.
    /// </summary>
    /// <param name="args1">First argument dictionary</param>
    /// <param name="args2">Second argument dictionary</param>
    /// <returns>True if arguments are equal, false otherwise</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool AreArgumentsEqual(SortedDictionary<string, object?>? args1, SortedDictionary<string, object?>? args2)
    {
        // ULTRA FAST PATH: Reference equality
        if (ReferenceEquals(args1, args2)) return true;
        
        // FAST PATH: Handle null cases
        if (args1 == null && args2 == null) return true;
        if (args1 == null || args2 == null) return false;
        
        // FAST PATH: Count mismatch
        if (args1.Count != args2.Count) return false;
        
        // ULTRA FAST PATH: Both empty
        if (args1.Count == 0) return true;

        // Both must have the same number of arguments (including 0)
        foreach (var (key, value1) in args1)
        {
            if (!args2.TryGetValue(key, out var value2))
            {
                return false;
            }

            if (!AreValuesEqual(value1, value2))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Compares two values for equality, using optimized comparison strategies.
    /// </summary>
    /// <param name="value1">First value</param>
    /// <param name="value2">Second value</param>
    /// <returns>True if values are equal, false otherwise</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool AreValuesEqual(object? value1, object? value2)
    {
        // ULTRA FAST PATH: Reference equality (includes both null)
        if (ReferenceEquals(value1, value2)) return true;

        // FAST PATH: One is null, other isn't
        if (value1 == null || value2 == null) return false;

        // FAST PATH: Type mismatch
        var type1 = value1.GetType();
        var type2 = value2.GetType();
        if (type1 != type2) return false;

        // ULTRA FAST PATH: Value types and strings (most common case)
        if (type1.IsValueType || value1 is string)
        {
            return value1.Equals(value2);
        }

        // FAST PATH: Dictionaries
        if (value1 is IDictionary<string, object?> dict1 && value2 is IDictionary<string, object?> dict2)
        {
            return AreDictionariesEqual(dict1, dict2);
        }

        // FAST PATH: Collections
        if (value1 is IList list1 && value2 is IList list2)
        {
            return AreListsEqual(list1, list2);
        }

        // For complex objects, use structural comparison
        if (type1.IsClass)
        {
            return AreObjectsStructurallyEqual(value1, value2);
        }

        return value1.Equals(value2);
    }

    /// <summary>
    /// Optimized dictionary equality comparison
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool AreDictionariesEqual(IDictionary<string, object?> dict1, IDictionary<string, object?> dict2)
    {
        // FAST PATH: Count mismatch
        var count = dict1.Count;
        if (count != dict2.Count) return false;
        
        // ULTRA FAST PATH: Both empty
        if (count == 0) return true;

        foreach (var kvp in dict1)
        {
            if (!dict2.TryGetValue(kvp.Key, out var value2) || !AreValuesEqual(kvp.Value, value2))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Optimized list equality comparison
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool AreListsEqual(IList list1, IList list2)
    {
        // FAST PATH: Count mismatch
        var count = list1.Count;
        if (count != list2.Count) return false;
        
        // ULTRA FAST PATH: Both empty
        if (count == 0) return true;

        for (int i = 0; i < count; i++)
        {
            if (!AreValuesEqual(list1[i], list2[i]))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Structural equality comparison for complex objects using reflection
    /// </summary>
    private static bool AreObjectsStructurallyEqual(object obj1, object obj2)
    {
        var type = obj1.GetType();
        var properties = type.GetProperties();

        foreach (var property in properties)
        {
            var value1 = property.GetValue(obj1);
            var value2 = property.GetValue(obj2);

            if (!AreValuesEqual(value1, value2))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Parses field type from path if provided in format "Type fieldPath".
    /// </summary>
    /// <param name="fieldPath">Field path to parse</param>
    /// <param name="defaultType">Default type to use if none specified</param>
    /// <param name="type">Parsed type output</param>
    /// <returns>Field path with type removed and trimmed</returns>
    internal static ReadOnlySpan<char> ParseFieldTypeFromPath(ReadOnlySpan<char> fieldPath, ReadOnlySpan<char> defaultType, out ReadOnlySpan<char> type)
    {
        // FAST PATH: No space means no type annotation
        var spaceIndex = fieldPath.IndexOf(' ');
        if (spaceIndex <= 0)
        {
            type = defaultType;
            return fieldPath.TrimEndDotsAndSpaces();
        }

        var potentialType = fieldPath[..spaceIndex];
        // Type specification must be at beginning and:
        // 1. Start with letter or '['
        // 2. Not contain dots (to avoid treating field paths like "parent.." as types)
        // 3. Be a reasonable type name (contain letters/digits) OR be an array marker (just "[]")
        if (potentialType.Length > 0 &&
            (char.IsLetter(potentialType[0]) || potentialType[0] == '[') &&
            potentialType.IndexOf('.') == -1 &&
            (potentialType.HasLetterOrDigit() || potentialType.SequenceEqual("[]".AsSpan())))
        {
            type = potentialType;
            fieldPath = fieldPath[(spaceIndex + 1)..];
        }
        else
        {
            type = defaultType;
        }

        return fieldPath.TrimEndDotsAndSpaces();
    }

    /// <summary>
    /// Creates a new FieldDefinition with sorted arguments for consistent behavior.
    /// Arguments are passed by reference to avoid unnecessary copying of potentially large dictionaries.
    /// <param name="name">Field name</param>
    /// <param name="type">Field type</param>
    /// <param name="alias">Optional field alias</param>
    /// <param name="arguments">Field arguments (passed by reference for performance)</param>
    /// <param name="path">Field path for caching</param>
    /// <param name="metadata">Optional field metadata</param>
    /// <returns>New FieldDefinition instance</returns>
    /// </summary>
    internal static FieldDefinition CreateFieldDefinition(ReadOnlySpan<char> name, ReadOnlySpan<char> type, ReadOnlySpan<char> alias, SortedDictionary<string, object?>? arguments, ReadOnlySpan<char> path, Dictionary<string, object?>? metadata = null)
    {
        // Use type interning for memory efficiency
        var nameStr = name.ToString();
        var typeStr = Caching.TypeCache.GetInternedType(type);
        var aliasStr = alias.IsEmpty ? null : alias.ToString();
        var pathStr = path.ToString();

        // FAST PATH: Skip dictionary operations when arguments are empty or null
        if (arguments?.Count == 0 || arguments == null)
        {
            return new FieldDefinition(nameStr, typeStr, aliasStr, null)
            {
                Path = pathStr,
                _metadata = metadata
            };
        }

        // Create a new sorted dictionary to ensure consistent argument ordering
        var sortedArguments = new SortedDictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var kvp in arguments)
        {
            sortedArguments[kvp.Key] = SortArgumentValue(kvp.Value);
        }

        return new FieldDefinition(nameStr, typeStr, aliasStr, sortedArguments)
        {
            Path = pathStr,
            _metadata = metadata
        };
    }

    /// <summary>
    /// Finds an existing field in the collection by name, alias, or path.
    /// </summary>
    /// <param name="fields">Field collection to search</param>
    /// <param name="fieldDefinition">Field definition to find</param>
    /// <returns>Existing field if found, null otherwise</returns>
    internal static FieldDefinition? FindExistingField(Dictionary<string, FieldDefinition> fields, FieldDefinition fieldDefinition)
    {
        // Try to find field with same name and alias
        var existingField = fields.Values.FirstOrDefault(f =>
            f.Name == fieldDefinition.Name && f._alias == fieldDefinition._alias);

        return existingField ?? fields.GetValueOrDefault(fieldDefinition.Path);
    }

    /// <summary>
    /// Finds an existing field by traversing the field path.
    /// </summary>
    /// <param name="fields">The root fields collection</param>
    /// <param name="fieldPath">The field path to search for</param>
    /// <returns>The existing field if found, null otherwise</returns>
    internal static FieldDefinition? FindExistingFieldByPath(Dictionary<string, FieldDefinition> fields, ReadOnlySpan<char> fieldPath)
    {
        if (fieldPath.IsEmpty || fieldPath.IsWhiteSpace())
        {
            return null;
        }

        if (fieldPath.IsSimpleField())
        {
            return fields.GetValueOrDefault(fieldPath.ToString());
        }

        return TraverseFieldPath(fields, fieldPath);
    }

    private static FieldDefinition? TraverseFieldPath(Dictionary<string, FieldDefinition> fields, ReadOnlySpan<char> fieldPath)
    {
        var currentFields = fields;
        var remainingPath = fieldPath;
        FieldDefinition? lastField = null;

        while (!remainingPath.IsEmpty)
        {
            ExtractPathSegment(remainingPath, out var segment, out var nextPath);
            
            var currentField = FindFieldInCollection(currentFields, segment.Name);
            if (currentField == null)
            {
                return null;
            }

            lastField = currentField;
            currentFields = currentField.Fields;
            remainingPath = nextPath;
        }

        return lastField;
    }

    private static void ExtractPathSegment(ReadOnlySpan<char> path, out SpanSegment segment, out ReadOnlySpan<char> nextPath)
    {
        var dotIndex = path.IndexOf('.');
        var segmentSpan = dotIndex == -1 ? path : path[..dotIndex];
        nextPath = dotIndex == -1 ? ReadOnlySpan<char>.Empty : path[(dotIndex + 1)..];
        
        var fieldName = segmentSpan.ExtractFieldName();
        var isLastFragment = dotIndex == -1;
        
        segment = new SpanSegment(fieldName, ReadOnlySpan<char>.Empty, isLastFragment, ReadOnlySpan<char>.Empty);
    }

    private static FieldDefinition? FindFieldInCollection(Dictionary<string, FieldDefinition> fields, ReadOnlySpan<char> fieldName)
    {
        var fieldNameStr = fieldName.ToString();
        
        if (fields.TryGetValue(fieldNameStr, out var field))
        {
            return field;
        }

        return FindFieldWithTypeAnnotation(fields, fieldName);
    }

    private static FieldDefinition? FindFieldWithTypeAnnotation(Dictionary<string, FieldDefinition> fields, ReadOnlySpan<char> fieldName)
    {
        foreach (var field in fields.Values)
        {
            var nameSpan = field.Name.AsSpan();
            var spaceIndex = nameSpan.LastIndexOf(' ');
            var actualName = spaceIndex == -1 ? nameSpan : nameSpan[(spaceIndex + 1)..];
            
            if (actualName.Trim().SequenceEqual(fieldName))
            {
                return field;
            }
        }
        return null;
    }

    /// <summary>
    /// Writes a collection with specified prefix/suffix characters and custom item writer
    /// </summary>
    internal static void WriteCollection(char prefix, char suffix, IEnumerable list, StringBuilder builder, Action<StringBuilder, object?> itemWriter)
    {
        builder.Append(prefix);

        bool first = true;
        foreach (var obj in list)
        {
            if (!first)
            {
                builder.Append(", ");
            }

            first = false;
            itemWriter(builder, obj);
        }

        builder.Append(suffix);
    }
}
