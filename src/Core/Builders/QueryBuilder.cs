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
    /// <param name="arguments">The arguments for the field.</param>
    /// <param name="metadata">The field metadata.</param>
    /// <returns>Instance of <see cref="QueryBuilder"/>.</returns>
    /// <exception cref="ArgumentException">Thrown when the field is null or empty.</exception>
    public QueryBuilder AddField(string field, Dictionary<string, object?>? arguments = null, Dictionary<string, object?>? metadata = null)
        => AddFieldImpl(field, arguments is null ? Constants.EmptyArguments : new SortedDictionary<string, object?>(arguments, StringComparer.OrdinalIgnoreCase), [], metadata);

    /// <summary>
    ///     Adds a field to the query.
    /// </summary>
    /// <param name="field">Field name or path.</param>
    /// <param name="subFields">The subfields for the field.</param>
    /// <param name="metadata">The field metadata.</param>
    /// <returns>Instance of <see cref="QueryBuilder"/>.</returns>
    /// <exception cref="ArgumentException">Thrown when the field is null or empty.</exception>
    public QueryBuilder AddField(string field, string[]? subFields, Dictionary<string, object?>? metadata = null)
        => AddFieldImpl(field, Constants.EmptyArguments, subFields, metadata);

    /// <summary>
    ///     Adds a field to the query.
    /// </summary>
    /// <param name="field">Field name or path.</param>
    /// <param name="subFields">The subfields for the field.</param>
    /// <param name="metadata">The field metadata.</param>
    /// <returns>Instance of <see cref="QueryBuilder"/>.</returns>
    /// <exception cref="ArgumentException">Thrown when the field is null or empty.</exception>
    public QueryBuilder AddField(string field, FieldDefinition[]? subFields, Dictionary<string, object?>? metadata = null)
        => AddFieldDefinitionImpl(field, Constants.EmptyArguments, subFields, metadata);

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

        // Preserve existing field type if it exists and is not default
        var fieldType = DetermineFieldType(field, hasSubFields: true);

        var builder = FieldBuilder.Create(Definition.Fields, field, fieldType);

        fieldBuilder.Invoke(builder);

        var updatedField = builder.Build();

        Definition.Fields[updatedField.Name] = updatedField;

        _queryMap.UpdateRootMapping(_definition);
        return this;
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
        => AddFieldImpl(field, new SortedDictionary<string, object?>(arguments, StringComparer.OrdinalIgnoreCase), subFields, metadata);

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
        => AddFieldDefinitionImpl(field, new SortedDictionary<string, object?>(arguments, StringComparer.OrdinalIgnoreCase), subFields, metadata);

    /// <summary>
    /// Adds a field to the query with type only.
    /// </summary>
    /// <param name="field">Field name or path</param>
    /// <param name="type">The field type</param>
    /// <returns>Instance of <see cref="QueryBuilder"/></returns>
    /// <exception cref="ArgumentException">Thrown when the field is null or empty.</exception>
    public QueryBuilder AddField(string field, string type)
        => AddFieldImpl(field, Constants.EmptyArguments, [], null);

    /// <summary>
    /// Adds a field to the query with type and metadata.
    /// </summary>
    /// <param name="field">Field name or path</param>
    /// <param name="type">The field type</param>
    /// <param name="metadata">The field metadata</param>
    /// <returns>Instance of <see cref="QueryBuilder"/></returns>
    /// <exception cref="ArgumentException">Thrown when the field is null or empty.</exception>
    public QueryBuilder AddField(string field, string type, Dictionary<string, object?>? metadata)
        => AddFieldImpl(field, Constants.EmptyArguments, [], metadata);

    /// <summary>
    /// Adds a field to the query with arguments and metadata using Action.
    /// </summary>
    /// <param name="field">Field name or path</param>
    /// <param name="arguments">The arguments for the field</param>
    /// <param name="metadata">The field metadata</param>
    /// <param name="fieldBuilder">The field builder action</param>
    /// <returns>Instance of <see cref="QueryBuilder"/></returns>
    /// <exception cref="ArgumentException">Thrown when the field is null or empty.</exception>
    /// <exception cref="ArgumentNullException">Thrown when the fieldBuilder is null.</exception>
    public QueryBuilder AddField(string field, Dictionary<string, object?> arguments, Dictionary<string, object?>? metadata, Action<FieldBuilder> fieldBuilder)
    {
        if (string.IsNullOrWhiteSpace(field))
        {
            throw new ArgumentException("Field cannot be null or empty", nameof(field));
        }

        ArgumentNullException.ThrowIfNull(fieldBuilder);

        var builder = FieldBuilder.Create([], field, Constants.DefaultFieldType, arguments is null ? Constants.EmptyArguments : new SortedDictionary<string, object?>(arguments, StringComparer.OrdinalIgnoreCase), metadata);
        fieldBuilder(builder);
        var fieldDefinition = builder.Build();

        _definition.Fields[fieldDefinition.Name] = fieldDefinition;
        _queryMap.UpdateRootMapping(_definition);

        return this;
    }

    /// <summary>
    /// Adds a field to the query with type, metadata and Action.
    /// </summary>
    /// <param name="field">Field name or path</param>
    /// <param name="type">The field type</param>
    /// <param name="metadata">The field metadata</param>
    /// <param name="fieldBuilder">The field builder action</param>
    /// <returns>Instance of <see cref="QueryBuilder"/></returns>
    /// <exception cref="ArgumentException">Thrown when the field is null or empty.</exception>
    /// <exception cref="ArgumentNullException">Thrown when the fieldBuilder is null.</exception>
    public QueryBuilder AddField(string field, string type, Dictionary<string, object?>? metadata, Action<FieldBuilder> fieldBuilder)
    {
        if (string.IsNullOrWhiteSpace(field))
        {
            throw new ArgumentException("Field cannot be null or empty", nameof(field));
        }

        ArgumentNullException.ThrowIfNull(fieldBuilder);

        var builder = FieldBuilder.Create([], field, type, null, metadata);
        fieldBuilder(builder);
        var fieldDefinition = builder.Build();

        _definition.Fields[fieldDefinition.Name] = fieldDefinition;
        _queryMap.UpdateRootMapping(_definition);

        return this;
    }

    public QueryBuilder Include(QueryBuilder queryBuilder) => IncludeImpl(queryBuilder.Definition);

    /// <summary>
    /// Core implementation for adding field definitions with optimized parameter passing.
    /// </summary>
    /// <param name="field">Field name or path</param>
    /// <param name="arguments">Normalized arguments dictionary (passed by reference for performance)</param>
    /// <param name="subFields">Optional array of sub-field definitions</param>
    /// <param name="metadata">Normalized metadata dictionary (passed by reference for performance)</param>
    /// <returns>Current QueryBuilder instance for method chaining</returns>
    private QueryBuilder AddFieldDefinitionImpl(string field, in SortedDictionary<string, object?> arguments, FieldDefinition[]? subFields, Dictionary<string, object?>? metadata)
    {
        if (string.IsNullOrWhiteSpace(field))
        {
            throw new ArgumentException("Field cannot be null or empty", nameof(field));
        }

        // Extract variables from arguments for the query definition
        Helpers.ExtractVariablesFromValue(arguments, Definition.Variables);

        // Determine field type based on presence of subfields and existing field type
        var type = DetermineFieldType(field, subFields?.Length > 0);

        var builder = FieldBuilder.Create(Definition.Fields, field, type, arguments, metadata);

        // Early return if no subfields to process
        if (subFields is null || subFields.Length == 0)
        {
            _queryMap.UpdateRootMapping(_definition);
            return this;
        }

        // Add all subfields to the builder
        foreach (var subField in subFields)
        {
            builder.AddField(subField);
        }

        _queryMap.UpdateRootMapping(_definition);
        return this;
    }

    /// <summary>
    /// Implementation for adding fields with string array subfields, converting them to FieldDefinitions.
    /// </summary>
    /// <param name="field">Field name or path</param>
    /// <param name="arguments">Normalized arguments dictionary (passed by reference for performance)</param>
    /// <param name="subFields">Optional array of subfield names</param>
    /// <param name="metadata">Normalized metadata dictionary (passed by reference for performance)</param>
    /// <returns>Current QueryBuilder instance for method chaining</returns>
    private QueryBuilder AddFieldImpl(string field, in SortedDictionary<string, object?> arguments, string[]? subFields, Dictionary<string, object?>? metadata)
    {
        // Convert string subfields to FieldDefinition objects
        var subFieldDefinitions = subFields?
            .Select(subField => new FieldDefinition(subField))
            .ToArray();

        return AddFieldDefinitionImpl(field, in arguments, subFieldDefinitions, metadata);
    }

    /// <summary>
    /// Core implementation for including another query definition with optimized parameter passing.
    /// </summary>
    /// <param name="queryDefinition">Query definition to include (passed by reference for performance)</param>
    /// <returns>Current QueryBuilder instance for method chaining</returns>
    private QueryBuilder IncludeImpl(in QueryDefinition queryDefinition)
    {
        QueryMerger.MergeQuery(_definition, _queryMap, in queryDefinition);
        return this;
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
        => _queryMap.GetPathTo(queryName, nodePath, _definition);

    /// <summary>
    /// Gets the count of fields in the QueryDefinition.
    /// This represents the correct value with or without merge.
    /// </summary>
    internal int DefinitionsCount => Definition.Fields.Count;

    /// <inheritdoc cref="QueryBlock.ToString()"/>
    public override string ToString() => Definition.ToString();

    public static implicit operator string(QueryBuilder query) => query.ToString();

    /// <summary>
    /// Determines the appropriate field type based on existing fields, explicit types, and subfield presence.
    /// </summary>
    private string DetermineFieldType(string field, bool hasSubFields)
    {
        // Parse type from field path to check for explicit type
        var fieldSpan = field.AsSpan();
        Helpers.ParseFieldTypeFromPath(fieldSpan, Constants.DefaultFieldType, out var parsedType);
        var hasExplicitType = !string.Equals(parsedType, Constants.DefaultFieldType, StringComparison.OrdinalIgnoreCase);

        // Find existing field - check the actual target field, not just root
        var existingField = Helpers.FindExistingFieldByPath(Definition.Fields, field);

        if (!hasSubFields)
        {
            return Constants.DefaultFieldType;
        }

        // Priority 1: Preserve existing explicit types (non-default, non-object)
        if (existingField?.Type != null &&
            !string.Equals(existingField.Type, Constants.DefaultFieldType, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(existingField.Type, Constants.ObjectFieldType, StringComparison.OrdinalIgnoreCase))
        {
            return existingField.Type;
        }

        // Priority 2: Use newly specified explicit type
        if (hasExplicitType)
        {
            return parsedType;
        }

        // Priority 3: Preserve existing object type
        if (existingField?.Type == Constants.ObjectFieldType)
        {
            return Constants.ObjectFieldType;
        }

        // Priority 4: Default to object type for subfields
        return Constants.ObjectFieldType;
    }
}
