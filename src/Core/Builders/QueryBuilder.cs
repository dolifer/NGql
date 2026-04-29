using System.Runtime.CompilerServices;
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
    public QueryDefinition Definition => _definition;

    /// <inheritdoc cref="QueryBlock.Variables"/>
    public IEnumerable<Variable> Variables => Definition.Variables;

    /// <summary>
    ///     Maps original query names to their merged definition names.
    /// </summary>
    private QueryMap? _queryMap;
    private QueryMap QueryMapInstance => _queryMap ??= new();

    private readonly QueryDefinition _definition;

    /// <summary>
    ///     Caches paths to fields for O(1) lookup in GetPathTo().
    ///     Maps field name/alias → string[] path segments from root.
    /// </summary>
    /// <summary>
    /// Two-level path cache: <c>rootPath → (nodePath → segments)</c>. The two-level structure avoids
    /// allocating a concatenated <c>"{root}.{node}"</c> string on every <c>GetPathTo</c> cache hit.
    /// </summary>
    private readonly Dictionary<string, Dictionary<string, string[]>> _pathIndex = new();

    private QueryBuilder(QueryDefinition queryDefinition) => _definition = queryDefinition;

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
    /// <param name="arguments">The arguments for the field.</param>
    /// <param name="metadata">The field metadata.</param>
    /// <returns>Instance of <see cref="QueryBuilder"/>.</returns>
    /// <exception cref="ArgumentException">Thrown when the field is null or empty.</exception>
    public QueryBuilder AddField(string field, Dictionary<string, object?>? arguments = null, Dictionary<string, object?>? metadata = null)
    {
        // FAST PATH: Most common case - no arguments, no metadata
        if (arguments is null && metadata is null)
        {
            return AddFieldFastPath(field);
        }
        
        if (arguments?.Count > 0)
        {
            SortedDictionary<string, object?>? sortedArgs = new SortedDictionary<string, object?>(arguments, StringComparer.OrdinalIgnoreCase);
            return AddFieldCore(field, sortedArgs, null, metadata);
        }
        return AddFieldCore(field, null, null, metadata);
    }

    /// <summary>
    ///     Adds a field to the query.
    /// </summary>
    /// <param name="field">Field name or path.</param>
    /// <param name="subFields">The subfields for the field.</param>
    /// <param name="metadata">The field metadata.</param>
    /// <returns>Instance of <see cref="QueryBuilder"/>.</returns>
    /// <exception cref="ArgumentException">Thrown when the field is null or empty.</exception>
    public QueryBuilder AddField(string field, string[]? subFields, Dictionary<string, object?>? metadata = null)
        => AddFieldCore(field, null, subFields?.Select(subField => new FieldDefinition(subField)), metadata);

    /// <summary>
    ///     Adds a field to the query.
    /// </summary>
    /// <param name="field">Field name or path.</param>
    /// <param name="subFields">The subfields for the field.</param>
    /// <param name="metadata">The field metadata.</param>
    /// <returns>Instance of <see cref="QueryBuilder"/>.</returns>
    /// <exception cref="ArgumentException">Thrown when the field is null or empty.</exception>
    public QueryBuilder AddField(string field, FieldDefinition[]? subFields, Dictionary<string, object?>? metadata = null)
        => AddFieldCore(field, null, subFields, metadata);

    /// <summary>
    ///     Adds a field to the query using a field builder.
    /// </summary>
    /// <param name="field">Field name or path.</param>
    /// <param name="fieldBuilder">The field builder action.</param>
    /// <returns>Instance of <see cref="QueryBuilder"/>.</returns>
    /// <exception cref="ArgumentException">Thrown when the field is null or empty.</exception>
    /// <exception cref="ArgumentNullException">Thrown when the fieldBuilder is null.</exception>
    public QueryBuilder AddField(string field, Action<FieldBuilder> fieldBuilder)
    {
        ArgumentNullException.ThrowIfNull(fieldBuilder);
        return AddFieldBuilderCore(field, Constants.DefaultFieldType, null, null, fieldBuilder);
    }

    /// <summary>
    ///     Adds a field to the query.
    /// </summary>
    /// <param name="field">Field name or path.</param>
    /// <param name="arguments">The arguments for the field.</param>
    /// <param name="subFields">The subfields for the field.</param>
    /// <param name="metadata">The field metadata.</param>
    /// <returns>Instance of <see cref="QueryBuilder"/>.</returns>
    /// <exception cref="ArgumentException">Thrown when the field is null or empty.</exception>
    public QueryBuilder AddField(string field, Dictionary<string, object?> arguments, string[] subFields, Dictionary<string, object?>? metadata = null)
    {
        SortedDictionary<string, object?>? sortedArgs = arguments?.Count > 0
            ? new SortedDictionary<string, object?>(arguments, StringComparer.OrdinalIgnoreCase)
            : null;
        return AddFieldCore(field, sortedArgs, subFields?.Select(subField => new FieldDefinition(subField)), metadata);
    }

    /// <summary>
    ///     Adds a field to the query.
    /// </summary>
    /// <param name="field">Field name or path.</param>
    /// <param name="arguments">The arguments for the field.</param>
    /// <param name="subFields">The subfields for the field.</param>
    /// <param name="metadata">The field metadata.</param>
    /// <returns>Instance of <see cref="QueryBuilder"/>.</returns>
    /// <exception cref="ArgumentException">Thrown when the field is null or empty.</exception>
    public QueryBuilder AddField(string field, Dictionary<string, object?> arguments, FieldDefinition[] subFields, Dictionary<string, object?>? metadata = null)
    {
        SortedDictionary<string, object?>? sortedArgs = arguments?.Count > 0
            ? new SortedDictionary<string, object?>(arguments, StringComparer.OrdinalIgnoreCase)
            : null;
        return AddFieldCore(field, sortedArgs, subFields, metadata);
    }

    /// <summary>
    ///     Adds a field with a specific type to the query.
    /// </summary>
    /// <param name="field">Field name or path</param>
    /// <param name="type">The field type</param>
    /// <returns>Instance of <see cref="QueryBuilder"/>.</returns>
    /// <exception cref="ArgumentException">Thrown when the field is null or empty.</exception>
    public QueryBuilder AddField(string field, string type)
        => AddFieldBuilderCore(field, type, null, null, _ => { });

    /// <summary>
    ///     Adds a field with a specific type to the query.
    /// </summary>
    /// <param name="field">Field name or path</param>
    /// <param name="type">The field type</param>
    /// <param name="metadata">The field metadata</param>
    /// <returns>Instance of <see cref="QueryBuilder"/>.</returns>
    /// <exception cref="ArgumentException">Thrown when the field is null or empty.</exception>
    public QueryBuilder AddField(string field, string type, Dictionary<string, object?>? metadata)
        => AddFieldBuilderCore(field, type, null, metadata, _ => { });

    /// <summary>
    ///     Adds a field to the query using a field builder with arguments.
    /// </summary>
    /// <param name="field">Field name or path</param>
    /// <param name="arguments">The arguments for the field</param>
    /// <param name="metadata">The field metadata</param>
    /// <param name="fieldBuilder">The field builder action</param>
    /// <returns>Instance of <see cref="QueryBuilder"/>.</returns>
    /// <exception cref="ArgumentException">Thrown when the field is null or empty.</exception>
    /// <exception cref="ArgumentNullException">Thrown when the fieldBuilder is null.</exception>
    public QueryBuilder AddField(string field, Dictionary<string, object?> arguments, Dictionary<string, object?>? metadata, Action<FieldBuilder> fieldBuilder)
    {
        ArgumentNullException.ThrowIfNull(fieldBuilder);
        SortedDictionary<string, object?>? sortedArgs = arguments?.Count > 0
            ? new SortedDictionary<string, object?>(arguments, StringComparer.OrdinalIgnoreCase)
            : null;
        return AddFieldBuilderCore(field, Constants.DefaultFieldType, sortedArgs, metadata, fieldBuilder);
    }

    /// <summary>
    ///     Adds a field to the query using a field builder with a specific type.
    /// </summary>
    /// <param name="field">Field name or path</param>
    /// <param name="type">The field type</param>
    /// <param name="metadata">The field metadata</param>
    /// <param name="fieldBuilder">The field builder action</param>
    /// <returns>Instance of <see cref="QueryBuilder"/>.</returns>
    /// <exception cref="ArgumentException">Thrown when the field is null or empty.</exception>
    /// <exception cref="ArgumentNullException">Thrown when the fieldBuilder is null.</exception>
    public QueryBuilder AddField(string field, string type, Dictionary<string, object?>? metadata, Action<FieldBuilder> fieldBuilder)
        => AddFieldBuilderCore(field, type, null, metadata, fieldBuilder);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private QueryBuilder AddFieldFastPath(string field)
    {
        if (string.IsNullOrWhiteSpace(field))
        {
            throw new ArgumentException("Field cannot be null or empty", nameof(field));
        }

        // ULTRA FAST PATH: Direct field creation for simple cases
        var fieldSpan = field.AsSpan();
        if (fieldSpan.IsSimpleField())
        {
            // Bypass FieldBuilder.Create for maximum performance
            if (!Definition.Fields.ContainsKey(field))
            {
                Definition.Fields[field] = new FieldDefinition(field, Constants.DefaultFieldType)
                {
                    Path = field
                };
            }
        }
        else
        {
            // Fallback to standard processing for complex fields
            FieldBuilder.Create(Definition.Fields, field, Constants.DefaultFieldType, null, null);
        }
        
        // Phase 3: Invalidate caches after field addition
        InvalidateLookupCaches();
        
        // Defer UpdateRootMapping - will be called when query is built/used
        return this;
    }

    /// <summary>
    /// Core implementation for adding fields using FieldBuilder pattern.
    /// </summary>
    /// <param name="field">Field name or path</param>
    /// <param name="fieldType">The field type</param>
    /// <param name="arguments">Optional arguments dictionary</param>
    /// <param name="metadata">Optional metadata dictionary</param>
    /// <param name="fieldBuilder">The field builder action</param>
    /// <returns>Current QueryBuilder instance for method chaining</returns>
    private QueryBuilder AddFieldBuilderCore(string field, string fieldType, SortedDictionary<string, object?>? arguments, Dictionary<string, object?>? metadata, Action<FieldBuilder> fieldBuilder)
    {
        if (string.IsNullOrWhiteSpace(field))
        {
            throw new ArgumentException("Field cannot be null or empty", nameof(field));
        }

        ArgumentNullException.ThrowIfNull(fieldBuilder);

        // FAST PATH: Only extract variables if arguments has content
        if (arguments?.Count > 0)
        {
            Helpers.ExtractVariablesFromValue(arguments, Definition.Variables);
        }

        // Use the provided field type
        var builder = FieldBuilder.Create(Definition.Fields, field, fieldType, arguments, metadata);
        fieldBuilder(builder);

        QueryMapInstance.UpdateRootMapping(_definition);
        return this;
    }

    public QueryBuilder Include(QueryBuilder queryBuilder) => IncludeImpl(queryBuilder.Definition);

    /// <summary>
    /// Core implementation for adding fields with optional arguments.
    /// </summary>
    /// <param name="field">Field name or path</param>
    /// <param name="arguments">Optional arguments dictionary</param>
    /// <param name="subFields">Optional array of sub-field definitions</param>
    /// <param name="metadata">Optional metadata dictionary</param>
    /// <returns>Current QueryBuilder instance for method chaining</returns>
    private QueryBuilder AddFieldCore(string field, SortedDictionary<string, object?>? arguments, IEnumerable<FieldDefinition>? subFields, Dictionary<string, object?>? metadata)
    {
        if (string.IsNullOrWhiteSpace(field))
            throw new ArgumentException("Field cannot be null or empty", nameof(field));

        var hasSubFields = subFields?.Any() == true;

        if (arguments is { Count: > 0 })
            Helpers.ExtractVariablesFromValue(arguments, Definition.Variables);

        var type = hasSubFields ? Constants.ObjectFieldType : Constants.DefaultFieldType;
        var builder = FieldBuilder.Create(Definition.Fields, field, type, arguments, metadata);

        if (!hasSubFields)
        {
            QueryMapInstance.UpdateRootMapping(_definition);
            // Phase 3: Invalidate caches after field addition
            InvalidateLookupCaches();
            return this;
        }

        foreach (var subField in subFields!)
            builder.AddField(subField);

        QueryMapInstance.UpdateRootMapping(_definition);
        // Phase 3: Invalidate caches after field addition
        InvalidateLookupCaches();
        return this;
    }

    /// <summary>
    /// Core implementation for including another query definition with optimized parameter passing.
    /// </summary>
    /// <param name="queryDefinition">Query definition to include (passed by reference for performance)</param>
    /// <returns>Current QueryBuilder instance for method chaining</returns>
    private QueryBuilder IncludeImpl(in QueryDefinition queryDefinition)
    {
        QueryMerger.MergeQuery(_definition, QueryMapInstance, this, in queryDefinition);
        
        // Phase 3: Invalidate lookup caches after merge since fields changed
        InvalidateLookupCaches();
        
        return this;
    }

    /// <summary>
    /// Phase 3 optimization: Invalidates both path and field lookup caches.
    /// Called after any modification to Definition.Fields to ensure cache consistency.
    /// </summary>
    private void InvalidateLookupCaches()
    {
        _pathIndex.Clear();
    }

    public QueryBuilder WithMetadata(Dictionary<string, object> metadata)
    {
        var mergedMetadata = Helpers.MergeMetadata(_definition._metadata, metadata);
        _definition.Metadata = mergedMetadata;

        return this;
    }

    /// <summary>
    /// Gets the path segments to reach a specific node within a query.
    /// </summary>
    /// <param name="queryName">The name of the query to find the path for.</param>
    /// <param name="nodePath">The optional node path within the query (e.g., "edges.node").</param>
    /// <returns>An array of path segments to reach the specified node.</returns>
    public string[] GetPathTo(string queryName, string? nodePath = null)
        => QueryMapInstance.GetPathTo(queryName, nodePath, _definition, _pathIndex);

    /// <summary>
    /// Gets the count of fields in the QueryDefinition.
    /// This represents the correct value with or without merge.
    /// </summary>
    internal int DefinitionsCount => Definition.Fields.Count;

    /// <inheritdoc cref="QueryBlock.ToString()"/>
    public override string ToString()
    {
        QueryMapInstance.UpdateRootMapping(_definition);
        return Definition.ToString();
    }

    public static implicit operator string(QueryBuilder query) => query.ToString();
}
