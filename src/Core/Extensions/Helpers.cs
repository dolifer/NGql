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
        ExtractVariablesFromValueCore(value, variables, null);
    }

    private static void ExtractVariablesFromValueCore(object? value, SortedSet<Variable> variables, HashSet<object>? visited)
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
            ExtractVariablesFromDictionary(dict, variables, visited);
            return;
        }

        if (value is IList list)
        {
            ExtractVariablesFromList(list, variables, visited);
            return;
        }

        if (ShouldExtractFromObjectProperties(value))
        {
            ExtractVariablesFromObjectProperties(value, variables, visited);
        }
    }

    private static void ExtractVariablesFromDictionary(IDictionary dict, SortedSet<Variable> variables, HashSet<object>? visited)
    {
        visited ??= new HashSet<object>(ReferenceEqualityComparer.Instance);
        if (!visited.Add(dict)) return; // cycle detected
        
        foreach (var val in dict.Values)
        {
            ExtractVariablesFromValueCore(val, variables, visited);
        }
    }

    private static void ExtractVariablesFromList(IList list, SortedSet<Variable> variables, HashSet<object>? visited)
    {
        visited ??= new HashSet<object>(ReferenceEqualityComparer.Instance);
        if (!visited.Add(list)) return; // cycle detected
        
        foreach (var item in list)
        {
            ExtractVariablesFromValueCore(item, variables, visited);
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

    private static void ExtractVariablesFromObjectProperties(object obj, SortedSet<Variable> variables, HashSet<object>? visited)
    {
        visited ??= new HashSet<object>(ReferenceEqualityComparer.Instance);
        if (!visited.Add(obj)) return; // cycle detected
        var properties = obj.GetType().GetProperties();
        foreach (var property in properties)
        {
            var propertyValue = property.GetValue(obj);
            if (propertyValue != null)
            {
                ExtractVariablesFromValueCore(propertyValue, variables, visited);
            }
        }
    }

    /// <summary>
    /// Generic dictionary merging with recursive support for nested dictionaries.
    /// </summary>
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
    /// Merges metadata dictionaries, handling nullable values appropriately with deep/recursive merging for nested dictionaries.
    /// </summary>
    /// <param name="existing">The existing metadata dictionary</param>
    /// <param name="update">The metadata dictionary to merge in</param>
    /// <returns>A merged Dictionary with nullable values suitable for metadata</returns>
    internal static Dictionary<string, object?> MergeMetadata(
        Dictionary<string, object?>? existing,
        Dictionary<string, object> update)
    {
        if (existing is null || existing.Count == 0) return ConvertToNullable(update);
        if (update.Count == 0) return existing;

        var result = new Dictionary<string, object?>(existing.Count + update.Count, StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in existing) result[key] = value;
        foreach (var (key, newValue) in update)
        {
            result[key] = MergedMetadataValue(result, key, newValue);
        }

        return result;
    }

    private static Dictionary<string, object?> ConvertToNullable(Dictionary<string, object> source)
    {
        var converted = new Dictionary<string, object?>(source.Count, StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in source) converted[key] = value;
        return converted;
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
        if (update is null || update.Count == 0)
            return existing ?? new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        if (existing is null || existing.Count == 0)
            return new Dictionary<string, object?>(update, StringComparer.OrdinalIgnoreCase);

        var result = new Dictionary<string, object?>(existing.Count + update.Count, StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in existing) result[key] = value;
        foreach (var (key, newValue) in update)
        {
            result[key] = MergedNullableMetadataValue(result, key, newValue);
        }
        return result;
    }

    private static object? MergedNullableMetadataValue(Dictionary<string, object?> target, string key, object? newValue)
    {
        if (target.TryGetValue(key, out var existingValue)
            && existingValue is Dictionary<string, object?> existingDict
            && newValue is Dictionary<string, object?> newDict)
        {
            return MergeNullableMetadataDictionaries(existingDict, newDict);
        }
        return newValue;
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
        foreach (var (key, value) in existing) result[key] = value;
        foreach (var (key, newValue) in update)
        {
            result[key] = MergedMetadataValue(result, key, newValue);
        }
        return result;
    }

    private static object? MergedMetadataValue(Dictionary<string, object?> target, string key, object newValue)
    {
        if (target.TryGetValue(key, out var existingValue)
            && existingValue is Dictionary<string, object?> existingNested
            && newValue is Dictionary<string, object> newNested)
        {
            return MergeMetadataDictionaries(existingNested, newNested);
        }
        return newValue;
    }

    private static Dictionary<string, object?> MergeNullableMetadataDictionaries(
        Dictionary<string, object?> existing,
        Dictionary<string, object?> update)
    {
        var result = new Dictionary<string, object?>(existing.Count + update.Count, StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in existing) result[key] = value;
        foreach (var (key, newValue) in update)
        {
            result[key] = MergedNullableMetadataValue(result, key, newValue);
        }
        return result;
    }

    internal static object? SortArgumentValue(object? value)
    {
        if (value is null) return null;
        if (ValueFormatter.IsPrimitiveType(value)) return value;
        return SortNonPrimitive(value);
    }

    private static object? SortNonPrimitive(object value) => value switch
    {
        IDictionary<string, object?> dict => SortDictionary(dict),
        Array arr => arr.Cast<object>().Select(SortArgumentValue).ToArray(),
        IEnumerable<object> list when !value.GetType().IsArray => SortListItems(list),
        _ => IsDecomposable(value) ? DecomposeToDictionary(value) : value,
    };

    private static SortedDictionary<string, object?> SortDictionary(IDictionary<string, object?> dict)
        => new(dict.ToDictionary(kvp => kvp.Key,
                                  elementSelector: kvp => SortArgumentValue(kvp.Value),
                                  comparer: StringComparer.OrdinalIgnoreCase),
               comparer: StringComparer.OrdinalIgnoreCase);

    /// <summary>True for arbitrary CLR objects whose properties should be reflected into a
    /// sorted dictionary. Excludes types we already format/serialize specially.</summary>
    private static bool IsDecomposable(object obj)
        => obj is not string and not Variable and not QueryBlock and not IDictionary and not IList;

    private static SortedDictionary<string, object?> DecomposeToDictionary(object obj)
        => new(obj.GetType().GetProperties()
                  .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
                  .ToDictionary(p => p.Name,
                                p => SortArgumentValue(p.GetValue(obj)),
                                comparer: StringComparer.OrdinalIgnoreCase),
               comparer: StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Efficiently sorts list items in a single pass without double allocations.
    /// Avoids: .Select().ToList() which creates intermediate IEnumerable + List allocations.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static List<object?> SortListItems(IEnumerable<object> list)
    {
        var result = new List<object?>();
        foreach (var item in list)
        {
            result.Add(SortArgumentValue(item));
        }
        return result;
    }

    /// <summary>
    /// Compares two argument dictionaries for equality.
    /// </summary>
    /// <param name="args1">First argument dictionary</param>
    /// <param name="args2">Second argument dictionary</param>
    /// <returns>True if arguments are equal, false otherwise</returns>
    internal static bool AreArgumentsEqual(SortedDictionary<string, object?>? args1, SortedDictionary<string, object?>? args2)
    {
        var status = CompareArgumentShapes(args1, args2);
        return status switch
        {
            ArgumentShape.Equal => true,
            ArgumentShape.Mismatch => false,
            _ => ArgumentEntriesMatch(args1!, args2!),
        };
    }

    private enum ArgumentShape { Equal, Mismatch, NeedsEntryComparison }

    private static ArgumentShape CompareArgumentShapes(SortedDictionary<string, object?>? a, SortedDictionary<string, object?>? b)
    {
        if (ReferenceEquals(a, b)) return ArgumentShape.Equal;
        if (a is null || b is null) return ArgumentShape.Mismatch;
        if (a.Count != b.Count) return ArgumentShape.Mismatch;
        if (a.Count == 0) return ArgumentShape.Equal;
        return ArgumentShape.NeedsEntryComparison;
    }

    private static bool ArgumentEntriesMatch(SortedDictionary<string, object?> a, SortedDictionary<string, object?> b)
    {
        foreach (var (key, value1) in a)
        {
            if (!b.TryGetValue(key, out var value2)) return false;
            if (!AreValuesEqual(value1, value2)) return false;
        }
        return true;
    }

    /// <summary>
    /// Compares two values for equality, using optimized comparison strategies.
    /// </summary>
    /// <param name="value1">First value</param>
    /// <param name="value2">Second value</param>
    /// <returns>True if values are equal, false otherwise</returns>
    private static bool AreValuesEqual(object? value1, object? value2)
    {
        if (ReferenceEquals(value1, value2)) return true;
        if (value1 is null || value2 is null) return false;

        var type1 = value1.GetType();
        if (type1 != value2.GetType()) return false;

        if (type1.IsValueType || value1 is string) return value1.Equals(value2);

        return AreReferenceTypedValuesEqual(value1, value2);
    }

    private static bool AreReferenceTypedValuesEqual(object value1, object value2)
    {
        if (value1 is IDictionary<string, object?> dict1 && value2 is IDictionary<string, object?> dict2)
            return AreDictionariesEqual(dict1, dict2);

        if (value1 is IList list1 && value2 is IList list2)
            return AreListsEqual(list1, list2);

        return AreObjectsStructurallyEqual(value1, value2);
    }

    /// <summary>
    /// Optimized dictionary equality comparison
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool AreDictionariesEqual(IDictionary<string, object?> dict1, IDictionary<string, object?> dict2)
    {
        if (dict1.Count != dict2.Count) return false;
        foreach (var kvp in dict1)
        {
            if (!dict2.TryGetValue(kvp.Key, out var value2)) return false;
            if (!AreValuesEqual(kvp.Value, value2)) return false;
        }
        return true;
    }

    /// <summary>
    /// Optimized list equality comparison
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool AreListsEqual(IList list1, IList list2)
    {
        var count = list1.Count;
        if (count != list2.Count) return false;
        for (int i = 0; i < count; i++)
        {
            if (!AreValuesEqual(list1[i], list2[i])) return false;
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
        var spaceIndex = fieldPath.IndexOf(' ');
        if (spaceIndex <= 0)
        {
            type = defaultType;
            return fieldPath.TrimEndDotsAndSpaces();
        }

        var potentialType = fieldPath[..spaceIndex];
        if (LooksLikeTypeAnnotation(potentialType))
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

    // Type annotation must start with a letter or '[', contain no dots, and either include a
    // letter/digit OR be the bare "[]" array marker.
    private static bool LooksLikeTypeAnnotation(ReadOnlySpan<char> candidate)
        => candidate.Length > 0
        && (char.IsLetter(candidate[0]) || candidate[0] == '[')
        && candidate.IndexOf('.') < 0
        && (candidate.HasLetterOrDigit() || candidate.SequenceEqual("[]".AsSpan()));

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
    internal static FieldDefinition CreateFieldDefinition(ReadOnlySpan<char> name, ReadOnlySpan<char> type, ReadOnlySpan<char> alias, IDictionary<string, object?>? arguments, ReadOnlySpan<char> path, Dictionary<string, object?>? metadata = null)
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
        FieldDefinition? existingField = null;
        foreach (var f in fields.Values)
        {
            if (f.Name == fieldDefinition.Name && f._alias == fieldDefinition._alias)
            {
                existingField = f;
                break;
            }
        }
        return existingField ?? fields.GetValueOrDefault(fieldDefinition.Path);
    }

    internal static FieldDefinition? FindExistingField(FieldChildren children, FieldDefinition fieldDefinition)
    {
        foreach (var f in children.AsSpan())
        {
            if (f.Name == fieldDefinition.Name && f._alias == fieldDefinition._alias)
                return f;
        }
        return children.Find(fieldDefinition.Path.AsSpan());
    }

    /// <summary>
    /// Validates that a field name conforms to GraphQL identifier rules: [_A-Za-z][_0-9A-Za-z]*
    /// Handles type-annotation prefixes (e.g. "[User!]! user" → validates "user") and
    /// alias prefixes (e.g. "alias:name" → validates both "alias" and "name").
    /// For dotted paths, validate each segment individually before calling this method.
    /// </summary>
    internal static void ValidateFieldName(ReadOnlySpan<char> name)
    {
        if (name.IsEmpty)
            throw new ArgumentException("Field name cannot be empty.");

        // Strip type annotation prefix: take identifier part after the last space
        var spaceIndex = name.LastIndexOf(' ');
        var identifier = spaceIndex >= 0 ? name[(spaceIndex + 1)..].Trim() : name.Trim();

        // Handle alias:name syntax
        var colonIndex = identifier.IndexOf(':');
        if (colonIndex >= 0)
        {
            var alias = identifier[..colonIndex].Trim();
            var fieldName = identifier[(colonIndex + 1)..].Trim();
            if (!alias.IsEmpty) ValidateIdentifier(alias);
            if (!fieldName.IsEmpty) ValidateIdentifier(fieldName);
            return;
        }

        ValidateIdentifier(identifier);
    }

    private static void ValidateIdentifier(ReadOnlySpan<char> name)
    {
        if (name.IsEmpty)
            throw new ArgumentException("Field name cannot be empty.");

        if (!IsValidGraphQlNameStart(name[0]))
            throw new ArgumentException($"Invalid GraphQL field name '{name.ToString()}': must start with a letter or underscore.");

        for (int i = 1; i < name.Length; i++)
        {
            if (!IsValidGraphQlNameChar(name[i]))
                throw new ArgumentException($"Invalid GraphQL field name '{name.ToString()}': contains invalid character '{name[i]}' at position {i}.");
        }
    }

    private static bool IsValidGraphQlNameStart(char c)
        => c == '_' || (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z');

    private static bool IsValidGraphQlNameChar(char c)
        => c == '_' || (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9');

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
