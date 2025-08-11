using System;
using System.Collections.Generic;
using System.Linq;
using NGql.Core.Abstractions;
using NGql.Core.Extensions;
using NGql.Core.Features;

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

    /// <summary>
    ///     Maps original query names to their merged definition names.
    /// </summary>
    internal Dictionary<string, string> QueryMap { get; } = new();

    private QueryBuilder(QueryDefinition queryDefinition)
    {
        Definition = queryDefinition;
        // Initialize the QueryMap to track the root query's own path
        UpdateRootQueryMap();
    }

    /// <summary>
    ///     Creates a new instance of <see cref="QueryBuilder"/>.
    /// </summary>
    /// <param name="name">The name of the query.</param>
    /// <param name="options">The query building options</param>
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

        UpdateRootQueryMap();
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
            UpdateRootQueryMap();
            return this;
        }

        foreach (var subField in subFields)
        {
            builder.AddField(subField);
        }

        UpdateRootQueryMap();
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

        // Determine merging strategy and merge using QueryMerger
        var effectiveMergingStrategy = GetEffectiveMergingStrategy(queryDefinition.MergingStrategy);
        var mergeResult = QueryMerger.MergeQuery(Definition.Fields, queryDefinition, effectiveMergingStrategy);

        // Apply the merge results
        Definition.Fields.Clear();
        foreach (var (key, field) in mergeResult.UpdatedFields)
        {
            Definition.Fields[key] = field;
        }

        // Update QueryMap with merge results (only for incoming query)
        foreach (var (queryName, fieldPath) in mergeResult.QueryMap)
        {
            QueryMap[queryName] = fieldPath;
        }

        // Update root query mapping if fields changed
        UpdateRootQueryMap();

        return this;
    }

    private MergingStrategy GetEffectiveMergingStrategy(MergingStrategy childStrategy)
    {
        // Child NeverMerge always takes precedence
        if (childStrategy == MergingStrategy.NeverMerge)
            return MergingStrategy.NeverMerge;

        return Definition.MergingStrategy switch
        {
            MergingStrategy.NeverMerge => MergingStrategy.NeverMerge,
            MergingStrategy.MergeByDefault => childStrategy,
            _ => Definition.MergingStrategy
        };
    }
 
    /// <summary>
    /// Updates the QueryMap entry for the root query to point to its first field's alias/name
    /// </summary>
    private void UpdateRootQueryMap()
    {
        if (Definition.Fields.Count > 0)
        {
            // Keep existing root mapping if it exists and points to a valid field
            if (QueryMap.TryGetValue(Definition.Name, out var existingPath) && 
                Definition.Fields.ContainsKey(existingPath))
            {
                return; // Keep the existing valid mapping
            }

            // If we don't have a valid mapping, use the first field
            var firstField = Definition.Fields.Values.First();
            var fieldPath = !string.IsNullOrEmpty(firstField.Alias)
                ? firstField.Alias
                : firstField.Name;
            QueryMap[Definition.Name] = fieldPath;
        }
        else
        {
            // If no fields, map to itself
            QueryMap[Definition.Name] = Definition.Name;
        }
    }

    /// <summary>
    /// Gets the query path for a given query name, returning the merged path if the query was merged,
    /// or the original name if it remains separate.
    /// </summary>
    /// <param name="queryName">The original query name to lookup.</param>
    /// <returns>The final query path (merged target or original name).</returns>
    public string GetQueryPath(string queryName)
        => QueryMap.GetValueOrDefault(queryName, queryName);

    /// <summary>
    /// Gets the count of fields in the QueryDefinition.
    /// This represents the correct value with or without merge.
    /// </summary>
    internal int DefinitionsCount => Definition.Fields.Count;

    /// <inheritdoc cref="QueryBlock.ToString()"/>
    public override string ToString() => Definition.ToString();
    public static implicit operator string(QueryBuilder query) => query.ToString();
}
