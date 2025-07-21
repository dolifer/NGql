using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using NGql.Core.Abstractions;

namespace NGql.Core;

public sealed class QueryBuilderOptions
{
    public static readonly QueryBuilderOptions Default = new();

    public bool UseFieldsCache { get; set; } = true;
}

/// <summary>
///     Represents a query builder.
/// </summary>
public sealed class QueryBuilder
{
    private readonly QueryBuilderOptions _options;
    private static readonly SortedDictionary<string, object> EmptyArguments = [];

    private readonly QueryDefinition _queryDefinition;
    private readonly Dictionary<string, FieldDefinition> _fieldCache = new();
    
    /// <inheritdoc cref="QueryBlock.Variables"/>
    public IEnumerable<Variable> Variables => _queryDefinition.Variables;
    
    private QueryBuilder(QueryDefinition queryDefinition, QueryBuilderOptions? options)
    {
        _queryDefinition = queryDefinition;
        _options = options ?? QueryBuilderOptions.Default;
    }

    /// <summary>
    ///     Creates a new instance of <see cref="QueryBuilder"/>.
    /// </summary>
    /// <param name="name">The name of the query.</param>
    /// <param name="options">The query building options</param>
    /// <returns>Instance of <see cref="QueryBuilder"/>.</returns>
    public static QueryBuilder CreateDefaultBuilder(string name, QueryBuilderOptions? options = null) => new(new QueryDefinition(name),options);

    /// <summary>
    ///     Creates a new instance of <see cref="QueryBuilder"/>.
    /// </summary>
    /// <param name="queryDefinition">The query definition.</param>
    /// <param name="options">The query building options</param>
    /// <returns>Instance of <see cref="QueryBuilder"/>.</returns>
    public static QueryBuilder CreateFromDefinition(QueryDefinition queryDefinition, QueryBuilderOptions? options = null) => new(queryDefinition,options);

    /// <summary>
    ///     Adds a field to the query.
    /// </summary>
    /// <param name="field">Field name or path.</param>
    /// <returns>Instance of <see cref="QueryBuilder"/>.</returns>
    /// <exception cref="ArgumentException">Thrown when the field is null or empty.</exception>
    public QueryBuilder AddField(string field) => AddFieldImpl(field, new SortedDictionary<string, object>(), []);
    
    /// <summary>
    ///     Adds a field to the query.
    /// </summary>
    /// <param name="field">Field name or path.</param>
    /// <param name="subFields">The subfields for the field.</param>
    /// <returns>Instance of <see cref="QueryBuilder"/>.</returns>
    /// <exception cref="ArgumentException">Thrown when the field is null or empty.</exception>
    public QueryBuilder AddField(string field, string[]? subFields) => AddFieldImpl(field, new SortedDictionary<string, object>(), subFields);

    /// <summary>
    ///     Adds a field to the query.
    /// </summary>
    /// <param name="field">Field name or path.</param>
    /// <param name="arguments">The arguments for the field.</param>
    /// <returns>Instance of <see cref="QueryBuilder"/>.</returns>
    /// <exception cref="ArgumentException">Thrown when the field is null or empty.</exception>
    public QueryBuilder AddField(string field, Dictionary<string, object> arguments) => AddFieldImpl(field, new SortedDictionary<string, object>(arguments), []);

    /// <summary>
    ///     Adds a field to the query.
    /// </summary>
    /// <param name="field">Field name or path.</param>
    /// <param name="arguments">The arguments for the field.</param>
    /// <param name="subFields">The subfields for the field.</param>
    /// <returns>Instance of <see cref="QueryBuilder"/>.</returns>
    /// <exception cref="ArgumentException">Thrown when the field is null or empty.</exception>
    public QueryBuilder AddField(string field, Dictionary<string, object> arguments, string[] subFields) => AddFieldImpl(field, new SortedDictionary<string, object>(arguments), subFields);
    
    private QueryBuilder AddFieldImpl(string field, SortedDictionary<string, object> arguments, string[]? subFields)
    {
        if (string.IsNullOrWhiteSpace(field))
        {
            throw new ArgumentException("Field cannot be null or empty", nameof(field));
        }
        
        var value = GetOrAddField(_queryDefinition.Fields, field, arguments);

        if (subFields is null || subFields.Length == 0)
        {
            return this;
        }
        
        foreach (var subField in subFields)
        {
            GetOrAddField(value.Fields, subField, EmptyArguments);
        }
        
        return this;
    }

    public QueryBuilder Include(QueryBuilder queryBuilder) => IncludeImpl(queryBuilder._queryDefinition);

    private QueryBuilder IncludeImpl(QueryDefinition queryDefinition)
    {
        foreach (var fieldDefinition in queryDefinition.Fields.Values)
        {
            RecursiveCreateField(_queryDefinition.Fields, fieldDefinition);
        }

        return this;
    }

    private void RecursiveCreateField(SortedDictionary<string, FieldDefinition> fields, FieldDefinition fieldDefinition)
    {
        var parentField = GetOrCreateField(fields, fieldDefinition.Name, fieldDefinition.Alias, fieldDefinition.Arguments);

        foreach (var childFieldDefinition in fieldDefinition.Fields.Values)
        {
            RecursiveCreateField(parentField.Fields, childFieldDefinition);
        }
    }

    private FieldDefinition GetOrCreateField(SortedDictionary<string, FieldDefinition> fields, string fieldName, string? fieldAlias, SortedDictionary<string, object> arguments)
    {
        if (!fields.TryGetValue(fieldName, out var rootField))
        {
            return fields[fieldName] = GetNewField(fieldName, fieldAlias, arguments);
        }

        return rootField;
    }
    
    private FieldDefinition GetOrAddField(SortedDictionary<string, FieldDefinition> fields, ReadOnlySpan<char> fieldPath, 
        SortedDictionary<string, object> arguments)
    {
        FieldDefinition? value = null;
        SortedDictionary<string, FieldDefinition> currentFields = fields;

        while (fieldPath.Length > 0)
        {
            var nextDot = fieldPath.IndexOf('.');
            var isLastFragment = nextDot == -1;

            var currentPart = isLastFragment
                ? fieldPath
                : fieldPath[..nextDot];

            var currentPath = currentPart.ToString(); 
            var (name, alias) = GetFieldNameAndAlias(currentPath);

            if (string.IsNullOrWhiteSpace(currentPath))
            {
                fieldPath = nextDot == -1 ? ReadOnlySpan<char>.Empty : fieldPath[(nextDot + 1)..];
                continue;
            }
            
            if (_options.UseFieldsCache && _fieldCache.TryGetValue(name, out var cachedField))
            {
                value = cachedField;
                currentFields = value.Fields;
                
                fieldPath = nextDot == -1 ? ReadOnlySpan<char>.Empty : fieldPath[(nextDot + 1)..];
                continue;
            }

            if (!currentFields.TryGetValue(name, out var childValue))
            {
                var fieldArguments = isLastFragment ? arguments : EmptyArguments;
                value = currentFields[name] = GetNewField(name, alias, fieldArguments);

                if (_options.UseFieldsCache)
                {
                    _fieldCache[name] = value;
                }
            }
            else
            {
                value = childValue;
                // Update alias if one is provided and the field doesn't already have one
                if (alias != null && value.Alias == null)
                {
                    value = currentFields[name] = new FieldDefinition(name, alias, value.Arguments);
                }
            }
            
            fieldPath = nextDot == -1 ? ReadOnlySpan<char>.Empty : fieldPath[(nextDot + 1)..];
            currentFields = value.Fields;
        }

        return value ?? throw new InvalidOperationException($"Failed to create a new field for path: {fieldPath}");
    }

    private FieldDefinition GetNewField(string name, string? alias, SortedDictionary<string, object> arguments)
    {
        Helpers.ExtractVariablesFromValue(arguments, _queryDefinition.Variables);
        var sortedArguments = new SortedDictionary<string, object>(arguments.ToDictionary(
            kvp => kvp.Key,
            kvp => Helpers.SortArgumentValue(kvp.Value)));

        return new FieldDefinition(name, alias, sortedArguments);
    }

    private static (string Name, string? Alias) GetFieldNameAndAlias(string field)
    {
        var fieldNameParts = field.Split(':', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var namePart = fieldNameParts.Length == 2 ? fieldNameParts[1] : field;
        var alias = fieldNameParts.Length == 2 ? fieldNameParts[0] : null;

        return (namePart, alias);
    }
    
    /// <inheritdoc cref="QueryBlock.ToString()"/>
    public override string ToString() => _queryDefinition.ToString();
    public static implicit operator string(QueryBuilder query) => query.ToString();
}
