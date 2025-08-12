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
    private readonly Dictionary<string, string> _queryMap = new();

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
            _queryMap[queryName] = fieldPath;
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
            if (_queryMap.TryGetValue(Definition.Name, out var existingPath) && 
                Definition.Fields.ContainsKey(existingPath))
            {
                return; // Keep the existing valid mapping
            }

            // If we don't have a valid mapping, use the first field
            var firstField = Definition.Fields.Values.First();
            var fieldPath = !string.IsNullOrEmpty(firstField.Alias)
                ? firstField.Alias
                : firstField.Name;
            _queryMap[Definition.Name] = fieldPath;
        }
        else
        {
            // If no fields, map to itself
            _queryMap[Definition.Name] = Definition.Name;
        }
    }

    /// <summary>
    /// Gets the count of fields in the QueryDefinition.
    /// This represents the correct value with or without merge.
    /// </summary>
    internal int DefinitionsCount => Definition.Fields.Count;

    /// <inheritdoc cref="QueryBlock.ToString()"/>
    public override string ToString() => Definition.ToString();
    public static implicit operator string(QueryBuilder query) => query.ToString();

    /// <summary>
    /// Gets the path segments to reach a specific node within a query.
    /// </summary>
    /// <param name="queryName">The name of the query to find the path for.</param>
    /// <param name="nodePath">The optional node path within the query (e.g., "edges.node").</param>
    /// <returns>An array of path segments to reach the specified node.</returns>
    public string[] GetPathTo(string queryName, string? nodePath = null)
    {
        // Get the root path for the query from QueryMap
        var rootPath = _queryMap.GetValueOrDefault(queryName, queryName);
        
        // If the query doesn't exist in our map, return empty array
        if (string.IsNullOrEmpty(rootPath))
        {
            return [];
        }

        // If no node path specified, return just the root
        if (string.IsNullOrEmpty(nodePath))
        {
            return [rootPath];
        }
        
        // Find the path to the target node in the field structure
        if (Definition.Fields.TryGetValue(rootPath, out var rootField))
        {
            var nodeSegments = nodePath.Split('.');
            var targetNode = nodeSegments[^1]; // Last segment is what we're looking for
            
            // Search for the target node and return the path to its parent
            var path = FindPathToNode(rootField, targetNode, [rootPath]);
            if (path is { Length: > 1 })
            {
                // Return path excluding the target node itself (path to parent)
                return path.Take(path.Length - 1).ToArray();
            }
        }
        
        return [rootPath];
    }
    
    /// <summary>
    /// Recursively searches for a target node and returns the full path to it.
    /// </summary>
    private static string[]? FindPathToNode(FieldDefinition field, string targetNode, string[] currentPath)
    {
        // Check if any direct child matches the target node
        foreach (var childField in field.Fields.Values)
        {
            var fieldIdentifier = !string.IsNullOrEmpty(childField.Alias) ? childField.Alias : childField.Name;
            
            if (childField.Name.Equals(targetNode, StringComparison.OrdinalIgnoreCase))
            {
                // Found the target node, return the path including the target
                return currentPath.Concat([fieldIdentifier]).ToArray();
            }

            var childPath = FindPathToNode(childField, targetNode, currentPath.Concat([fieldIdentifier]).ToArray());
            if (childPath != null)
            {
                return childPath;
            }
        }

        return null;
    }
}
