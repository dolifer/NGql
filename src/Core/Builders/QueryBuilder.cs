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
    ///     Creates a new <see cref="QueryBuilder"/> that renders as a GraphQL <c>mutation</c>.
    ///     The fluent surface (<c>AddField</c>, <c>Include</c>, etc.) is identical to the
    ///     query path; only the operation prefix differs at render time.
    /// </summary>
    /// <param name="name">The name of the mutation.</param>
    /// <returns>Instance of <see cref="QueryBuilder"/> in mutation mode.</returns>
    public static QueryBuilder CreateMutationBuilder(string name)
    {
        var definition = new QueryDefinition(name) { OperationType = OperationType.Mutation };
        return new(definition);
    }

    /// <summary>
    ///     Creates a new <see cref="QueryBuilder"/> that renders as a GraphQL <c>mutation</c>,
    ///     with a specific merging strategy.
    /// </summary>
    /// <param name="name">The name of the mutation.</param>
    /// <param name="mergingStrategy">The merging strategy to use.</param>
    /// <returns>Instance of <see cref="QueryBuilder"/> in mutation mode.</returns>
    public static QueryBuilder CreateMutationBuilder(string name, MergingStrategy mergingStrategy)
    {
        var definition = new QueryDefinition(name)
        {
            OperationType = OperationType.Mutation,
            MergingStrategy = mergingStrategy,
        };
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
        // Signature declares subFields non-nullable; the public API contract requires non-null.
        return AddFieldCore(field, sortedArgs, subFields.Select(subField => new FieldDefinition(subField)), metadata);
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

    /// <summary>
    ///     Adds a field to the query using a field builder with arguments. Convenience overload —
    ///     equivalent to passing <c>metadata: null</c> to the four-arg form.
    /// </summary>
    /// <param name="field">Field name or path</param>
    /// <param name="arguments">The arguments for the field</param>
    /// <param name="fieldBuilder">The field builder action</param>
    /// <returns>Instance of <see cref="QueryBuilder"/>.</returns>
    /// <exception cref="ArgumentException">Thrown when the field is null or empty.</exception>
    /// <exception cref="ArgumentNullException">Thrown when the fieldBuilder is null.</exception>
    public QueryBuilder AddField(string field, Dictionary<string, object?> arguments, Action<FieldBuilder> fieldBuilder)
        => AddField(field, arguments, metadata: null, fieldBuilder);

    /// <summary>
    ///     Adds a field with explicit sub-field names and a field builder action — useful when
    ///     the caller wants both static sub-fields AND a chance to add nested structure via the builder.
    /// </summary>
    /// <param name="field">Field name or path</param>
    /// <param name="subFields">The static sub-field names to seed into the field</param>
    /// <param name="fieldBuilder">The field builder action invoked after the static sub-fields are added</param>
    /// <returns>Instance of <see cref="QueryBuilder"/>.</returns>
    /// <exception cref="ArgumentException">Thrown when the field is null or empty.</exception>
    /// <exception cref="ArgumentNullException">Thrown when the fieldBuilder is null.</exception>
    public QueryBuilder AddField(string field, string[] subFields, Action<FieldBuilder> fieldBuilder)
    {
        ArgumentNullException.ThrowIfNull(fieldBuilder);
        return AddField(field, b =>
        {
            foreach (var sub in subFields)
            {
                b.AddField(sub);
            }

            fieldBuilder(b);
        });
    }

    /// <summary>
    ///     Declares a named GraphQL fragment at the operation's top level. The fragment is
    ///     referenced from one or more fields via <c>FieldBuilder.SpreadFragment(name)</c>;
    ///     the renderer emits <c>fragment Name on TypeName { … }</c> after the operation block.
    /// </summary>
    /// <param name="name">Fragment identifier. Used at spread sites (<c>...Name</c>); must be
    /// unique per operation. Calling <c>AddFragment</c> twice with the same name and type is
    /// idempotent — the second call merges fields into the existing fragment, like
    /// <see cref="FieldBuilder.OnType(string, Action{FieldBuilder})"/> does for inline fragments.</param>
    /// <param name="onType">The GraphQL type the fragment is declared on.</param>
    /// <param name="build">Action that populates the fragment's selection set. Receives a
    /// <see cref="FieldBuilder"/> so the same <c>AddField</c> / <c>OnType</c> idioms work as
    /// inside any other selection set.</param>
    /// <returns>Instance of <see cref="QueryBuilder"/>.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> or
    /// <paramref name="onType"/> is null or whitespace.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="build"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when a fragment with the same
    /// <paramref name="name"/> already exists with a different <paramref name="onType"/>.</exception>
    public QueryBuilder AddFragment(string name, string onType, Action<FieldBuilder> build)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Named fragment name cannot be null or whitespace.", nameof(name));
        }
        if (string.IsNullOrWhiteSpace(onType))
        {
            throw new ArgumentException("Named fragment type name cannot be null or whitespace.", nameof(onType));
        }
        ArgumentNullException.ThrowIfNull(build);

        var fragment = _definition.GetOrAddNamedFragment(name, onType);

        // Same synthetic-field trick OnType uses for inline fragments: build a temporary
        // FieldDefinition whose `_children` aliases the fragment's field store, so the lambda's
        // AddField calls write straight into the fragment without an extra copy step. The
        // `_fragments` and `_spreadFragments` fields are aliased the same way for nested
        // inline fragments and fragment-spread-inside-named-fragment cases.
        var fragmentSurface = new FieldDefinition(
            name: $"__named_fragment_{name}",
            type: Constants.DefaultFieldType)
        {
            _children = fragment.GetOrCreateFieldsStore(),
            _fragments = fragment._fragments,
            _spreadFragments = fragment._spreadFragments,
        };

        var inner = FieldBuilder.Create(fragmentSurface);
        build(inner);

        // Reflect any newly-created nested-fragment / spread-fragment maps back onto the named
        // fragment so subsequent AddFragment calls on the same name pick them up.
        fragment._fragments = fragmentSurface._fragments;
        fragment._spreadFragments = fragmentSurface._spreadFragments;

        return this;
    }

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

    /// <summary>
    /// Merges <paramref name="queryBuilder"/>'s fields and variables into this builder.
    /// Merge behavior is governed by this builder's <see cref="MergingStrategy"/>:
    /// <see cref="NGql.Core.MergingStrategy.MergeByDefault"/> appends fragments,
    /// <see cref="NGql.Core.MergingStrategy.MergeByFieldPath"/> merges compatible same-path
    /// fields and auto-aliases on argument conflict, and
    /// <see cref="NGql.Core.MergingStrategy.NeverMerge"/> always aliases the included fields
    /// as <c>name_1</c>, <c>name_2</c>, …
    /// </summary>
    /// <param name="queryBuilder">Builder whose fields will be merged into this one.</param>
    /// <returns>This builder, for chaining.</returns>
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
        // Fragments (named, inline, and spreads) are not yet handled by the merger — silently
        // dropping them would emit GraphQL with broken references, so we reject upfront. When
        // fragment merge semantics are designed (issue #20 follow-up), this guard goes away.
        ThrowIfIncomingHasFragments(queryDefinition);

        QueryMerger.MergeQuery(_definition, QueryMapInstance, this, in queryDefinition);

        // Phase 3: Invalidate lookup caches after merge since fields changed
        InvalidateLookupCaches();

        return this;
    }

    private static void ThrowIfIncomingHasFragments(in QueryDefinition queryDefinition)
    {
        if (queryDefinition._namedFragments is { Count: > 0 })
        {
            throw new NotSupportedException(
                "Include does not yet support queries that declare named fragments. " +
                "Inline the fragment's fields manually, or build the merged query without fragments.");
        }

        if (queryDefinition._fields is { Count: > 0 } && AnyFieldHasFragment(queryDefinition._fields.Values))
        {
            throw new NotSupportedException(
                "Include does not yet support queries that contain inline fragments or fragment spreads. " +
                "Build the merged query without fragments, or apply Include before adding fragments.");
        }
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Major Code Smell", "S3267:Loops should be simplified using the \"Where\" LINQ method",
        Justification = "Hot path on every Include call — a foreach with early return avoids the enumerator allocation that LINQ Any/Where would incur.")]
    private static bool AnyFieldHasFragment(IEnumerable<FieldDefinition> fields)
    {
        foreach (var field in fields)
        {
            if (FieldHasAnyFragment(field)) return true;
        }
        return false;
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Major Code Smell", "S3267:Loops should be simplified using the \"Where\" LINQ method",
        Justification = "Recursive walk over a Span<FieldDefinition>; LINQ does not span-iterate.")]
    private static bool FieldHasAnyFragment(FieldDefinition field)
    {
        if (field._fragments is { Count: > 0 }) return true;
        if (field._spreadFragments is { Count: > 0 }) return true;
        if (field._children is { Count: > 0 })
        {
            foreach (var child in field._children.AsSpan())
            {
                if (FieldHasAnyFragment(child)) return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Phase 3 optimization: Invalidates both path and field lookup caches.
    /// Called after any modification to Definition.Fields to ensure cache consistency.
    /// </summary>
    private void InvalidateLookupCaches()
    {
        _pathIndex.Clear();
    }

    /// <summary>
    /// Merges <paramref name="metadata"/> into the query definition's metadata bag, deeply
    /// combining nested dictionaries. Existing keys are overwritten by <paramref name="metadata"/>
    /// only when the new value is not itself a dictionary; nested dictionaries are recursively
    /// merged.
    /// </summary>
    /// <param name="metadata">Metadata to merge in.</param>
    /// <returns>This builder, for chaining.</returns>
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
