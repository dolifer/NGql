using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
}
