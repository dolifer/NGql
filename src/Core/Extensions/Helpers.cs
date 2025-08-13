using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using NGql.Core.Abstractions;

namespace NGql.Core.Extensions;

internal static class Helpers
{
    internal static void ExtractVariablesFromValue(object value, SortedSet<Variable> variables)
    {
        switch (value)
        {
            case Variable variable:
                variables.Add(variable);
                break;
            case IDictionary dict:
            {
                foreach (var val in dict.Values)
                {
                    ExtractVariablesFromValue(val, variables);
                }

                break;
            }
            case IList list:
            {
                foreach (var item in list)
                {
                    ExtractVariablesFromValue(item, variables);
                }

                break;
            }
            case { } obj when obj is not string &&
                              obj is not Variable &&
                              obj is not QueryBlock &&
                              obj is not IDictionary &&
                              obj is not IList &&
                              !ValueFormatter.TryFormatPrimitiveType(obj, out _):
            {
                // Extract variables from object properties using reflection
                var properties = obj.GetType().GetProperties();
                foreach (var property in properties)
                {
                    var propertyValue = property.GetValue(obj);
                    if (propertyValue != null)
                    {
                        ExtractVariablesFromValue(propertyValue, variables);
                    }
                }
                break;
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
        var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        // First, add all existing entries
        if (existing != null)
        {
            foreach (var (key, value) in existing)
            {
                result[key] = value;
            }
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
            return value;

        // Check if it's a primitive type that can be formatted
        if (ValueFormatter.TryFormatPrimitiveType(value, out _))
            return value;
        
        return value switch
        {
            IDictionary<string, object?> dict => new SortedDictionary<string, object?>(
                dict.ToDictionary(
                    kvp => kvp.Key,
                    kvp => SortArgumentValue(kvp.Value)
                ), StringComparer.OrdinalIgnoreCase),
        
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
                            p => SortArgumentValue(p.GetValue(obj))
                        ), 
                    StringComparer.OrdinalIgnoreCase),

            _ => value
        };
    }

    public static void ApplyFieldChanges(SortedDictionary<string, FieldDefinition> fieldDefinitions, FieldDefinition fieldDefinition)
    {
        var existingField = fieldDefinitions.Values.FirstOrDefault(x => x.Path == fieldDefinition.Path);
        
        if (existingField is not null)
        {
            fieldDefinitions[fieldDefinition.Name] = fieldDefinition;
            return;
        }
        
        var pathParts = fieldDefinition.Path.Split('.');
        var currentFields = fieldDefinitions;

        foreach (var part in pathParts)
        {
            if (!currentFields.TryGetValue(part, out var currentField))
            {
                return; // Path not found
            }

            if (part == pathParts[^1]) // Last part - update the values
            {
                currentFields[part] = fieldDefinition;
                return;
            }

            currentFields = currentField.Fields;
        }
    }

    /// <summary>
    /// Normalizes arguments by ensuring non-null SortedDictionary.
    /// </summary>
    /// <param name="arguments">The arguments to normalize</param>
    /// <returns>A non-null SortedDictionary</returns>
    internal static SortedDictionary<string, object?> NormalizeArguments(SortedDictionary<string, object?>? arguments)
        => arguments ?? new SortedDictionary<string, object?>();
    
    /// <summary>
    /// Normalizes arguments by ensuring non-null Dictionary.
    /// </summary>
    /// <param name="arguments">The arguments to normalize</param>
    /// <returns>A non-null Dictionary</returns>
    internal static SortedDictionary<string, object?> NormalizeArguments(Dictionary<string, object?>? arguments)
        => arguments is null ? new SortedDictionary<string, object?>() : new SortedDictionary<string, object?>(arguments);

    /// <summary>
    /// Normalizes metadata by ensuring non-null Dictionary with nullable values.
    /// </summary>
    /// <param name="metadata">The metadata to normalize</param>
    /// <returns>A non-null Dictionary with nullable values</returns>
    internal static Dictionary<string, object?> NormalizeMetadata(Dictionary<string, object?>? metadata)
        => metadata ?? new Dictionary<string, object?>();

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

    /// <summary>
    /// Compares two values for equality, handling complex objects via JSON serialization.
    /// </summary>
    /// <param name="value1">First value</param>
    /// <param name="value2">Second value</param>
    /// <returns>True if values are equal, false otherwise</returns>
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
}
