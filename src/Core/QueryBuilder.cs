using System;
using System.Collections.Generic;
using System.Linq;
using NGql.Core.Abstractions;
using NGql.Core.Builders;

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

    private readonly QueryDefinition _queryDefinition;

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
    public QueryBuilder AddField(string field) => AddFieldImpl(field, new SortedDictionary<string, object?>(), []);
    
    /// <summary>
    ///     Adds a field to the query.
    /// </summary>
    /// <param name="field">Field name or path.</param>
    /// <param name="subFields">The subfields for the field.</param>
    /// <returns>Instance of <see cref="QueryBuilder"/>.</returns>
    /// <exception cref="ArgumentException">Thrown when the field is null or empty.</exception>
    public QueryBuilder AddField(string field, string[]? subFields) => AddFieldImpl(field, new SortedDictionary<string, object?>(), subFields);

    /// <summary>
    ///     Adds a field to the query.
    /// </summary>
    /// <param name="field">Field name or path.</param>
    /// <param name="arguments">The arguments for the field.</param>
    /// <returns>Instance of <see cref="QueryBuilder"/>.</returns>
    /// <exception cref="ArgumentException">Thrown when the field is null or empty.</exception>
    public QueryBuilder AddField(string field, Dictionary<string, object?> arguments) => AddFieldImpl(field, new SortedDictionary<string, object?>(arguments), []);

    /// <summary>
    ///     Adds a field to the query.
    /// </summary>
    /// <param name="field">Field name or path.</param>
    /// <param name="fieldBuilder">The field builder action.</param>
    /// <returns>Instance of <see cref="QueryBuilder"/>.</returns>
    /// <exception cref="ArgumentException">Thrown when the field is null or empty.</exception>
    public QueryBuilder AddField(string field, Action<FieldBuilder> fieldBuilder)
    {
        if (string.IsNullOrWhiteSpace(field))
        {
            throw new ArgumentException("Field cannot be null or empty", nameof(field));
        }
        
        var builder = FieldBuilder.Create(_queryDefinition.Fields, field);

        fieldBuilder.Invoke(builder);

        var updatedField = builder.Build();

        Helpers.ApplyFieldChanges(_queryDefinition.Fields, updatedField);

        return this;
    }

    /// <summary>
    ///     Adds a field to the query.
    /// </summary>
    /// <param name="field">Field name or path.</param>
    /// <param name="arguments">The arguments for the field.</param>
    /// <param name="subFields">The subfields for the field.</param>
    /// <returns>Instance of <see cref="QueryBuilder"/>.</returns>
    /// <exception cref="ArgumentException">Thrown when the field is null or empty.</exception>
    public QueryBuilder AddField(string field, Dictionary<string, object?> arguments, string[] subFields) => AddFieldImpl(field, new SortedDictionary<string, object?>(arguments), subFields);
    
    private QueryBuilder AddFieldImpl(string field, SortedDictionary<string, object?> arguments, string[]? subFields)
    {
        if (string.IsNullOrWhiteSpace(field))
        {
            throw new ArgumentException("Field cannot be null or empty", nameof(field));
        }

        Helpers.ExtractVariablesFromValue(arguments, _queryDefinition.Variables);

        var builder = FieldBuilder.Create(_queryDefinition.Fields, field, arguments);

        if (subFields is null || subFields.Length == 0)
        {
            return this;
        }

        foreach (var subField in subFields)
        {
            builder.AddField(subField);
        }

        return this;
    }

    public QueryBuilder Include(QueryBuilder queryBuilder) => IncludeImpl(queryBuilder._queryDefinition);

    private QueryBuilder IncludeImpl(QueryDefinition queryDefinition)
    {
        _queryDefinition.Variables = new SortedSet<Variable>(_queryDefinition.Variables.Union(queryDefinition.Variables));
        
        foreach (var fieldDefinition in queryDefinition.Fields.Values)
        {
            FieldBuilder.Include(_queryDefinition.Fields, fieldDefinition);
        }

        return this;
    }
 
    /// <inheritdoc cref="QueryBlock.ToString()"/>
    public override string ToString() => _queryDefinition.ToString();
    public static implicit operator string(QueryBuilder query) => query.ToString();
}
