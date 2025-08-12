using System;
using System.Collections.Generic;
using System.Linq;
using NGql.Core.Abstractions;
using NGql.Core.Features;
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
    public QueryDefinition Definition => _definition;

    /// <inheritdoc cref="QueryBlock.Variables"/>
    public IEnumerable<Variable> Variables => Definition.Variables;

    /// <summary>
    ///     Maps original query names to their merged definition names.
    /// </summary>
    private readonly QueryMap _queryMap = new();

    private readonly QueryDefinition _definition;

    private QueryBuilder(QueryDefinition queryDefinition)
    {
        _definition = queryDefinition;
        // Initialize the QueryMap to track the root query's own path
        _queryMap.UpdateRootMapping(_definition);
    }

    /// <summary>
    ///     Creates a new instance of <see cref="QueryBuilder"/>.
    /// </summary>
    /// <param name="name">The name of the query.</param>
    /// <returns>Instance of <see cref="QueryBuilder"/>.</returns>
    public static QueryBuilder CreateDefaultBuilder(string name) => new(new QueryDefinition(name));

    /// <summary>
    ///     Creates a new instance of <see cref="QueryBuilder"/> with a specific merging strategy.
    /// </summary>
    /// <param name="name">The name of the query.</param>
    /// <param name="mergingStrategy">The merging strategy to use.</param>
    /// <returns>Instance of <see cref="QueryBuilder"/>.</returns>
    public static QueryBuilder CreateDefaultBuilder(string name, MergingStrategy mergingStrategy)
    {
        var definition = new QueryDefinition(name) { MergingStrategy = mergingStrategy };
        return new(definition);
    }

    /// <summary>
    ///     Sets the merging strategy for this query builder.
    /// </summary>
    /// <param name="strategy">The merging strategy to use.</param>
    /// <returns>Instance of <see cref="QueryBuilder"/>.</returns>
    public QueryBuilder WithMergingStrategy(MergingStrategy strategy)
    {
        Definition.MergingStrategy = strategy;
        return this;
    }

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

        _queryMap.UpdateRootMapping(_definition);
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

        var type = subFields?.Length > 0
            ? Constants.ObjectFieldType // If subfields are present, treat as an object
            : Constants.DefaultFieldType; // Otherwise, use default type

        var builder = FieldBuilder.Create(Definition.Fields, field, type, arguments);

        if (subFields is null || subFields.Length == 0)
        {
            _queryMap.UpdateRootMapping(_definition);
            return this;
        }

        foreach (var subField in subFields)
        {
            builder.AddField(subField);
        }

        _queryMap.UpdateRootMapping(_definition);
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
        QueryMerger.MergeQuery(_definition, _queryMap, in queryDefinition);

        return this;
    }

    /// <summary>
    /// Gets the path segments to reach a specific node within a query.
    /// </summary>
    /// <param name="queryName">The name of the query to find the path for.</param>
    /// <param name="nodePath">The optional node path within the query (e.g., "edges.node").</param>
    /// <returns>An array of path segments to reach the specified node.</returns>
    public string[] GetPathTo(string queryName, string? nodePath = null)
        => _queryMap.GetPathTo(queryName, nodePath, _definition);

    /// <summary>
    /// Gets the count of fields in the QueryDefinition.
    /// This represents the correct value with or without merge.
    /// </summary>
    internal int DefinitionsCount => Definition.Fields.Count;

    /// <inheritdoc cref="QueryBlock.ToString()"/>
    public override string ToString() => Definition.ToString();

    public static implicit operator string(QueryBuilder query) => query.ToString();
}
