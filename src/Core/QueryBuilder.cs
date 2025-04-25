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
    public static QueryBuilder CreateDefaultBuilder(string name) => new(new QueryDefinition(name));
    
    /// <summary>
    ///     Creates a new instance of <see cref="QueryBuilder"/>.
    /// </summary>
    /// <param name="queryDefinition">The query definition.</param>
    /// <returns>Instance of <see cref="QueryBuilder"/>.</returns>
    public static QueryBuilder CreateFromDefinition(QueryDefinition queryDefinition) => new(queryDefinition);

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
        
        var parts = field.Split('.', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var rootField = GetOrCreateField(_queryDefinition.Fields, parts[0], arguments);
        var value = GetOrAddField(rootField, parts[1..], arguments);

        foreach (var subField in subFields ?? [])
        {
            GetOrAddField(value, [subField]);
        }
        
        return this;
    }

    private static FieldDefinition GetOrCreateField(Dictionary<string, FieldDefinition> fields, string fieldName, Dictionary<string, object>? arguments = null)
    {
        if (!fields.TryGetValue(fieldName, out var rootField))
        {
            return fields[fieldName] = GetNewField(fieldName, arguments);
        }

        return rootField;
    }
    
    private static FieldDefinition GetOrAddField(FieldDefinition parentField, string[] parts, Dictionary<string, object>? arguments = null)
    {
        var value = parentField;
        
        for (var i = 0; i < parts.Length; i++)
        {
            var fieldArguments = i == parts.Length - 1 ? arguments : null;
            var newFieldName = parts[i];

            if (!value.Fields.TryGetValue(newFieldName, out var childValue))
            {
                value = value.Fields[newFieldName] = GetNewField(newFieldName, fieldArguments);
            }
            else
            {
                value = childValue;
            }
        }

        return value;
    }
    
    private static FieldDefinition GetNewField(string field, Dictionary<string, object>? arguments = null)
    {
        var fieldNameParts = field.Split(':', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var namePart = fieldNameParts.Length == 2 ? fieldNameParts[1] : field;
        var alias = fieldNameParts.Length == 2 ? fieldNameParts[0] : null;

        return new FieldDefinition(namePart, alias, arguments);
    }
    
    public Query ToQuery() => _queryDefinition.ToQuery();
}
