using System.Collections;
using System.Reflection;
using NGql.Core.Abstractions;

namespace NGql.Core.Extensions;

internal static class QueryBlockObjectExtensions
{
    internal static SortedDictionary<string, object> GetArguments(this QueryBlock queryBlock, bool isRootElement)
    {
        var arguments = new SortedDictionary<string, object>(StringComparer.Ordinal);
        CopyArguments(queryBlock, isRootElement, arguments);

        if (isRootElement)
        {
            AddMissingRootVariables(queryBlock, arguments);
        }
        return arguments;
    }

    private static void CopyArguments(QueryBlock queryBlock, bool isRootElement, SortedDictionary<string, object> arguments)
    {
        foreach (var kvp in queryBlock.Arguments)
        {
            var key = kvp.Value is Variable variable && isRootElement ? variable.Name : kvp.Key;
            arguments[key] = kvp.Value;
        }
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Major Code Smell", "S3267:Loops should be simplified using the \"Where\" LINQ method",
        Justification = "Render hot path — a plain foreach avoids the closure + enumerator allocations of Where on every block render.")]
    private static void AddMissingRootVariables(QueryBlock queryBlock, SortedDictionary<string, object> arguments)
    {
        foreach (var variable in queryBlock.Variables)
        {
            if (!ContainsVariableNamed(arguments, variable.Name))
            {
                arguments[variable.Name] = variable;
            }
        }
    }

    private static bool ContainsVariableNamed(SortedDictionary<string, object> arguments, string name)
    {
        foreach (var value in arguments.Values)
        {
            if (value is Variable existing && existing.Name == name)
            {
                return true;
            }
        }
        return false;
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
        // Use Span to avoid string allocations during path parsing
        var pathSpan = path.AsSpan();
        var currentBlock = block;

        while (!pathSpan.IsEmpty)
        {
            var dotIndex = pathSpan.IndexOf('.');
            ReadOnlySpan<char> currentSegment;

            if (dotIndex == -1)
            {
                // Last segment
                currentSegment = pathSpan;
                pathSpan = ReadOnlySpan<char>.Empty;
            }
            else
            {
                // Extract current segment and advance
                currentSegment = pathSpan[..dotIndex];
                pathSpan = pathSpan[(dotIndex + 1)..];
            }

            // Skip empty segments
            if (currentSegment.IsEmpty || currentSegment.IsWhiteSpace())
            {
                continue;
            }

            // Convert to string only when creating the QueryBlock
            var segmentString = currentSegment.ToString();
            var subQuery = new QueryBlock(segmentString);
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
        var properties = GetSelectableProperties(typeof(T));

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
        var properties = GetSelectableProperties(type);

        HandleProperties(block, obj, properties);
    }

    /// <summary>
    /// Returns the properties of <paramref name="type"/> that map to GraphQL selection fields:
    /// indexer properties are excluded (they are not addressable as fields and would throw when
    /// read without index arguments), and <c>new</c>-shadowed properties are collapsed to their
    /// most-derived declaration so a response name is never emitted twice. Ordering matches the
    /// original alphabetical (case-insensitive) sort so normal types render unchanged.
    /// </summary>
    private static PropertyInfo[] GetSelectableProperties(Type type)
    {
        // A name may appear more than once when a derived type re-declares a base property with
        // `new`; GetProperties() returns every declaration and does not guarantee derived-first
        // ordering, so keep the declaration whose DeclaringType is furthest down the hierarchy.
        var byName = new Dictionary<string, PropertyInfo>(StringComparer.Ordinal);

        foreach (var property in type.GetProperties())
        {
            if (property.GetIndexParameters().Length > 0)
            {
                continue;
            }

            if (!byName.TryGetValue(property.Name, out var existing) || IsMoreDerivedThan(property, existing))
            {
                byName[property.Name] = property;
            }
        }

        return byName.Values
            .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static bool IsMoreDerivedThan(PropertyInfo candidate, PropertyInfo current)
    {
        var candidateType = candidate.DeclaringType;
        var currentType = current.DeclaringType;
        if (candidateType is null || currentType is null || candidateType == currentType)
        {
            return false;
        }

        // candidate is more derived when currentType sits somewhere up its base chain.
        for (var baseType = candidateType.BaseType; baseType is not null; baseType = baseType.BaseType)
        {
            if (baseType == currentType)
            {
                return true;
            }
        }
        return false;
    }

    private static void HandleProperties(QueryBlock block, object? obj, PropertyInfo[] properties)
    {
        foreach (var property in properties)
        {
            HandleProperty(block, obj, property);
        }
    }

    private static void HandleProperty(QueryBlock block, object? obj, PropertyInfo property)
    {
        var value = obj is null ? null : property.GetValue(obj);
        var alias = property.GetAlias();

        if (value is IDictionary dict)
        {
            HandleDictionary(block, property.Name, alias, dict);
            return;
        }
        if (value is not null && !IsSimpleType(value.GetType()))
        {
            var subQuery = new QueryBlock(property.Name, alias: alias);
            subQuery.Include(value);
            block.AddField(subQuery);
            return;
        }
        AddSimpleProperty(block, property.Name, alias);
    }

    private static void AddSimpleProperty(QueryBlock block, string propertyName, string? alias)
    {
        if (alias == propertyName)
        {
            block.AddField(propertyName);
            return;
        }
        block.AddField(alias is null
            ? new QueryBlock(propertyName) { IsEmpty = true }
            : new QueryBlock(alias, alias: propertyName) { IsEmpty = true });
    }

    private static void HandleDictionary(QueryBlock block, string name, string? alias, IDictionary dict)
    {
        var subQuery = new QueryBlock(name, alias: alias);
        var sortedKeys = dict.Keys.Cast<object>()
            .OrderBy(k => k.ToString(), StringComparer.OrdinalIgnoreCase);

        foreach (var key in sortedKeys)
        {
            if (dict[key] is IDictionary nestedDict)
            {
                HandleDictionary(subQuery, key.ToString()!, null, nestedDict);
            }
            else
            {
                subQuery.AddField(key.ToString()!);
            }
        }

        block.AddField(subQuery);
    }

    private static readonly HashSet<Type> SimpleTypes =
    [
        typeof(string),
        typeof(decimal),
        typeof(DateTime),
        typeof(DateTimeOffset),
        typeof(TimeSpan),
        typeof(Guid)
    ];

    private static bool IsSimpleType(Type type)
        => type.IsPrimitive ||
            SimpleTypes.Contains(type) ||
            Convert.GetTypeCode(type) != TypeCode.Object;
}

