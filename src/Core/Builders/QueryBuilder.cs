using System;
using System.Collections.Generic;
using System.Linq;
using NGql.Core.Abstractions;
using NGql.Core.Extensions;

namespace NGql.Core.Builders;

/// <summary>
///     Represents a query builder.
/// </summary>
public sealed class QueryBuilder
{
    /// <summary>
    ///    The query definition that this builder is working with.
    /// </summary>
    public QueryDefinition Definition { get; }

    /// <inheritdoc cref="QueryBlock.Variables"/>
    public IEnumerable<Variable> Variables => Definition.Variables;
    
    private QueryBuilder(QueryDefinition queryDefinition)
    {
        Definition = queryDefinition;
    }

    /// <summary>
    ///     Creates a new instance of <see cref="QueryBuilder"/>.
    /// </summary>
    /// <param name="name">The name of the query.</param>
    /// <param name="options">The query building options</param>
    /// <returns>Instance of <see cref="QueryBuilder"/>.</returns>
    public static QueryBuilder CreateDefaultBuilder(string name) => new(new QueryDefinition(name));

    /// <summary>
    ///     Creates a new instance of <see cref="QueryBuilder"/>.
    /// </summary>
    /// <param name="queryDefinition">The query definition.</param>
    /// <param name="options">The query building options</param>
    /// <returns>Instance of <see cref="QueryBuilder"/>.</returns>
    public static QueryBuilder CreateFromDefinition(QueryDefinition queryDefinition) => new(queryDefinition);

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
    /// <param name="subFields">The subfields for the field.</param>
    /// <returns>Instance of <see cref="QueryBuilder"/>.</returns>
    /// <exception cref="ArgumentException">Thrown when the field is null or empty.</exception>
    public QueryBuilder AddField(string field, FieldDefinition[]? subFields) => AddFieldDefinitionImpl(field, new SortedDictionary<string, object?>(), subFields);
    
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
        
        var builder = FieldBuilder.Create(Definition.Fields, field, "object");

        fieldBuilder.Invoke(builder);

        var updatedField = builder.Build();

        Helpers.ApplyFieldChanges(Definition.Fields, updatedField);

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

    /// <summary>
    ///     Adds a field to the query.
    /// </summary>
    /// <param name="field">Field name or path.</param>
    /// <param name="arguments">The arguments for the field.</param>
    /// <param name="subFields">The subfields for the field.</param>
    /// <returns>Instance of <see cref="QueryBuilder"/>.</returns>
    /// <exception cref="ArgumentException">Thrown when the field is null or empty.</exception>
    public QueryBuilder AddField(string field, Dictionary<string, object?> arguments, FieldDefinition[] subFields) => AddFieldDefinitionImpl(field, new SortedDictionary<string, object?>(arguments), subFields);

    private QueryBuilder AddFieldDefinitionImpl(string field, SortedDictionary<string, object?> arguments, FieldDefinition[]? subFields)
    {
        if (string.IsNullOrWhiteSpace(field))
        {
            throw new ArgumentException("Field cannot be null or empty", nameof(field));
        }

        Helpers.ExtractVariablesFromValue(arguments, Definition.Variables);

        var builder = FieldBuilder.Create(Definition.Fields, field, "object", arguments);

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

    private QueryBuilder AddFieldImpl(string field, SortedDictionary<string, object?> arguments, string[]? subFields)
    {
        var subFieldDefinitions = subFields?
            .Select(subField => new FieldDefinition(subField))
            .ToArray();

        return AddFieldDefinitionImpl(field, arguments, subFieldDefinitions);
    }

    public QueryBuilder Include(QueryBuilder queryBuilder) => IncludeImpl(queryBuilder.Definition);

    private QueryBuilder IncludeImpl(QueryDefinition queryDefinition)
    {
        Definition.Variables = new SortedSet<Variable>(Definition.Variables.Union(queryDefinition.Variables));
        
        foreach (var fieldDefinition in queryDefinition.Fields.Values)
        {
            FieldBuilder.Include(Definition.Fields, fieldDefinition);
        }

        return this;
    }
 
    /// <inheritdoc cref="QueryBlock.ToString()"/>
    public override string ToString() => Definition.ToString();
    public static implicit operator string(QueryBuilder query) => query.ToString();
}
