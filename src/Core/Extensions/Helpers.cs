using System;
using System.Collections;
using System.Runtime.CompilerServices;
using System.Text.Json;
using NGql.Core.Abstractions;

namespace NGql.Core.Extensions;

internal static class Helpers
{
    /// <summary>
    /// Parses field name and alias from a span for better performance
    /// </summary>
    internal static (string name, string? alias) GetFieldNameAndAliasSpan(ReadOnlySpan<char> fieldName)
    {
        // Match the original behavior exactly: split on ':' and only handle exactly 2 parts
        var fieldString = fieldName.ToString();
        var fieldNameParts = fieldString.Split(':', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var namePart = fieldNameParts.Length == 2 ? fieldNameParts[1] : fieldString;
        var alias = fieldNameParts.Length == 2 ? fieldNameParts[0] : null;

        return (namePart, alias);
    }

    /// <summary>
    /// Splits a path span into a list for better performance
    /// </summary>
    internal static List<string> SplitPathToListSpan(ReadOnlySpan<char> path)
    {
        if (path.IsEmpty)
            return new List<string>();

        // Pre-size list by counting dots + 1 to avoid reallocations
        var dotCount = 0;
        for (var i = 0; i < path.Length; i++)
        {
            if (path[i] == '.') dotCount++;
        }
        var result = new List<string>(dotCount + 1);
        
        var start = 0;
        for (var i = 0; i < path.Length; i++)
        {
            if (path[i] == '.')
            {
                if (i > start)
                {
                    result.Add(path[start..i].ToString());
                }
                start = i + 1;
            }
        }
        
        if (start < path.Length)
        {
            result.Add(path[start..].ToString());
        }
        
        return result;
    }

    /// <summary>
    /// Parses field type and name from a span for better performance
    /// </summary>
    internal static (string fieldType, string fieldName) ParseFieldTypeAndNameSpan(ReadOnlySpan<char> field)
    {
        var spaceIndex = field.IndexOf(' ');
        if (spaceIndex == -1)
        {
            return (Constants.DefaultFieldType, field.ToString());
        }

        var fieldType = field[..spaceIndex].ToString();
        var fieldName = field[(spaceIndex + 1)..].ToString();
        
        return (fieldType, fieldName);
    }

    /// <summary>
    /// Ref struct for span-based field name and alias parsing to avoid allocations
    /// </summary>
    internal ref struct FieldNameAliasPair
    {
        public ReadOnlySpan<char> Name;
        public ReadOnlySpan<char> Alias;
        public bool HasAlias;

        public FieldNameAliasPair(ReadOnlySpan<char> name, ReadOnlySpan<char> alias, bool hasAlias)
        {
            Name = name;
            Alias = alias;
            HasAlias = hasAlias;
        }
    }

    /// <summary>
    /// Parses field name and alias from a span returning spans for zero allocation
    /// </summary>
    internal static FieldNameAliasPair GetFieldNameAndAliasSpanOptimized(ReadOnlySpan<char> fieldName)
    {
        // Match original behavior: use string split to handle edge cases correctly
        var fieldString = fieldName.ToString();
        var fieldNameParts = fieldString.Split(':', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        
        if (fieldNameParts.Length == 2)
        {
            var alias = fieldNameParts[0].AsSpan();
            var name = fieldNameParts[1].AsSpan();
            return new FieldNameAliasPair(name, alias, true);
        }
        
        return new FieldNameAliasPair(fieldName, ReadOnlySpan<char>.Empty, false);
    }

    /// <summary>
    /// Creates a new SortedDictionary with case-insensitive string keys for field definitions.
    /// </summary>
    internal static SortedDictionary<string, FieldDefinition> CreateFieldDictionary()
        => new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Creates a new SortedDictionary with case-insensitive string keys for field definitions, initialized with existing data.
    /// </summary>
    internal static SortedDictionary<string, FieldDefinition> CreateFieldDictionary(IDictionary<string, FieldDefinition> source)
        => new(source, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Creates a new SortedDictionary with case-insensitive string keys for arguments.
    /// </summary>
    internal static SortedDictionary<string, object?> CreateArgumentDictionary()
        => new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Creates a new SortedDictionary with case-insensitive string keys for arguments, initialized with existing data.
    /// </summary>
    internal static SortedDictionary<string, object?> CreateArgumentDictionary(IDictionary<string, object?> source)
        => new(source, StringComparer.OrdinalIgnoreCase);

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

    internal static SortedDictionary<string, object> MergeDictionaries(
        IDictionary<string, object> existing,
        IDictionary<string, object> update)
    {
        var result = new SortedDictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        // First, add all existing entries
        foreach (var (key, value) in existing)
        {
            result[key] = value;
        }

        // Then merge or add update entries
        foreach (var (key, updateValue) in update)
        {
            if (result.TryGetValue(key, out var existingValue))
            {
                if (existingValue is IDictionary<string, object> existingDict &&
                    updateValue is IDictionary<string, object> updateDict)
                {
                    // Recursively merge nested dictionaries
                    result[key] = MergeDictionaries(existingDict, updateDict);
                }
                else
                {
                    // For non-dictionary values, update value overrides existing
                    result[key] = updateValue;
                }
            }
            else
            {
                result[key] = updateValue;
            }
        }

        return result;
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

        // SLOW PATH: Need to merge dictionaries
        var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        // First, add all existing entries
        foreach (var (key, value) in existing)
        {
            result[key] = value;
        }

        // Then merge or add update entries
        foreach (var (key, updateValue) in update)
        {
            if (result.TryGetValue(key, out var existingValue))
            {
                if (existingValue is Dictionary<string, object?> existingDict &&
                    updateValue is Dictionary<string, object> updateDict)
                {
                    // Recursively merge nested dictionaries
                    result[key] = MergeMetadata(existingDict, updateDict);
                }
                else
                {
                    // For non-dictionary values, update value overrides existing
                    result[key] = updateValue;
                }
            }
            else
            {
                result[key] = updateValue;
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
    internal static bool AreArgumentsEqual(SortedDictionary<string, object?> args1, SortedDictionary<string, object?> args2)
    {
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
            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.Never,
            };

            var json1 = JsonSerializer.Serialize(value1, options);
            var json2 = JsonSerializer.Serialize(value2, options);
            return json1 == json2;
        }

        return value1.Equals(value2);
    }

    internal static (string Name, string? Alias) GetFieldNameAndAlias(string field)
    {
        var fieldNameParts = field.Split(':', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var namePart = fieldNameParts.Length == 2 ? fieldNameParts[1] : field;
        var alias = fieldNameParts.Length == 2 ? fieldNameParts[0] : null;

        return (namePart, alias);
    }

    /// <summary>
    /// Efficiently splits a path string into a list using Span operations to avoid string allocations.
    /// </summary>
    /// <param name="pathSpan">Path to split as ReadOnlySpan</param>
    /// <returns>List of path segments</returns>
    internal static List<string> SplitPathToList(ReadOnlySpan<char> pathSpan)
    {
        var result = new List<string>();

        while (!pathSpan.IsEmpty)
        {
            var dotIndex = pathSpan.IndexOf('.');
            ReadOnlySpan<char> segment;

            if (dotIndex == -1)
            {
                // Last segment
                segment = pathSpan;
                pathSpan = ReadOnlySpan<char>.Empty;
            }
            else
            {
                // Extract current segment and advance
                segment = pathSpan[..dotIndex];
                pathSpan = pathSpan[(dotIndex + 1)..];
            }

            // Skip empty segments
            if (!segment.IsEmpty && !segment.IsWhiteSpace())
            {
                result.Add(segment.ToString());
            }
        }

        return result;
    }

    /// <summary>
    /// Parses field type from path if provided in format "Type fieldPath".
    /// </summary>
    /// <param name="fieldPath">Field path to parse</param>
    /// <param name="defaultType">Default type to use if none specified</param>
    /// <param name="type">Parsed type output</param>
    /// <returns>Field path with type removed and trimmed</returns>
    internal static ReadOnlySpan<char> ParseFieldTypeFromPath(ReadOnlySpan<char> fieldPath, string defaultType, out string type)
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
            (HasLetterOrDigit(potentialType) || potentialType.SequenceEqual("[]".AsSpan())))
        {
            type = potentialType.ToString();
            fieldPath = fieldPath[(spaceIndex + 1)..];
        }
        else
        {
            type = defaultType;
        }

        return fieldPath.TrimEnd(['.', ' ']);
    }

    private static bool HasLetterOrDigit(ReadOnlySpan<char> span)
    {
        foreach (var c in span)
        {
            if (char.IsLetterOrDigit(c))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Creates a new FieldDefinition with sorted arguments for consistent behavior.
    /// Arguments are passed by reference to avoid unnecessary copying of potentially large dictionaries.
    /// </summary>
    /// <param name="name">Field name</param>
    /// <param name="type">Field type</param>
    /// <param name="alias">Optional field alias</param>
    /// <param name="arguments">Field arguments (passed by reference for performance)</param>
    /// <param name="path">Field path for caching</param>
    /// <param name="metadata">Optional field metadata</param>
    /// <returns>New FieldDefinition instance</returns>
    internal static FieldDefinition CreateFieldDefinition(string name, string type, string? alias, in SortedDictionary<string, object?> arguments, string path, Dictionary<string, object?>? metadata = null)
    {
        // FAST PATH: Skip dictionary operations when arguments are empty
        if (arguments.Count == 0)
        {
            return new FieldDefinition(name, type, alias, Constants.EmptyArguments, null)
            {
                Path = path,
                Metadata = metadata
            };
        }

        // Create a new sorted dictionary to ensure consistent argument ordering
        var sortedArguments = new SortedDictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var kvp in arguments)
        {
            sortedArguments[kvp.Key] = SortArgumentValue(kvp.Value);
        }

        return new FieldDefinition(name, type, alias, sortedArguments, null)
        {
            Path = path,
            Metadata = metadata
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
            f.Name == fieldDefinition.Name && f.Alias == fieldDefinition.Alias);

        return existingField ?? fields.GetValueOrDefault(fieldDefinition.Path);
    }

    /// <summary>
    /// Extracts the root field name from a field path, handling aliases and dotted paths.
    /// </summary>
    /// <param name="fieldPath">The field path to parse</param>
    /// <returns>The root field name</returns>
    internal static string GetRootFieldName(string fieldPath)
    {
        if (string.IsNullOrWhiteSpace(fieldPath))
        {
            return string.Empty;
        }

        var parts = fieldPath.Split('.');
        if (parts.Length == 0)
        {
            return string.Empty;
        }

        var rootPart = parts[0].Split(':');
        return rootPart.Length > 0 ? rootPart[^1].Trim() : string.Empty;
    }

    /// <summary>
    /// Finds an existing field by traversing the field path.
    /// </summary>
    /// <param name="fields">The root fields collection</param>
    /// <param name="fieldPath">The field path to search for</param>
    /// <returns>The existing field if found, null otherwise</returns>
    internal static FieldDefinition? FindExistingFieldByPath(SortedDictionary<string, FieldDefinition> fields, string fieldPath)
    {
        if (string.IsNullOrWhiteSpace(fieldPath))
        {
            return null;
        }

        // FAST PATH: Simple field name without dots, spaces, or colons
        if (fieldPath.IndexOf('.') == -1 && fieldPath.IndexOf(' ') == -1 && fieldPath.IndexOf(':') == -1)
        {
            return fields.TryGetValue(fieldPath, out var simpleField) ? simpleField : null;
        }

        var pathSegments = fieldPath.Split('.');
        var currentFields = fields;
        FieldDefinition? currentField = null;

        foreach (var segment in pathSegments)
        {
            var fieldName = segment.Split(':').Last().Trim();

            // Try exact match first
            if (currentFields.ContainsKey(fieldName))
            {
                currentField = currentFields[fieldName];
            }
            // Try to find field with type annotation (e.g., "[] posts" when looking for "posts")
            else
            {
                currentField = currentFields.Values.FirstOrDefault(f =>
                    f.Name.Split(' ').Last().Trim() == fieldName);

                if (currentField == null)
                {
                    return null;
                }
            }

            currentFields = currentField.Fields;
        }

        return currentField;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static string NormalizeFieldName(string fieldName)
    {
        return fieldName.ToLowerInvariant();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static string NormalizeFieldName(ReadOnlySpan<char> fieldName)
    {
        return fieldName.ToString().ToLowerInvariant();
    }
}
