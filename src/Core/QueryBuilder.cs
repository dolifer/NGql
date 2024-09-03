using System;
using System.Collections.Generic;
using NGql.Core.Abstractions;
using NGql.Core.Extensions;

namespace NGql.Core;

/// <summary>
///     Represents a query builder.
/// </summary>
public sealed class QueryBuilder
{
    private readonly QueryDefinition _queryDefinition;

    private QueryBuilder(QueryDefinition queryDefinition) => _queryDefinition = queryDefinition;

    /// <summary>
    ///     Creates a new instance of <see cref="QueryBuilder"/>.
    /// </summary>
    /// <param name="name">The name of the query.</param>
    /// <returns>Instance of <see cref="QueryBuilder"/>.</returns>
    public static QueryBuilder New(string name) => new(new QueryDefinition(name));
    
    /// <summary>
    ///     Creates a new instance of <see cref="QueryBuilder"/>.
    /// </summary>
    /// <param name="queryDefinition">The query definition.</param>
    /// <returns>Instance of <see cref="QueryBuilder"/>.</returns>
    public static QueryBuilder FromDefinition(QueryDefinition queryDefinition) => new(queryDefinition);

    /// <summary>
    ///     Adds a field to the query.
    /// </summary>
    /// <param name="field">Field name or path.</param>
    /// <returns>Instance of <see cref="QueryBuilder"/>.</returns>
    /// <exception cref="ArgumentException">Thrown when the field is null or empty.</exception>
    public QueryBuilder AddField(string field)
    {
        if (string.IsNullOrWhiteSpace(field))
        {
            throw new ArgumentException("Field cannot be null or empty", nameof(field));
        }
        
        var parts = field.Split('.', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var value = GetField(_queryDefinition.Fields, parts);

        AddSubField(value, parts[1..]);
        return this;
    }

    private static void AddSubField(FieldDefinition parent, string[] path)
    {
        if (path.Length == 0)
        {
            return;
        }
        
        var value = GetField(parent.Fields, path);

        AddSubField(value, path[1..]);
    }

    private static FieldDefinition GetField(Dictionary<string, FieldDefinition> fields, string[] path)
    {
        var first = path[0];
        var fieldNameParts = first.Split(':', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        
        if (fieldNameParts.Length > 1)
        {
            var alias = fieldNameParts[0];
            var name = fieldNameParts[1];
            if (fields.TryGetValue(name, out var value))
            {
                return value;
            }

            return fields[name] = new FieldDefinition(name, alias);
        }
        else
        {

            if (fields.TryGetValue(first, out var value))
            {
                return value;
            }
        }

        return fields[first] = new FieldDefinition(first);
    }
    
    public Query ToQuery() => _queryDefinition.ToQuery();
}
