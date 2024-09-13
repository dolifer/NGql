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
    /// <param name="arguments">The arguments for the field.</param>
    /// <param name="subFields">The subfields for the field.</param>
    /// <returns>Instance of <see cref="QueryBuilder"/>.</returns>
    /// <exception cref="ArgumentException">Thrown when the field is null or empty.</exception>
    public QueryBuilder AddField(string field, Dictionary<string, object>? arguments = null, string[]? subFields = null)
    {
        if (string.IsNullOrWhiteSpace(field))
        {
            throw new ArgumentException("Field cannot be null or empty", nameof(field));
        }
        
        var value = GetOrAddField(_queryDefinition.Fields, field, arguments);

        foreach (var subField in subFields ?? [])
        {
            GetOrAddField(value.Fields, subField);
        }
        
        return this;
    }

    private static FieldDefinition GetOrAddField(Dictionary<string, FieldDefinition> fields, string path, Dictionary<string, object>? arguments = null)
    {
        var parts = path.Split('.', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var value = default(FieldDefinition)!;

        for(var i = 0; i < parts.Length; i++)
        {
            var newField = GetNewField(parts[i], i == parts.Length - 1, arguments);

            if (i == 0)
            {
                if (!fields.TryGetValue(newField.Name, out value))
                {
                    value = fields[newField.Name] = newField;
                }

                continue;
            }
            
            if (!value.Fields.TryGetValue(newField.Name, out var childValue))
            {
                value = value.Fields[newField.Name] = newField;
            }
            else
            {
                value = childValue;
            }
        }
        
        return value;
    }
    
    private static FieldDefinition GetNewField(string field, bool isLast, Dictionary<string, object>? arguments = null)
    {
        var fieldNameParts = field.Split(':', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        return  new FieldDefinition(fieldNameParts.Length == 2 ? fieldNameParts[1] : field,
            fieldNameParts.Length == 2 ? fieldNameParts[0] : null, isLast ? arguments : null);
    }
    
    public Query ToQuery() => _queryDefinition.ToQuery();
}
