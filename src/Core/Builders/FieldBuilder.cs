using System.Runtime.CompilerServices;
using NGql.Core.Abstractions;
using NGql.Core.Extensions;

namespace NGql.Core.Builders;

/// <summary>
/// Fluent helper passed to the <c>fieldBuilder</c> action overloads of
/// <see cref="QueryBuilder.AddField(string, Action{FieldBuilder})"/>. Use it to add
/// nested fields, configure a parent field's type/arguments/metadata, or compose
/// sub-trees without leaving the outer chain.
/// </summary>
public sealed class FieldBuilder
{
    private FieldDefinition _fieldDefinition;

    private FieldBuilder(FieldDefinition fieldDefinition)
    {
        _fieldDefinition = fieldDefinition;
    }

    /// <summary>
    /// Adds a field definition to the builder.
    /// </summary>
    /// <param name="fieldDefinition">The field definition to add.</param>
    /// <returns>The current FieldBuilder instance for method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when fieldDefinition is null.</exception>
    public FieldBuilder AddField(FieldDefinition fieldDefinition)
    {
        ArgumentNullException.ThrowIfNull(fieldDefinition);
        // Dotted names are valid here — they are expanded into nested fields by FieldFactory,
        // matching the string-overload behavior. Validate each segment individually.
        ValidateFieldNameSegments(fieldDefinition.Name.AsSpan());
        var arguments = fieldDefinition._arguments;

        // FieldDefinition._type is always set non-null by every constructor path.
        FieldFactory.GetOrAddField(_fieldDefinition, fieldDefinition.Name, fieldDefinition._type!, arguments, _fieldDefinition.Path, fieldDefinition.Metadata);
        return this;
    }

    /// <summary>
    /// Adds a field with nested subfields, optional arguments, and metadata to the builder.
    /// This creates an object-type field that contains the specified subfields.
    /// </summary>
    /// <param name="fieldName">The name of the parent field to add. Supports dotted notation for nested fields.</param>
    /// <param name="subFields">Array of subfield names to add under this field. Each subfield will have default String type.</param>
    /// <param name="arguments">Optional GraphQL arguments for the parent field (e.g., filtering, pagination parameters).</param>
    /// <param name="metadata">Optional metadata dictionary to associate with the parent field.</param>
    /// <returns>The current FieldBuilder instance for method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when fieldName or subFields is null.</exception>
    /// <exception cref="ArgumentException">Thrown when fieldName is empty or subFields array is empty.</exception>
    public FieldBuilder AddField(string fieldName, string[] subFields, Dictionary<string, object?>? arguments = null, Dictionary<string, object?>? metadata = null)
        => AddFieldCore(fieldName, Constants.ObjectFieldType, arguments, subFields, metadata);

    /// <summary>
    /// Adds a field with a specific type, GraphQL arguments, and optional metadata to the builder.
    /// </summary>
    /// <param name="fieldName">The name of the field to add. Supports dotted notation for nested fields.</param>
    /// <param name="type">The GraphQL type of the field (e.g., "String!", "[String]", custom types).</param>
    /// <param name="arguments">GraphQL arguments for the field such as filters, pagination, or custom parameters.</param>
    /// <param name="metadata">Optional metadata dictionary to associate with the field.</param>
    /// <returns>The current FieldBuilder instance for method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when fieldName or type is null.</exception>
    /// <example>
    /// <code>
    /// builder.AddField("users", "User", new Dictionary&lt;string, object?&gt; { ["first"] = 10 });
    /// </code>
    /// </example>
    public FieldBuilder AddField(string fieldName, string type, Dictionary<string, object?>? arguments, Dictionary<string, object?>? metadata = null)
        => AddFieldCore(fieldName, type, arguments, metadata: metadata);

    /// <summary>
    /// Adds a field with a specific type, nested subfields, and optional metadata to the builder.
    /// </summary>
    /// <param name="fieldName">The name of the parent field to add. Supports dotted notation for nested fields.</param>
    /// <param name="type">The GraphQL type of the parent field (typically an object type or array type).</param>
    /// <param name="subFields">Array of subfield names to add under this field. Each subfield will have default String type.</param>
    /// <param name="metadata">Optional metadata dictionary to associate with the parent field.</param>
    /// <returns>The current FieldBuilder instance for method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when fieldName, type, or subFields is null.</exception>
    /// <example>
    /// <code>
    /// builder.AddField("user", "User", new[] { "id", "name", "email" });
    /// </code>
    /// </example>
    public FieldBuilder AddField(string fieldName, string type, string[] subFields, Dictionary<string, object?>? metadata = null)
        => AddFieldCore(fieldName, type, subFields: subFields, metadata: metadata);

    /// <summary>
    /// Adds a field with GraphQL arguments to the builder. The field will have the default String type.
    /// </summary>
    /// <param name="fieldName">The name of the field to add. Supports dotted notation for nested fields.</param>
    /// <param name="arguments">GraphQL arguments for the field such as filters, pagination, or custom parameters.</param>
    /// <returns>The current FieldBuilder instance for method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when fieldName is null.</exception>
    /// <example>
    /// <code>
    /// builder.AddField("searchResults", new SortedDictionary&lt;string, object?&gt; { ["query"] = "GraphQL" });
    /// </code>
    /// </example>
    public FieldBuilder AddField(string fieldName, Dictionary<string, object?>? arguments)
        => AddFieldCore(fieldName, arguments: arguments);

    /// <summary>
    /// Adds a field with GraphQL arguments and metadata to the builder. The field will have the default String type.
    /// </summary>
    /// <param name="fieldName">The name of the field to add. Supports dotted notation for nested fields.</param>
    /// <param name="arguments">GraphQL arguments for the field such as filters, pagination, or custom parameters.</param>
    /// <param name="metadata">Optional metadata dictionary to associate with the field for custom processing.</param>
    /// <returns>The current FieldBuilder instance for method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when fieldName is null.</exception>
    public FieldBuilder AddField(string fieldName, Dictionary<string, object?>? arguments, Dictionary<string, object?>? metadata)
        => AddFieldCore(fieldName, arguments: arguments, metadata: metadata);

    /// <summary>
    /// Adds a field with a specific type, nested subfields, GraphQL arguments, and metadata to the builder.
    /// This is the most comprehensive overload that supports all field configuration options.
    /// </summary>
    /// <param name="fieldName">The name of the parent field to add. Supports dotted notation for nested fields.</param>
    /// <param name="type">The GraphQL type of the parent field (typically an object type or array type).</param>
    /// <param name="subFields">Array of subfield names to add under this field. Each subfield will have default String type.</param>
    /// <param name="arguments">GraphQL arguments for the parent field such as filters, pagination, or custom parameters.</param>
    /// <param name="metadata">Optional metadata dictionary to associate with the parent field for custom processing.</param>
    /// <returns>The current FieldBuilder instance for method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when fieldName, type, or subFields is null.</exception>
    public FieldBuilder AddField(string fieldName, string type, string[] subFields, Dictionary<string, object?>? arguments, Dictionary<string, object?>? metadata)
        => AddFieldCore(fieldName, type, arguments, subFields, metadata);

    /// <summary>
    /// Adds a field with a nested builder action for configuring subfields dynamically.
    /// The field will have the default String type and allows fluent configuration of nested fields.
    /// </summary>
    /// <param name="fieldName">The name of the field to add. Supports dotted notation for nested fields.</param>
    /// <param name="action">A delegate that receives a FieldBuilder instance for configuring nested fields within this field.</param>
    /// <returns>The current FieldBuilder instance for method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when fieldName or action is null.</exception>
    /// <example>
    /// <code>
    /// builder.AddField("user", user => {
    ///     user.AddField("id");
    ///     user.AddField("profile", profile => {
    ///         profile.AddField("name");
    ///         profile.AddField("email");
    ///     });
    /// });
    /// </code>
    /// </example>
    public FieldBuilder AddField(string fieldName, Action<FieldBuilder> action)
        => AddFieldCore(fieldName, action: action);

    /// <summary>
    /// Adds a field with metadata and a nested builder action for configuring subfields dynamically.
    /// The field will have the default String type.
    /// </summary>
    /// <param name="fieldName">The name of the field to add. Supports dotted notation for nested fields.</param>
    /// <param name="metadata">Optional metadata dictionary to associate with the field for custom processing.</param>
    /// <param name="action">A delegate that receives a FieldBuilder instance for configuring nested fields within this field.</param>
    /// <returns>The current FieldBuilder instance for method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when fieldName or action is null.</exception>
    public FieldBuilder AddField(string fieldName, Dictionary<string, object?>? metadata, Action<FieldBuilder> action)
        => AddFieldCore(fieldName, metadata: metadata, action: action);

    /// <summary>
    /// Adds a field with a specific type and a nested builder action for configuring subfields dynamically.
    /// </summary>
    /// <param name="fieldName">The name of the field to add. Supports dotted notation for nested fields.</param>
    /// <param name="type">The GraphQL type of the field (typically an object type when using nested actions).</param>
    /// <param name="action">A delegate that receives a FieldBuilder instance for configuring nested fields within this field.</param>
    /// <returns>The current FieldBuilder instance for method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when fieldName, type, or action is null.</exception>
    /// <example>
    /// <code>
    /// builder.AddField("posts", "[Post]", posts => {
    ///     posts.AddField("id", "ID!");
    ///     posts.AddField("title");
    ///     posts.AddField("author", "User", author => {
    ///         author.AddField("name");
    ///     });
    /// });
    /// </code>
    /// </example>
    public FieldBuilder AddField(string fieldName, string type, Action<FieldBuilder> action)
        => AddFieldCore(fieldName, type, action: action);

    /// <summary>
    /// Adds a field with a specific type, metadata, and a nested builder action for configuring subfields dynamically.
    /// </summary>
    /// <param name="fieldName">The name of the field to add. Supports dotted notation for nested fields.</param>
    /// <param name="type">The GraphQL type of the field (typically an object type when using nested actions).</param>
    /// <param name="metadata">Optional metadata dictionary to associate with the field for custom processing.</param>
    /// <param name="action">A delegate that receives a FieldBuilder instance for configuring nested fields within this field.</param>
    /// <returns>The current FieldBuilder instance for method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when fieldName, type, or action is null.</exception>
    public FieldBuilder AddField(string fieldName, string type, Dictionary<string, object?>? metadata, Action<FieldBuilder> action)
        => AddFieldCore(fieldName, type, metadata: metadata, action: action);

    /// <summary>
    /// Adds a field with GraphQL arguments, metadata, and a nested builder action for configuring subfields dynamically.
    /// The field will have the default String type.
    /// </summary>
    /// <param name="fieldName">The name of the field to add. Supports dotted notation for nested fields.</param>
    /// <param name="arguments">GraphQL arguments for the field such as filters, pagination, or custom parameters.</param>
    /// <param name="metadata">Optional metadata dictionary to associate with the field for custom processing.</param>
    /// <param name="action">A delegate that receives a FieldBuilder instance for configuring nested fields within this field.</param>
    /// <returns>The current FieldBuilder instance for method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when fieldName or action is null.</exception>
    public FieldBuilder AddField(string fieldName, Dictionary<string, object?>? arguments, Dictionary<string, object?>? metadata, Action<FieldBuilder> action)
        => AddFieldCore(fieldName, arguments: arguments, metadata: metadata, action: action);

    /// <summary>
    /// Adds a field with a specific type, GraphQL arguments, metadata, and a nested builder action for configuring subfields dynamically.
    /// This is the most comprehensive action-based overload that supports all field configuration options.
    /// </summary>
    /// <param name="fieldName">The name of the field to add. Supports dotted notation for nested fields.</param>
    /// <param name="type">The GraphQL type of the field (typically an object type when using nested actions).</param>
    /// <param name="arguments">GraphQL arguments for the field such as filters, pagination, or custom parameters.</param>
    /// <param name="metadata">Optional metadata dictionary to associate with the field for custom processing.</param>
    /// <param name="action">A delegate that receives a FieldBuilder instance for configuring nested fields within this field.</param>
    /// <returns>The current FieldBuilder instance for method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when fieldName, type, or action is null.</exception>
    public FieldBuilder AddField(string fieldName, string type, Dictionary<string, object?>? arguments, Dictionary<string, object?>? metadata, Action<FieldBuilder> action)
        => AddFieldCore(fieldName, type, arguments, metadata: metadata, action: action);

    /// <summary>
    /// Adds a field with predefined subfields and a nested builder action for additional dynamic configuration.
    /// The field will have an object type and includes both the specified subfields and any fields configured via the action.
    /// </summary>
    /// <param name="fieldName">The name of the parent field to add. Supports dotted notation for nested fields.</param>
    /// <param name="subFields">Array of subfield names to add under this field. Each subfield will have a default String type.</param>
    /// <param name="action">A delegate that receives a FieldBuilder instance for configuring additional nested fields within this field.</param>
    /// <returns>The current FieldBuilder instance for method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when fieldName, subFields, or action is null.</exception>
    public FieldBuilder AddField(string fieldName, string[] subFields, Action<FieldBuilder> action)
        => AddFieldCore(fieldName, Constants.ObjectFieldType, subFields: subFields, action: action);

    /// <summary>
    /// Adds a field with predefined subfields, metadata, and a nested builder action for additional dynamic configuration.
    /// The field will have an object type.
    /// </summary>
    /// <param name="fieldName">The name of the parent field to add. Supports dotted notation for nested fields.</param>
    /// <param name="subFields">Array of subfield names to add under this field. Each subfield will have a default String type.</param>
    /// <param name="metadata">Optional metadata dictionary to associate with the parent field for custom processing.</param>
    /// <param name="action">A delegate that receives a FieldBuilder instance for configuring additional nested fields within this field.</param>
    /// <returns>The current FieldBuilder instance for method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when fieldName, subFields, or action is null.</exception>
    public FieldBuilder AddField(string fieldName, string[] subFields, Dictionary<string, object?>? metadata, Action<FieldBuilder> action)
        => AddFieldCore(fieldName, Constants.ObjectFieldType, subFields: subFields, metadata: metadata, action: action);

    /// <summary>
    /// Adds a field with a specific type, predefined subfields, and a nested builder action for additional dynamic configuration.
    /// </summary>
    /// <param name="fieldName">The name of the parent field to add. Supports dotted notation for nested fields.</param>
    /// <param name="type">The GraphQL type of the parent field (typically an object type or array type).</param>
    /// <param name="subFields">Array of subfield names to add under this field. Each subfield will have a default String type.</param>
    /// <param name="action">A delegate that receives a FieldBuilder instance for configuring additional nested fields within this field.</param>
    /// <returns>The current FieldBuilder instance for method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when fieldName, type, subFields, or action is null.</exception>
    public FieldBuilder AddField(string fieldName, string type, string[] subFields, Action<FieldBuilder> action)
        => AddFieldCore(fieldName, type, subFields: subFields, action: action);

    /// <summary>
    /// Adds a field with a specific type, predefined subfields, metadata, and a nested builder action for additional dynamic configuration.
    /// </summary>
    /// <param name="fieldName">The name of the parent field to add. Supports dotted notation for nested fields.</param>
    /// <param name="type">The GraphQL type of the parent field (typically an object type or array type).</param>
    /// <param name="subFields">Array of subfield names to add under this field. Each subfield will have a default String type.</param>
    /// <param name="metadata">Optional metadata dictionary to associate with the parent field for custom processing.</param>
    /// <param name="action">A delegate that receives a FieldBuilder instance for configuring additional nested fields within this field.</param>
    /// <returns>The current FieldBuilder instance for method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when fieldName, type, subFields, or action is null.</exception>
    public FieldBuilder AddField(string fieldName, string type, string[] subFields, Dictionary<string, object?>? metadata, Action<FieldBuilder> action)
        => AddFieldCore(fieldName, type, subFields: subFields, metadata: metadata, action: action);

    /// <summary>
    /// Adds a field with predefined subfields, GraphQL arguments, metadata, and a nested builder action for additional dynamic configuration.
    /// The field will have an object type. This is the most comprehensive subFields + action overload.
    /// </summary>
    /// <param name="fieldName">The name of the parent field to add. Supports dotted notation for nested fields.</param>
    /// <param name="subFields">Array of subfield names to add under this field. Each subfield will have a default String type.</param>
    /// <param name="arguments">GraphQL arguments for the parent field such as filters, pagination, or custom parameters.</param>
    /// <param name="metadata">Optional metadata dictionary to associate with the parent field for custom processing.</param>
    /// <param name="action">A delegate that receives a FieldBuilder instance for configuring additional nested fields within this field.</param>
    /// <returns>The current FieldBuilder instance for method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when fieldName, subFields, or action is null.</exception>
    public FieldBuilder AddField(string fieldName, string[] subFields, Dictionary<string, object?>? arguments, Dictionary<string, object?>? metadata, Action<FieldBuilder> action)
        => AddFieldCore(fieldName, Constants.ObjectFieldType, arguments, subFields, metadata, action);

    /// <summary>
    /// Adds a field with a specific type, predefined subfields, GraphQL arguments, metadata, and a nested builder action for additional dynamic configuration.
    /// This is the ultimate comprehensive overload that supports all possible field configuration options, including both predefined subfields and dynamic configuration.
    /// </summary>
    /// <param name="fieldName">The name of the parent field to add. Supports dotted notation for nested fields.</param>
    /// <param name="type">The GraphQL type of the parent field (typically an object type or array type).</param>
    /// <param name="subFields">Array of subfield names to add under this field. Each subfield will have a default String type.</param>
    /// <param name="arguments">GraphQL arguments for the parent field such as filters, pagination, or custom parameters.</param>
    /// <param name="metadata">Optional metadata dictionary to associate with the parent field for custom processing.</param>
    /// <param name="action">A delegate that receives a FieldBuilder instance for configuring additional nested fields within this field.</param>
    /// <returns>The current FieldBuilder instance for method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when fieldName, type, subFields, or action is null.</exception>
    /// <example>
    /// <code>
    /// builder.AddField("users", "[User]", new[] { "id", "email" }, 
    ///     new SortedDictionary&lt;string, object?&gt; { ["first"] = 10 },
    ///     new Dictionary&lt;string, object?&gt; { ["cached"] = true },
    ///     users => {
    ///         users.AddField("profile", "Profile", profile => {
    ///             profile.AddField("name");
    ///             profile.AddField("avatar", "String");
    ///         });
    ///     });
    /// </code>
    /// </example>
    public FieldBuilder AddField(string fieldName, string type, string[] subFields, Dictionary<string, object?>? arguments, Dictionary<string, object?>? metadata, Action<FieldBuilder> action)
        => AddFieldCore(fieldName, type, arguments, subFields, metadata, action);

    /// <summary>
    /// Adds a simple field with an optional type to the builder. This is the most basic overload for adding scalar fields.
    /// </summary>
    /// <param name="fieldName">The name of the field to add. Supports dotted notation for nested fields (e.g., "user.profile.name").</param>
    /// <param name="type">The GraphQL type of the field. Defaults to "String" if not specified. Common types include "String", "Int", "Boolean", "ID", "Float", etc.</param>
    /// <returns>The current FieldBuilder instance for method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when fieldName is null.</exception>
    /// <exception cref="ArgumentException">Thrown when fieldName is empty or whitespace.</exception>
    /// <example>
    /// <code>
    /// builder.AddField("name");                    // String field (default)
    /// builder.AddField("age", "Int");              // Integer field
    /// builder.AddField("isActive", "Boolean");     // Boolean field
    /// builder.AddField("user.profile.email");     // Nested field with dotted notation
    /// </code>
    /// </example>
    public FieldBuilder AddField(string fieldName, string type = Constants.DefaultFieldType)
        => AddFieldCore(fieldName, type);

    /// <summary>
    /// Core method that handles all field addition logic - simplified and optimized
    /// </summary>
    /// <param name="fieldName">The name of the field.</param>
    /// <param name="type">The type of the field (optional).</param>
    /// <param name="arguments">The arguments for the field (optional).</param>
    /// <param name="subFields">The subfields for the field (optional).</param>
    /// <param name="metadata">The metadata for the field (optional).</param>
    /// <param name="action">The action to configure nested fields (optional).</param>
    /// <returns>The current FieldBuilder instance for method chaining.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private FieldBuilder AddFieldCore(string fieldName, string? type = null, Dictionary<string, object?>? arguments = null, 
        string[]? subFields = null, Dictionary<string, object?>? metadata = null, Action<FieldBuilder>? action = null)
    {
        ValidateFieldNameSegments(fieldName.AsSpan());
        var fieldType = type ?? Constants.DefaultFieldType;
        var field = FieldFactory.GetOrAddField(_fieldDefinition, fieldName, fieldType, arguments, _fieldDefinition.Path, metadata);

        // FAST PATH: Skip subFields processing if array is null or empty
        if (subFields?.Length > 0)
        {
            foreach (var subField in subFields)
            {
                FieldFactory.GetOrAddField(field, subField, Constants.DefaultFieldType, null, field.Path);
            }
        }

        // FAST PATH: Skip action processing if null
        if (action != null)
        {
            var fieldBuilder = new FieldBuilder(field);
            action(fieldBuilder);
            // GetOrAddField above already added `field` as a child of _fieldDefinition,
            // so _fieldDefinition._children is non-null at this point.
            _fieldDefinition._children!.Set(field.Name, fieldBuilder._fieldDefinition);
        }

        return this;
    }

    /// <summary>
    /// Creates a new FieldBuilder instance for the specified field with type, arguments, and metadata.
    /// </summary>
    /// <param name="fieldDefinitions">The collection of existing field definitions.</param>
    /// <param name="fieldName">The name of the field to create.</param>
    /// <param name="type">The type of the field.</param>
    /// <param name="arguments">The arguments for the field.</param>
    /// <param name="metadata">The metadata for the field.</param>
    /// <returns>A new FieldBuilder instance.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FieldBuilder Create(SortedDictionary<string, FieldDefinition> fieldDefinitions, string fieldName, string type = Constants.DefaultFieldType, IDictionary<string, object?>? arguments = null, Dictionary<string, object?>? metadata = null)
    {
        // FAIL-FAST: Use empty arguments if null or empty
        var argumentsToUse = arguments is { Count: > 0 } ? arguments : null;

        // Use FieldFactory for field creation
        var rootField = FieldFactory.GetOrAddField(fieldDefinitions, fieldName, type, argumentsToUse, null, metadata);

        var fieldBuilder = new FieldBuilder(rootField);

        return fieldBuilder;
    }

    /// <summary>
    /// Creates a new FieldBuilder instance from an existing field definition.
    /// </summary>
    /// <param name="fieldDefinition">The field definition to create the builder from.</param>
    /// <returns>A new FieldBuilder instance.</returns>
    public static FieldBuilder Create(FieldDefinition fieldDefinition) => new(fieldDefinition);

    /// <summary>
    /// Builds and returns the final field definition.
    /// </summary>
    /// <returns>The constructed FieldDefinition.</returns>
    public FieldDefinition Build() => _fieldDefinition;

    /// <summary>
    /// Recursively creates and merges field definitions into the target field collection.
    /// This method handles field merging, argument consolidation, and nested field creation.
    /// </summary>
    /// <param name="fields">Target field collection</param>
    /// <param name="fieldDefinition">Field definition to create/merge</param>
    private static void RecursiveCreateField(SortedDictionary<string, FieldDefinition> fields, FieldDefinition fieldDefinition)
    {
        var parentField = FieldFactory.CreateOrMergeField(fields, fieldDefinition);

        // Recursively process all child fields using direct span iteration (zero-alloc)
        if (fieldDefinition._children != null)
        {
            var childrenSpan = fieldDefinition._children.AsSpan();
            parentField._children ??= new FieldChildren();
            var parentChildren = parentField._children;
            for (int i = 0; i < childrenSpan.Length; i++)
            {
                RecursiveCreateField(parentChildren, childrenSpan[i]);
            }
        }
    }

    private static void RecursiveCreateField(FieldChildren children, FieldDefinition fieldDefinition)
    {
        var parentField = FieldFactory.CreateOrMergeField(children, fieldDefinition);

        // Recursively process all child fields using direct span iteration (zero-alloc)
        if (fieldDefinition._children != null)
        {
            var childrenSpan = fieldDefinition._children.AsSpan();
            parentField._children ??= new FieldChildren();
            var parentChildren = parentField._children;
            for (int i = 0; i < childrenSpan.Length; i++)
            {
                RecursiveCreateField(parentChildren, childrenSpan[i]);
            }
        }
    }

    internal static void Include(SortedDictionary<string, FieldDefinition> fields, FieldDefinition fieldDefinition)
        => RecursiveCreateField(fields, fieldDefinition);

    private static void ValidateFieldNameSegments(ReadOnlySpan<char> fieldName)
    {
        if (fieldName.IsEmpty)
            throw new ArgumentException("Field name cannot be empty.", nameof(fieldName));

        // Validate each dot-separated segment individually
        while (!fieldName.IsEmpty)
        {
            var dotIndex = fieldName.IndexOf('.');
            var segment = dotIndex >= 0 ? fieldName[..dotIndex] : fieldName;
            if (!segment.IsEmpty)
                Helpers.ValidateFieldName(segment);
            fieldName = dotIndex >= 0 ? fieldName[(dotIndex + 1)..] : ReadOnlySpan<char>.Empty;
        }
    }

    /// <summary>
    /// Sets an alias for the current field.
    /// </summary>
    /// <param name="alias">The alias to set for the field.</param>
    /// <returns>The current FieldBuilder instance for method chaining.</returns>
    public FieldBuilder WithAlias(string alias)
    {
        _fieldDefinition = _fieldDefinition with { Alias = alias };

        return this;
    }

    /// <summary>
    /// Sets the type for the current field.
    /// </summary>
    /// <param name="type">The type to set for the field.</param>
    /// <returns>The current FieldBuilder instance for method chaining.</returns>
    public FieldBuilder WithType(string type)
    {
        _fieldDefinition = _fieldDefinition with { Type = type };

        return this;
    }

    /// <summary>
    /// Sets or merges metadata for the current field.
    /// </summary>
    /// <param name="metadata">The metadata to set or merge with existing metadata.</param>
    /// <returns>The current FieldBuilder instance for method chaining.</returns>
    public FieldBuilder WithMetadata(Dictionary<string, object> metadata)
    {
        var mergedMetadata = Helpers.MergeMetadata(_fieldDefinition._metadata, metadata);

        _fieldDefinition.Metadata = mergedMetadata;

        return this;
    }

    /// <summary>
    /// Adds or updates an argument for the current field.
    /// </summary>
    /// <param name="key">The argument key.</param>
    /// <param name="value">The argument value.</param>
    /// <returns>The current FieldBuilder instance for method chaining.</returns>
    public FieldBuilder Where(string key, object? value)
    {
        // Ensure that _arguments exist (lazy initialization)
        if (_fieldDefinition._arguments is null)
        {
            _fieldDefinition = _fieldDefinition with { _arguments = new(StringComparer.OrdinalIgnoreCase) };
        }

        // Determine the final value: merge dictionaries if both are dictionaries, otherwise set/override
        _fieldDefinition._arguments[key] = _fieldDefinition._arguments.TryGetValue(key, out var existingValue)
            ? (existingValue, value) switch
            {
                // Both values are dictionaries - merge them recursively
                (IDictionary<string, object?> existingDict, IDictionary<string, object?> newDict)
                    => Helpers.MergeNullableDictionaries(existingDict, newDict),

                // Not dictionaries or mixed types - override with new value
                _ => value
            }
            : value; // Key doesn't exist - set the value

        return this;
    }
}
