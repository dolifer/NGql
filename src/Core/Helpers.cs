using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NGql.Core.Abstractions;

namespace NGql.Core;

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
        }
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
}
