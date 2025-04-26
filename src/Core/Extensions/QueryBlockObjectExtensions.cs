using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NGql.Core.Abstractions;

namespace NGql.Core.Extensions;

internal static class QueryBlockObjectExtensions
{
    internal static SortedDictionary<string, object> GetArguments(this QueryBlock queryBlock, bool isRootElement)
    {
        var arguments = new SortedDictionary<string, object>(StringComparer.Ordinal);

        foreach (var kvp in queryBlock.Arguments)
        {
            if (kvp.Value is Variable variable)
            {
                arguments[isRootElement ? variable.Name : kvp.Key] = kvp.Value;
                continue;
            }

            arguments[kvp.Key] = kvp.Value;
        }

        if (!isRootElement)
        {
            return arguments;
        }

        foreach (var variable in queryBlock.Variables)
        {
            var existingArgument = arguments.Values
                .FirstOrDefault(x => x is Variable v && v.Name == variable.Name);

            if (existingArgument is null)
            {
                arguments[variable.Name] = variable;
            }
        }

        return arguments;
    }

    /// <summary>
    /// Adds the given type properties into <see cref="QueryBlock.FieldsList"/> part of the query.
    /// </summary>
    /// <param name="block">The query block</param>
    /// <param name="path">The path to use</param>
    /// <param name="name">The name to use</param>
    /// <param name="alias">The alias to use</param>
    /// <typeparam name="T">The type to include</typeparam>
    public static void IncludeAtPath<T>(this QueryBlock block, string path, string name, string? alias = null)
    {
        var paths = path.Split('.', StringSplitOptions.RemoveEmptyEntries);
        var currentBlock = block;

        foreach (var t in paths)
        {
            if (string.IsNullOrWhiteSpace(t))
            {
                continue;
            }

            var subQuery = new QueryBlock(t);
            currentBlock.AddField(subQuery);
            currentBlock = subQuery;
        }

        currentBlock.Include<T>(name, alias);
    }

    /// <summary>
    /// Adds the given type properties into <see cref="QueryBlock.FieldsList"/> part of the query.
    /// </summary>
    /// <param name="block">The query block</param>
    /// <param name="name">The name to use</param>
    /// <param name="alias"></param>
    /// <returns>Query</returns>
    public static void Include<T>(this QueryBlock block, string name, string? alias = null)
    {
        var properties = typeof(T).GetProperties();

        var subQuery = new QueryBlock(name, alias: alias);

        HandleProperties(subQuery, null, properties);

        block.AddField(subQuery);
    }

    /// <summary>
    /// Adds the given object properties into <see cref="QueryBlock.FieldsList"/> part of the query.
    /// </summary>
    /// <param name="block">The query block</param>
    /// <param name="obj">A value</param>
    /// <returns>Query</returns>
    public static void Include(this QueryBlock block, object obj)
    {
        var type = obj.GetType();
        var properties = type.GetProperties();

        HandleProperties(block, obj, properties);
    }

    private static void HandleProperties(QueryBlock block, object? obj, PropertyInfo[] properties)
    {
        foreach (var property in properties)
        {
            var value = obj is null ? null : property.GetValue(obj);
            var alias = property.GetAlias();

            if (value is IDictionary dict)
            {
                HandleDictionary(block, property.Name, alias, dict);
            }
            else if (value != null && !IsSimpleType(value.GetType()))
            {
                var subQuery = new QueryBlock(property.Name, alias: alias);
                subQuery.Include(value); // Recursive call for nested objects
                block.AddField(subQuery);
            }
            else
            {
                if (alias == property.Name)
                {
                    block.AddField(property.Name);
                }
                else
                {
                    block.AddField(alias is null
                        ? new QueryBlock(property.Name) { IsEmpty = true }
                        : new QueryBlock(alias, alias: property.Name) { IsEmpty = true });
                }
            }
        }
    }

    private static void HandleDictionary(QueryBlock block, string name, string? alias, IDictionary dict)
    {
        var subQuery = new QueryBlock(name, alias: alias);
        foreach (var key in dict.Keys)
        {
            if (dict[key] is IDictionary nestedDict)
            {
                HandleDictionary(subQuery, key.ToString(), null, nestedDict);
            }
            else
            {
                subQuery.AddField(key.ToString());
            }
        }

        block.AddField(subQuery);
    }

    private static bool IsSimpleType(Type type)
        => type.IsPrimitive ||
            new Type[] {
                    typeof(string),
                    typeof(decimal),
                    typeof(DateTime),
                    typeof(DateTimeOffset),
                    typeof(TimeSpan),
                    typeof(Guid)
            }.Contains(type) ||
            Convert.GetTypeCode(type) != TypeCode.Object;
}
