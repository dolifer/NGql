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

    // Tracks the enclosing FieldBuilder for nested Action<FieldBuilder> scopes (null for a root
    // builder, e.g. one created via QueryBuilder.AddField or a fragment surface). FieldDefinition
    // itself carries no parent back-pointer — adding one would ripple through Equals/DeepClone/
    // `with` semantics — so this is the only place a captured, since-detached FieldBuilder (see
    // Where()'s remarks) can still discover which ancestors' memoized deep fingerprints it must
    // invalidate.
    private readonly FieldBuilder? _parent;

    // The owning operation's variable set, threaded down from QueryBuilder.AddField(...) at the
    // root and inherited by every nested Action<FieldBuilder> scope. Null for a FieldBuilder with
    // no owning operation (the public FieldBuilder.Create(FieldDefinition) factory, used
    // standalone e.g. by tests or FieldFactory-adjacent code) — IncludeIf(Variable)/SkipIf(Variable)
    // still attach the directive in that case, they simply have nothing to promote the variable
    // into.
    private readonly SortedSet<Variable>? _variableSink;

    private FieldBuilder(FieldDefinition fieldDefinition, FieldBuilder? parent = null, SortedSet<Variable>? variableSink = null)
    {
        _fieldDefinition = fieldDefinition;
        if (parent is null) fieldDefinition.EnsureMergeMemoTracker();
        _parent = parent;
        _variableSink = variableSink ?? parent?._variableSink;
    }

    // Clears the memoized deep-fingerprint/subtree-has-arguments caches on every ancestor in this
    // builder's nesting chain, walking from the immediate parent up to the root. Used by mutation
    // sites (Where()) that can run long after the Action<FieldBuilder> that produced this builder
    // has returned — at which point AddFieldCore's own cascading ClearMergeMemo calls (one per
    // nesting level, run as each action unwinds) have already happened and cannot see this later
    // mutation. Idempotent and cheap: ClearMergeMemo is O(1) per node, and a chain of unrelated
    // Where() calls simply repeats the same small walk.
    private void ClearAncestorMergeMemos()
    {
        var ancestor = _parent;
        while (ancestor != null)
        {
            ancestor._fieldDefinition.ClearMergeMemo();
            ancestor = ancestor._parent;
        }
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
    /// Adds a field with arguments, then nested subfields and optional metadata. This is the args-first
    /// counterpart to <see cref="AddField(string, string[], Dictionary{string, object?}?, Dictionary{string, object?}?)"/>
    /// and matches the parameter order used by <see cref="QueryBuilder.AddField(string, Dictionary{string, object?}, string[], Dictionary{string, object?}?)"/>.
    /// </summary>
    /// <param name="fieldName">The name of the parent field to add.</param>
    /// <param name="arguments">GraphQL arguments for the parent field.</param>
    /// <param name="subFields">Array of subfield names to add under this field.</param>
    /// <param name="metadata">Optional metadata dictionary to associate with the parent field.</param>
    /// <returns>The current FieldBuilder instance for method chaining.</returns>
    public FieldBuilder AddField(string fieldName, Dictionary<string, object?>? arguments, string[] subFields, Dictionary<string, object?>? metadata = null)
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
    /// Adds a field with GraphQL arguments and a nested builder action for configuring subfields dynamically.
    /// The field will have the default String type.
    /// </summary>
    /// <remarks>
    /// <b>Behavior change in 2.1:</b> the dictionary at this position is now interpreted as
    /// <c>arguments</c>, matching the conventions of <see cref="QueryBuilder.AddField(string, Dictionary{string, object?}, Action{FieldBuilder})"/>
    /// and the rest of the <see cref="FieldBuilder"/> args-first overloads. In NGql 2.0 the same
    /// signature was interpreted as <c>metadata</c>; callers that relied on the old behavior must
    /// switch to the four-arg form with named arguments:
    /// <c>AddField(field, arguments: null, metadata: dict, action)</c>.
    /// </remarks>
    /// <param name="fieldName">The name of the field to add. Supports dotted notation for nested fields.</param>
    /// <param name="arguments">GraphQL arguments for the field.</param>
    /// <param name="action">A delegate that receives a FieldBuilder instance for configuring nested fields within this field.</param>
    /// <returns>The current FieldBuilder instance for method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when fieldName or action is null.</exception>
    public FieldBuilder AddField(string fieldName, Dictionary<string, object?>? arguments, Action<FieldBuilder> action)
        => AddFieldCore(fieldName, arguments: arguments, action: action);

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
    /// Args-first counterpart of <see cref="AddField(string, string[], Dictionary{string, object?}?, Dictionary{string, object?}?, Action{FieldBuilder})"/>.
    /// Mirrors the parameter order used by <see cref="QueryBuilder.AddField(string, Dictionary{string, object?}, string[], Dictionary{string, object?}?)"/>.
    /// </summary>
    /// <param name="fieldName">The name of the parent field to add.</param>
    /// <param name="arguments">GraphQL arguments for the parent field.</param>
    /// <param name="subFields">Array of subfield names to add under this field.</param>
    /// <param name="metadata">Optional metadata dictionary to associate with the parent field.</param>
    /// <param name="action">A delegate that receives a FieldBuilder instance for configuring additional nested fields.</param>
    /// <returns>The current FieldBuilder instance for method chaining.</returns>
    public FieldBuilder AddField(string fieldName, Dictionary<string, object?>? arguments, string[] subFields, Dictionary<string, object?>? metadata, Action<FieldBuilder> action)
        => AddFieldCore(fieldName, Constants.ObjectFieldType, arguments, subFields, metadata, action);

    /// <summary>
    /// Args-first variant of <see cref="AddField(string, string[], Dictionary{string, object?}?, Dictionary{string, object?}?, Action{FieldBuilder})"/>
    /// without metadata. Convenience overload that passes <c>metadata: null</c>.
    /// </summary>
    /// <param name="fieldName">The name of the parent field to add.</param>
    /// <param name="arguments">GraphQL arguments for the parent field.</param>
    /// <param name="subFields">Array of subfield names to add under this field.</param>
    /// <param name="action">A delegate that receives a FieldBuilder instance for configuring additional nested fields.</param>
    /// <returns>The current FieldBuilder instance for method chaining.</returns>
    public FieldBuilder AddField(string fieldName, Dictionary<string, object?>? arguments, string[] subFields, Action<FieldBuilder> action)
        => AddFieldCore(fieldName, Constants.ObjectFieldType, arguments, subFields, action: action);

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
            var fieldBuilder = new FieldBuilder(field, this);
            action(fieldBuilder);
            // GetOrAddField above already added `field` as a child of _fieldDefinition,
            // so _fieldDefinition._children is non-null at this point. Replace by REFERENCE (not
            // by Name) so a same-named/different-alias sibling already in _children is never
            // mistaken for `field`'s own slot.
            _fieldDefinition._children!.ReplaceReference(field, fieldBuilder._fieldDefinition);

            // The action may have mutated arguments anywhere in field's subtree (nested Where()/
            // AddField calls), which GetOrAddField above could not have anticipated. _fieldDefinition
            // is field's parent, so its memoized deep fingerprint — if any — is now stale too.
            _fieldDefinition.ClearMergeMemo();
        }

        // A captured builder can add argument-bearing fields long after its own action unwound
        // and a merge index fingerprinted the root. Only arguments (directly, or set inside the
        // action) change a fingerprint, so plain field additions skip the ancestor walk.
        if (action != null || arguments is { Count: > 0 })
        {
            ClearAncestorMergeMemos();
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
    public static FieldBuilder Create(Dictionary<string, FieldDefinition> fieldDefinitions, string fieldName, string type = Constants.DefaultFieldType, IDictionary<string, object?>? arguments = null, Dictionary<string, object?>? metadata = null)
        => Create(fieldDefinitions, fieldName, type, arguments, metadata, variableSink: null);

    /// <summary>
    /// Internal counterpart of <see cref="Create(Dictionary{string, FieldDefinition}, string, string, IDictionary{string, object?}?, Dictionary{string, object?}?)"/>
    /// that additionally threads the owning operation's variable set down into the returned
    /// builder (and every nested <c>Action&lt;FieldBuilder&gt;</c> scope built from it), so
    /// <see cref="IncludeIf(Variable)"/>/<see cref="SkipIf(Variable)"/> called anywhere in the
    /// resulting subtree can promote their variable into the operation signature. Used by
    /// <see cref="QueryBuilder"/>, which owns the <see cref="Abstractions.QueryDefinition.Variables"/>
    /// set this flows from.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static FieldBuilder Create(Dictionary<string, FieldDefinition> fieldDefinitions, string fieldName, string type, IDictionary<string, object?>? arguments, Dictionary<string, object?>? metadata, SortedSet<Variable>? variableSink)
    {
        // Empty/whitespace dotted segments are collapsed by FieldFactory.GetOrAddField below, the
        // same shared routine the instance AddField overloads use — so this factory no longer
        // bypasses that handling. Full per-segment identifier validation is intentionally NOT done
        // here: Create legitimately receives complex paths (type prefixes, alias:name, array
        // indices like results[0]) that the complex-path parser resolves.
        // FAIL-FAST: Use empty arguments if null or empty
        var argumentsToUse = arguments is { Count: > 0 } ? arguments : null;

        // Use FieldFactory for field creation
        var field = FieldFactory.GetOrAddField(fieldDefinitions, fieldName, type, argumentsToUse, null, metadata);

        return new FieldBuilder(field, CreateAncestorChain(fieldDefinitions, field, variableSink), variableSink);
    }

    /// <summary>
    /// Creates a new FieldBuilder instance from an existing field definition.
    /// </summary>
    /// <param name="fieldDefinition">The field definition to create the builder from.</param>
    /// <returns>A new FieldBuilder instance.</returns>
    public static FieldBuilder Create(FieldDefinition fieldDefinition) => new(fieldDefinition);

    // A dotted or complex path resolves to a nested field. Its builder can be captured and mutated
    // after a merge index has fingerprinted the root, so it needs the same ancestor chain a nested
    // Action<FieldBuilder> scope gets: Where()/IncludeIf()/SkipIf() clear every ancestor's memo and
    // reach the root's tracker through it. Root-level fields return null and keep the direct path.
    private static FieldBuilder? CreateAncestorChain(Dictionary<string, FieldDefinition> fieldDefinitions, FieldDefinition field, SortedSet<Variable>? variableSink)
    {
        if (fieldDefinitions.TryGetValue(field._effectiveName, out var rootCandidate) && ReferenceEquals(rootCandidate, field))
        {
            return null;
        }

        // Paths normally nest by prefix, which confines the search to one branch. The unpruned
        // pass covers any path shape that does not, so the chain is found whenever it exists.
        var ancestors = new List<FieldDefinition>(4);
        return BuildChain(fieldDefinitions, field, ancestors, variableSink, prunedByPath: true)
            ?? BuildChain(fieldDefinitions, field, ancestors, variableSink, prunedByPath: false);
    }

    private static FieldBuilder? BuildChain(Dictionary<string, FieldDefinition> fieldDefinitions, FieldDefinition field, List<FieldDefinition> ancestors, SortedSet<Variable>? variableSink, bool prunedByPath)
    {
        foreach (var root in fieldDefinitions.Values)
        {
            ancestors.Clear();
            if (!TryFindAncestors(root, field, ancestors, prunedByPath)) continue;

            FieldBuilder? parent = null;
            foreach (var ancestor in ancestors)
            {
                parent = new FieldBuilder(ancestor, parent, variableSink);
            }

            return parent;
        }

        return null;
    }

    private static bool TryFindAncestors(FieldDefinition current, FieldDefinition target, List<FieldDefinition> ancestors, bool prunedByPath)
    {
        if (current._children is not { Count: > 0 } children) return false;
        if (prunedByPath && !target.Path.StartsWith(current.Path, StringComparison.Ordinal)) return false;

        ancestors.Add(current);
        foreach (var child in children.AsSpan())
        {
            if (ReferenceEquals(child, target) || TryFindAncestors(child, target, ancestors, prunedByPath)) return true;
        }

        ancestors.RemoveAt(ancestors.Count - 1);
        return false;
    }

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
    private static void RecursiveCreateField(Dictionary<string, FieldDefinition> fields, FieldDefinition fieldDefinition)
    {
        var parentField = FieldFactory.CreateOrMergeField(fields, fieldDefinition);

        // Carry over the incoming field's inline fragments, spreads, and directives (deep-cloned)
        // — CreateOrMergeField only copies name/type/alias/arguments/metadata, so without this the
        // merged field would render without its fragment state.
        FieldDefinitionExtensions.MergeFragmentState(parentField, fieldDefinition);

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

            // Any descendant merge above may have changed the merge-relevant subtree beneath
            // parentField — its own memoized fingerprint (if any, from a prior FindMergeTarget
            // call) is now stale.
            parentField.ClearMergeMemo();
        }
    }

    private static void RecursiveCreateField(FieldChildren children, FieldDefinition fieldDefinition)
    {
        var parentField = FieldFactory.CreateOrMergeField(children, fieldDefinition);

        FieldDefinitionExtensions.MergeFragmentState(parentField, fieldDefinition);

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

            // Any descendant merge above may have changed the merge-relevant subtree beneath
            // parentField — its own memoized fingerprint (if any, from a prior FindMergeTarget
            // call) is now stale.
            parentField.ClearMergeMemo();
        }
    }

    internal static void Include(Dictionary<string, FieldDefinition> fields, FieldDefinition fieldDefinition)
        => RecursiveCreateField(fields, fieldDefinition);

    internal static void Include(FieldChildren children, FieldDefinition fieldDefinition)
        => RecursiveCreateField(children, fieldDefinition);

    private static void ValidateFieldNameSegments(ReadOnlySpan<char> fieldName)
    {
        if (fieldName.IsEmpty)
            throw new ArgumentException("Field name cannot be empty.", nameof(fieldName));

        // Validate each dot-separated segment individually. Empty/whitespace segments (e.g. from
        // "user..name" or "user. .name") are skipped — they are collapsed by FieldFactory rather
        // than rejected, so validation must agree with that skip contract.
        while (!fieldName.IsEmpty)
        {
            var dotIndex = fieldName.IndexOf('.');
            var segment = dotIndex >= 0 ? fieldName[..dotIndex] : fieldName;
            if (!segment.IsWhiteSpace())
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
            _fieldDefinition = _fieldDefinition with
            {
                _arguments = new(StringComparer.OrdinalIgnoreCase),
                _deepArgumentFingerprint = null,
                _subtreeHasAnyArguments = null,
            };
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

        // Mutating arguments in place (above) invalidates any memoized fingerprint on this field —
        // including the case where _arguments already existed and only its contents changed.
        _fieldDefinition._deepArgumentFingerprint = null;
        _fieldDefinition._subtreeHasAnyArguments = null;

        // This field's own memo is cleared above, but a captured, since-detached FieldBuilder (the
        // Action<FieldBuilder> that produced it already returned) means AddFieldCore's own cascading
        // ClearMergeMemo calls — one per nesting level, run as each action unwinds — happened BEFORE
        // this Where() call and cannot see it. Every ancestor in this builder's nesting chain still
        // has this field in its merge-relevant subtree, so their memoized deep fingerprints are
        // equally stale and must be cleared explicitly here.
        ClearAncestorMergeMemos();

        _fieldDefinition.InvalidateMergeIndex();

        return this;
    }

    /// <summary>
    /// Adds an inline GraphQL fragment narrowing the current field's selection set to a
    /// concrete type. Renders as <c>... on TypeName { … }</c>. Use when the parent field's
    /// schema return type is a union or interface and you need fields that only exist on
    /// a specific implementation.
    /// </summary>
    /// <param name="typeName">The concrete GraphQL type to narrow to (e.g. <c>"Repository"</c>).
    /// Case-sensitive — emitted verbatim into the rendered GraphQL.</param>
    /// <param name="action">Builder action for the fragment's selection set. The builder it
    /// receives writes into the fragment, not the parent field.</param>
    /// <returns>The current FieldBuilder instance for method chaining.</returns>
    /// <remarks>
    /// Multiple <c>OnType("Repository", …)</c> calls on the same parent merge into one
    /// fragment definition (idempotent registration, then the lambda extends the existing
    /// selection set). This matches the GraphQL semantics where two adjacent inline fragments
    /// for the same type are equivalent to one combined fragment.
    /// </remarks>
    /// <exception cref="ArgumentException">Thrown when <paramref name="typeName"/> is null or whitespace.</exception>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="action"/> is null.</exception>
    public FieldBuilder OnType(string typeName, Action<FieldBuilder> action)
    {
        if (string.IsNullOrWhiteSpace(typeName))
        {
            throw new ArgumentException("Inline fragment type name cannot be null or whitespace.", nameof(typeName));
        }
        ArgumentNullException.ThrowIfNull(action);

        var fragment = _fieldDefinition.GetOrAddInlineFragment(typeName);
        PopulateFragmentSurface($"__inline_fragment_{typeName}", fragment.GetOrCreateFieldsStore(),
            ref fragment._fragments, ref fragment._spreadFragments, ref fragment._directives, action, _variableSink);

        return this;
    }

    /// <summary>
    /// Runs a user selection-set lambda against a synthetic field whose stores alias
    /// <paramref name="fieldsStore"/> and the given fragment maps, then writes the lambda's
    /// final maps back through the ref parameters. Shared by <see cref="OnType"/> and
    /// <see cref="QueryBuilder.AddFragment"/> so the aliasing rules live in one place.
    /// </summary>
    /// <remarks>
    /// The final state is read from the builder's CURRENT field definition, not the original
    /// surface: fluent calls like <see cref="Where"/>/<see cref="WithAlias"/>/<see cref="WithType"/>
    /// replace the definition with a <c>with</c> record copy, so maps first created after such
    /// a call exist only on the copy. The <c>_children</c> store needs no reflect-back — it is
    /// created non-null up front and `with` copies carry the same reference.
    /// <para>
    /// <paramref name="directives"/> writes back <c>IncludeIf</c>/<c>SkipIf</c>/<c>Directive</c>
    /// calls made directly on the lambda's top-level builder into the caller's own directive store
    /// — <see cref="OnType"/> passes its <see cref="InlineFragmentDefinition"/>'s <c>_directives</c>
    /// so directives on an inline fragment (<c>... on Admin @include(if:$a){ … }</c>) survive.
    /// <see cref="QueryBuilder.AddFragment"/> passes a throwaway local: named-fragment-level
    /// directives are out of scope until fragment-spread directives ship (tracked alongside issue
    /// #20), so a call there is a silent no-op today, unchanged from before this parameter existed.
    /// </para>
    /// </remarks>
    internal static void PopulateFragmentSurface(
        string surfaceName,
        FieldChildren fieldsStore,
        ref Dictionary<string, InlineFragmentDefinition>? fragments,
        ref List<string>? spreadFragments,
        ref List<FieldDirective>? directives,
        Action<FieldBuilder> action,
        SortedSet<Variable>? variableSink = null)
    {
        var surface = new FieldDefinition(surfaceName, Constants.DefaultFieldType)
        {
            _children = fieldsStore,
            _fragments = fragments,
            _spreadFragments = spreadFragments,
            _directives = directives,
        };

        var builder = new FieldBuilder(surface, variableSink: variableSink);
        action(builder);

        var final = builder._fieldDefinition;
        fragments = final._fragments;
        spreadFragments = final._spreadFragments;
        directives = final._directives;
    }

    /// <summary>
    /// Spreads a named fragment into this field's selection set. The renderer emits
    /// <c>...Name</c> after plain fields and inline fragments. The fragment itself is declared
    /// at the operation's top level via <c>QueryBuilder.AddFragment(name, onType, build)</c>;
    /// this method only records the spread reference.
    /// </summary>
    /// <param name="name">The name of the fragment to spread. NGql does not validate that the
    /// fragment is actually declared — an undeclared spread renders verbatim and the server
    /// rejects it. NGql is schemaless.</param>
    /// <returns>The current FieldBuilder instance for method chaining.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is null or whitespace.</exception>
    public FieldBuilder SpreadFragment(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Spread fragment name cannot be null or whitespace.", nameof(name));
        }

        _fieldDefinition.AddSpreadFragment(name);
        return this;
    }

    /// <summary>
    /// Attaches an <c>@include(if:$var)</c> directive to the current field. The field is kept in
    /// the response only when the runtime value of <paramref name="ifVariable"/> is <c>true</c>.
    /// </summary>
    /// <param name="ifVariable">The variable name, with or without the leading <c>$</c> — both
    /// <c>"$show"</c> and <c>"show"</c> render as <c>if:$show</c>.</param>
    /// <returns>The current FieldBuilder instance for method chaining.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="ifVariable"/> is null or whitespace.</exception>
    /// <remarks>
    /// <b>This overload does NOT promote a variable declaration into the operation signature</b> —
    /// a bare <see cref="string"/> carries no GraphQL type, so there is nothing to promote. Use it
    /// only when the variable is already declared some other way (e.g. via
    /// <c>QueryBuilder.Definition.Variables.Add(...)</c>, or because another field argument already
    /// promoted a <see cref="Variable"/> with this same name). If you want the variable declared
    /// for you, use <see cref="IncludeIf(Variable)"/> instead — it both renders the directive and
    /// promotes the variable into <c>query Name($var:Type!){ … }</c>. Calling this overload with a
    /// variable that is never declared elsewhere renders a document a GraphQL server will reject.
    /// </remarks>
    public FieldBuilder IncludeIf(string ifVariable)
        => AddIfDirective("include", ifVariable);

    /// <summary>
    /// Attaches an <c>@include(if:$var)</c> directive to the current field and promotes
    /// <paramref name="condition"/> into the operation's variable signature (deduplicated with any
    /// other variable of the same name already declared), exactly as a <see cref="Variable"/>
    /// passed as a field argument does today. The field is kept in the response only when the
    /// runtime value of the variable is <c>true</c>.
    /// </summary>
    /// <param name="condition">The Boolean variable that gates inclusion. <see cref="Variable.Type"/>
    /// must reduce to <c>Boolean!</c> or <c>Boolean</c> — the GraphQL spec requires the <c>if</c>
    /// argument of <c>@include</c> to be a Boolean expression.</param>
    /// <returns>The current FieldBuilder instance for method chaining.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="condition"/>'s type is not
    /// <c>Boolean</c> or <c>Boolean!</c>.</exception>
    public FieldBuilder IncludeIf(Variable condition)
        => AddIfDirective("include", condition);

    /// <summary>
    /// Attaches a <c>@skip(if:$var)</c> directive to the current field. The field is omitted from
    /// the response when the runtime value of <paramref name="ifVariable"/> is <c>true</c>.
    /// </summary>
    /// <param name="ifVariable">The variable name, with or without the leading <c>$</c> — both
    /// <c>"$hide"</c> and <c>"hide"</c> render as <c>if:$hide</c>.</param>
    /// <returns>The current FieldBuilder instance for method chaining.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="ifVariable"/> is null or whitespace.</exception>
    /// <remarks>
    /// <b>This overload does NOT promote a variable declaration into the operation signature</b> —
    /// a bare <see cref="string"/> carries no GraphQL type, so there is nothing to promote. Use it
    /// only when the variable is already declared some other way (e.g. via
    /// <c>QueryBuilder.Definition.Variables.Add(...)</c>, or because another field argument already
    /// promoted a <see cref="Variable"/> with this same name). If you want the variable declared
    /// for you, use <see cref="SkipIf(Variable)"/> instead — it both renders the directive and
    /// promotes the variable into <c>query Name($var:Type!){ … }</c>. Calling this overload with a
    /// variable that is never declared elsewhere renders a document a GraphQL server will reject.
    /// </remarks>
    public FieldBuilder SkipIf(string ifVariable)
        => AddIfDirective("skip", ifVariable);

    /// <summary>
    /// Attaches a <c>@skip(if:$var)</c> directive to the current field and promotes
    /// <paramref name="condition"/> into the operation's variable signature (deduplicated with any
    /// other variable of the same name already declared), exactly as a <see cref="Variable"/>
    /// passed as a field argument does today. The field is omitted from the response when the
    /// runtime value of the variable is <c>true</c>.
    /// </summary>
    /// <param name="condition">The Boolean variable that gates the skip. <see cref="Variable.Type"/>
    /// must reduce to <c>Boolean!</c> or <c>Boolean</c> — the GraphQL spec requires the <c>if</c>
    /// argument of <c>@skip</c> to be a Boolean expression.</param>
    /// <returns>The current FieldBuilder instance for method chaining.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="condition"/>'s type is not
    /// <c>Boolean</c> or <c>Boolean!</c>.</exception>
    public FieldBuilder SkipIf(Variable condition)
        => AddIfDirective("skip", condition);

    /// <summary>
    /// Attaches an arbitrary directive to the current field, rendered as <c>@name</c> optionally
    /// followed by <c>(argName:value, …)</c>. Use for custom or server-defined directives such as
    /// <c>@deprecated</c> or <c>@format(as:"ISO8601")</c>.
    /// </summary>
    /// <param name="name">The directive name, with or without the leading <c>@</c> — both
    /// <c>"@format"</c> and <c>"format"</c> render as <c>@format</c>.</param>
    /// <param name="arguments">Optional directive arguments; <c>null</c> or empty for a
    /// no-argument directive like <c>@deprecated</c>. Any <see cref="Variable"/> values found in
    /// this dictionary (including nested inside dictionaries/lists/objects) are promoted into the
    /// operation's variable signature, exactly like field arguments.</param>
    /// <returns>The current FieldBuilder instance for method chaining.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is null or whitespace.</exception>
    public FieldBuilder Directive(string name, Dictionary<string, object?>? arguments = null)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Directive name cannot be null or whitespace.", nameof(name));
        }

        // Strip ALL leading '@' (not just one) so pathological input like "@@foo" still yields a
        // single-'@' directive on render. A name that is only '@' characters leaves nothing behind.
        var normalizedName = name.TrimStart('@');
        if (normalizedName.Length == 0)
        {
            throw new ArgumentException("Directive name cannot consist only of '@' characters.", nameof(name));
        }

        if (arguments?.Count > 0 && _variableSink is not null)
        {
            Helpers.ExtractVariablesFromValue(arguments, _variableSink);
        }

        _fieldDefinition.AddDirective(new FieldDirective(normalizedName, arguments));
        InvalidateMergeMemoIfConditional(normalizedName);
        return this;
    }

    // Shared helper for the bare-string @include / @skip overloads — both take a single `if:$var`
    // argument. The variable is normalized so callers may pass the name with or without the
    // leading `$`; it is always rendered with a single `$`. Does NOT promote — see the string
    // overloads' XML doc remarks for why.
    private FieldBuilder AddIfDirective(string directiveName, string ifVariable)
    {
        if (string.IsNullOrWhiteSpace(ifVariable))
        {
            throw new ArgumentException("Directive variable cannot be null or whitespace.", nameof(ifVariable));
        }

        // Collapse ANY number of leading '$' to exactly one so input like "$$show" renders a
        // single-'$' variable. A value that is only '$' characters carries no variable name.
        var bareName = ifVariable.TrimStart('$');
        if (bareName.Length == 0)
        {
            throw new ArgumentException("Directive variable must contain a name after the '$'.", nameof(ifVariable));
        }
        var name = "$" + bareName;
        var arguments = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["if"] = new Variable(name, "Boolean!"),
        };
        _fieldDefinition.AddDirective(new FieldDirective(directiveName, arguments));
        InvalidateMergeMemoIfConditional(directiveName);
        return this;
    }

    // Shared helper for the Variable-typed IncludeIf/SkipIf overloads — validates that the
    // variable's declared type reduces to a Boolean per the GraphQL spec's requirement for the
    // `if` argument of @include/@skip, attaches the directive, and — unlike AddIfDirective(string,
    // string) — promotes the variable into the owning operation's signature via the same
    // Helpers.ExtractVariablesFromValue path field-argument Variables already use.
    private FieldBuilder AddIfDirective(string directiveName, Variable condition)
    {
        if (!IsBooleanType(condition.Type))
        {
            throw new ArgumentException(
                $"Variable '{condition.Name}' passed to @{directiveName}'s condition must have type 'Boolean' or 'Boolean!', but was '{condition.Type}'.",
                nameof(condition));
        }

        var arguments = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["if"] = condition,
        };
        _fieldDefinition.AddDirective(new FieldDirective(directiveName, arguments));
        InvalidateMergeMemoIfConditional(directiveName);

        // Same promotion path field-argument Variables already use (Helpers.ExtractVariablesFromValue
        // adds the Variable to the set, deduping via Variable's value equality) rather than a
        // parallel mechanism.
        if (_variableSink is not null)
        {
            Helpers.ExtractVariablesFromValue(condition, _variableSink);
        }

        return this;
    }

    private static bool IsBooleanType(string type)
        => type.Equals("Boolean", StringComparison.Ordinal) || type.Equals("Boolean!", StringComparison.Ordinal);

    private void InvalidateMergeMemoIfConditional(string directiveName)
    {
        if (!directiveName.Equals("include", StringComparison.Ordinal)
            && !directiveName.Equals("skip", StringComparison.Ordinal))
        {
            return;
        }

        _fieldDefinition._deepArgumentFingerprint = null;
        _fieldDefinition._subtreeHasAnyArguments = null;
        ClearAncestorMergeMemos();
        _fieldDefinition.InvalidateMergeIndex();
    }
}
