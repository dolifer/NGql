using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using NGql.Core.Abstractions;

namespace NGql.Core.Extensions;

[SuppressMessage("Minor Code Smell", "S3267:Loops should be simplified with \"LINQ\" expressions")]
internal static class Helpers
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.Never,
    };

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
               !ValueFormatter.TryFormatPrimitiveType(obj, out _);
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
    /// Merges metadata dictionaries, handling nullable values appropriately.
    /// </summary>
    /// <param name="existing">The existing metadata dictionary</param>
    /// <param name="update">The metadata dictionary to merge in</param>
    /// <returns>A merged Dictionary with nullable values suitable for metadata</returns>
    internal static Dictionary<string, object?> MergeMetadata(
        Dictionary<string, object?>? existing,
        Dictionary<string, object> update)
    {
        // FAST PATH: If no existing metadata, just convert update to nullable
        if (existing is null || existing.Count == 0)
        {
            var converted = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (var (key, value) in update)
            {
                converted[key] = value;
            }
            return converted;
        }

        // FAST PATH: If no update metadata, return existing
        if (update.Count == 0)
        {
            return existing;
        }

        // Use generic merge logic
        return MergeDictionariesCore<Dictionary<string, object?>, object?>(
            existing,
            update.ToDictionary(kvp => kvp.Key, kvp => (object?)kvp.Value),
            () => new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase),
            (e, u) => MergeMetadata((Dictionary<string, object?>)e, u.ToDictionary(kvp => kvp.Key, kvp => kvp.Value!)));
    }

    internal static object? SortArgumentValue(object? value)
    {
        // Handle null values
        if (value is null)
        {
            return value;
        }

        // Check if it's a primitive type that can be formatted
        if (ValueFormatter.TryFormatPrimitiveType(value, out _))
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
    internal static bool AreArgumentsEqual(SortedDictionary<string, object?>? args1, SortedDictionary<string, object?>? args2)
    {
        // Handle null cases
        if (args1 == null && args2 == null) return true;
        if (args1 == null || args2 == null) return false;
        
        // For merging purposes, we need exact argument equality
        // Empty/null arguments should only match other empty/null arguments
        if (args1.Count != args2.Count)
        {
            return false;
        }

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
    /// Compares two values for equality, handling complex objects via JSON serialization.
    /// </summary>
    /// <param name="value1">First value</param>
    /// <param name="value2">Second value</param>
    /// <returns>True if values are equal, false otherwise</returns>
    private static bool AreValuesEqual(object? value1, object? value2)
    {
        if (ReferenceEquals(value1, value2))
        {
            return true;
        }

        if (value1 == null || value2 == null)
        {
            return false;
        }

        // Handle different types strictly - they should not be equal
        if (value1.GetType() != value2.GetType())
        {
            return false;
        }

        // For complex objects, serialize and compare with consistent ordering
        if (value1 is not string && value1.GetType().IsClass)
        {
            var json1 = JsonSerializer.Serialize(value1, JsonOptions);
            var json2 = JsonSerializer.Serialize(value2, JsonOptions);
            return json1 == json2;
        }

        return value1.Equals(value2);
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
            return fieldPath.TrimEnd(['.', ' ']);
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

        return fieldPath.TrimEnd(['.', ' ']);
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
        var nameStr = name.ToString();
        var typeStr = type.ToString();
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
    internal static FieldDefinition? FindExistingField(SortedDictionary<string, FieldDefinition> fields, FieldDefinition fieldDefinition)
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
    internal static FieldDefinition? FindExistingFieldByPath(SortedDictionary<string, FieldDefinition> fields, ReadOnlySpan<char> fieldPath)
    {
        if (fieldPath.IsEmpty || fieldPath.IsWhiteSpace())
        {
            return null;
        }

        // FAST PATH: Simple field name without dots, spaces, or colons
        if (fieldPath.IndexOf('.') == -1 && fieldPath.IndexOf(' ') == -1 && fieldPath.IndexOf(':') == -1)
        {
            return fields.GetValueOrDefault(fieldPath.ToString());
        }

        var currentFields = fields;
        FieldDefinition? currentField = null;
        var remainingPath = fieldPath;

        while (!remainingPath.IsEmpty)
        {
            var dotIndex = remainingPath.IndexOf('.');
            var segment = dotIndex == -1 ? remainingPath : remainingPath[..dotIndex];
            
            var colonIndex = segment.LastIndexOf(':');
            var fieldName = colonIndex == -1 ? segment : segment[(colonIndex + 1)..];
            fieldName = fieldName.Trim();

            var fieldNameStr = fieldName.ToString();

            // Try exact match first
            if (currentFields.ContainsKey(fieldNameStr))
            {
                currentField = currentFields[fieldNameStr];
            }
            // Try to find field with type annotation (e.g., "[] posts" when looking for "posts")
            else
            {
                currentField = null;
                foreach (var field in currentFields.Values)
                {
                    var nameSpan = field.Name.AsSpan();
                    var spaceIndex = nameSpan.LastIndexOf(' ');
                    var actualName = spaceIndex == -1 ? nameSpan : nameSpan[(spaceIndex + 1)..];
                    
                    if (actualName.Trim().SequenceEqual(fieldName))
                    {
                        currentField = field;
                        break;
                    }
                }

                if (currentField == null)
                {
                    return null;
                }
            }

            currentFields = currentField.Fields;
            remainingPath = dotIndex == -1 ? ReadOnlySpan<char>.Empty : remainingPath[(dotIndex + 1)..];
        }

        return currentField;
    }

    internal static FieldDefinition? FindExistingFieldByPath(SortedDictionary<string, FieldDefinition> fields, string fieldPath)
        => FindExistingFieldByPath(fields, fieldPath.AsSpan());
}
