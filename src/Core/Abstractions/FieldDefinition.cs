using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using NGql.Core.Extensions;

namespace NGql.Core.Abstractions;

/// <summary>
///     Represents a field definition.
/// </summary>
[SuppressMessage("Minor Code Smell", "S2292:Trivial properties should be auto-implemented")]
public sealed record FieldDefinition
{
    // Fields
    internal FieldChildren? _children;
    internal Dictionary<string, InlineFragmentDefinition>? _fragments;
    internal List<string>? _spreadFragments;
    internal string? _type;
    internal string? _alias;
    internal string _effectiveName;
    internal SortedDictionary<string, object?>? _arguments;
    internal Dictionary<string, object?>? _metadata;
    internal string Path { get; init; } = string.Empty;

    /// <summary>
    /// Cached result of "does this field's subtree contain any arguments?".
    /// Null = not yet computed. Reset to null whenever the subtree mutates.
    /// </summary>
    internal bool? _subtreeHasAnyArguments;

    private bool? _isArray;
    private bool? _isNullable;

    /// <summary>
    /// Creates a field definition with a name and optional type and alias.
    /// <paramref name="type"/> defaults to <see cref="Constants.DefaultFieldType"/> when null.
    /// </summary>
    /// <param name="name">Field name as it appears in the rendered GraphQL.</param>
    /// <param name="type">Optional type-annotation metadata (rendered nowhere; consumed by tooling).</param>
    /// <param name="alias">Optional response-side alias.</param>
    public FieldDefinition(string name, string? type = null, string? alias = null)
        : this(name, type ?? Constants.DefaultFieldType, alias, null)
    {
    }

    /// <summary>
    /// Creates a field definition with a pre-sorted argument dictionary and an optional
    /// child-field collection. Used internally on the hot path to avoid re-sorting.
    /// </summary>
    /// <param name="name">Field name.</param>
    /// <param name="type">Type-annotation metadata.</param>
    /// <param name="alias">Optional response-side alias.</param>
    /// <param name="sortedArguments">Pre-sorted argument map (case-insensitive); null/empty stores null.</param>
    /// <param name="fields">Optional initial children; null/empty leaves the field as a leaf.</param>
    public FieldDefinition(string name, string type, string? alias, SortedDictionary<string, object?>? sortedArguments = null, Dictionary<string, FieldDefinition>? fields = null)
    {
        Name = name;
        _alias = alias;
        _type = type;
        _arguments = sortedArguments?.Count > 0 ? sortedArguments : null;
        _children = AsChildren(fields);
        _effectiveName = !string.IsNullOrEmpty(_alias) ? _alias : Name;
    }

    /// <summary>
    /// Creates a field definition from an unsorted argument dictionary (e.g. one produced by
    /// callers using collection initializers). The dictionary is copied into a case-insensitive
    /// sorted store so output ordering is stable.
    /// </summary>
    /// <param name="name">Field name.</param>
    /// <param name="type">Type-annotation metadata.</param>
    /// <param name="alias">Optional response-side alias.</param>
    /// <param name="arguments">Unsorted argument map; null/empty stores null.</param>
    /// <param name="fields">Optional initial children; null/empty leaves the field as a leaf.</param>
    public FieldDefinition(string name, string type, string? alias, IDictionary<string, object?>? arguments, Dictionary<string, FieldDefinition>? fields = null)
    {
        Name = name;
        _alias = alias;
        _type = type;
        _arguments = ToSortedArguments(arguments);
        _children = AsChildren(fields);
        _effectiveName = !string.IsNullOrEmpty(_alias) ? _alias : Name;
    }

    private static SortedDictionary<string, object?>? ToSortedArguments(IDictionary<string, object?>? arguments)
    {
        if (arguments is null) return null;
        if (arguments.Count == 0) return null;
        var sorted = new SortedDictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var kvp in arguments) sorted[kvp.Key] = kvp.Value;
        return sorted;
    }

    private static FieldChildren? AsChildren(Dictionary<string, FieldDefinition>? fields)
    {
        if (fields is null || fields.Count == 0) return null;
        var children = new FieldChildren();
        foreach (var kvp in fields) children.Append(kvp.Value);
        return children;
    }

    // Properties
    /// <summary>
    /// The name of the field.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; init; }

    /// <summary>
    /// The type of the field. Defaults to <see cref="Constants.DefaultFieldType"/> if not specified.
    /// This is used to define the data type of the field, such as "String", "Int", "Boolean", etc.
    /// </summary>
    [JsonPropertyName("type")]
    public string? Type
    {
        get => _type;
        init => _type = value;
    }

    /// <summary>
    /// The alias of the field, if any. This is used to provide a more readable or meaningful name for the field in queries.
    /// If not specified, the field will use its original name.
    /// </summary>
    [JsonPropertyName("alias")]
    public string? Alias
    {
        get => _alias;
        init
        {
            _alias = value;
            _effectiveName = !string.IsNullOrEmpty(value) ? value : Name;
        }
    }

    private static readonly IReadOnlyDictionary<string, FieldDefinition> EmptyReadOnlyFields
        = new FieldChildren();

    /// <summary>
    ///     The collection of fields related to <see cref="FieldDefinition"/>.
    /// </summary>
    [JsonPropertyName("fields")]
    public IReadOnlyDictionary<string, FieldDefinition> Fields
        => _children ?? EmptyReadOnlyFields;

    private static readonly IReadOnlyDictionary<string, InlineFragmentDefinition> EmptyReadOnlyFragments
        = new Dictionary<string, InlineFragmentDefinition>();

    /// <summary>
    /// Inline fragments attached to this field, keyed by the fragment's GraphQL type name
    /// (case-sensitive). Each fragment renders as <c>... on TypeName { … }</c> after the
    /// field's plain children, alphabetical by type name.
    /// </summary>
    /// <remarks>
    /// Used when the field's schema return type is a union or interface and the caller needs
    /// type narrowing. See <see cref="NGql.Core.Builders.FieldBuilder.OnType(string, Action{NGql.Core.Builders.FieldBuilder})"/>
    /// for the builder-side API. Multiple <c>OnType</c> calls for the same type name merge
    /// into one fragment definition.
    /// </remarks>
    [JsonPropertyName("inlineFragments")]
    public IReadOnlyDictionary<string, InlineFragmentDefinition> InlineFragments
        => (IReadOnlyDictionary<string, InlineFragmentDefinition>?)_fragments ?? EmptyReadOnlyFragments;

    private static readonly IReadOnlyList<string> EmptyReadOnlySpreadFragments = Array.Empty<string>();

    /// <summary>
    /// Names of <see cref="NamedFragmentDefinition"/>s spread into this field's selection set
    /// (rendered as <c>...Name</c>). Order is preserved from <c>SpreadFragment</c> calls; the
    /// renderer emits spreads in declaration order after plain fields and inline fragments.
    /// </summary>
    /// <remarks>
    /// A list, not a set: the same fragment may legitimately appear multiple times at the
    /// same selection set once directives are added in a future release (e.g.
    /// <c>...Foo @include(if: $a) ...Foo @include(if: $b)</c>). Today, repeated spreads of
    /// the same name produce duplicate <c>...Name</c> output that the server collapses; the
    /// builder de-duplicates eagerly at <c>FieldBuilder.SpreadFragment</c> to keep render
    /// output minimal.
    ///
    /// Spreads are not validated against <see cref="QueryDefinition.NamedFragments"/> — an
    /// undeclared spread renders verbatim and the server rejects it. NGql is schemaless.
    /// </remarks>
    [JsonPropertyName("spreadFragments")]
    public IReadOnlyList<string> SpreadFragments
        => (IReadOnlyList<string>?)_spreadFragments ?? EmptyReadOnlySpreadFragments;

    /// <summary>
    /// Append a fragment-spread reference to this field's selection set. No-ops when the
    /// fragment name is already present, keeping the render output minimal until directives
    /// (which would distinguish duplicate spreads) are supported.
    /// </summary>
    internal void AddSpreadFragment(string name)
    {
        _spreadFragments ??= new List<string>();
        if (!_spreadFragments.Contains(name, StringComparer.Ordinal))
        {
            _spreadFragments.Add(name);
        }
    }

    private static readonly IReadOnlyDictionary<string, object?> EmptyReadOnlyArguments
        = new SortedDictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Sorted, case-insensitive view of the field's GraphQL arguments. Returns an empty
    /// dictionary (never null) when the field has no arguments.
    /// </summary>
    [JsonPropertyName("arguments")]
    public IReadOnlyDictionary<string, object?> Arguments
        => _arguments ?? EmptyReadOnlyArguments;

    /// <summary>
    /// Metadata associated with the field definition.
    /// This can include additional information such as descriptions, tags, or any other relevant data.
    ///
    /// Not used during query text generation but can be useful for documentation or introspection purposes.
    /// </summary>
    [JsonPropertyName("metadata")]
    public Dictionary<string, object?> Metadata
    {
        get => _metadata ??= [];
        set => _metadata = value;
    }

    /// <summary>
    /// Gets a value indicating whether this field type is an array.
    /// </summary>
    [JsonIgnore]
    public bool IsArray => _isArray ??= _type.IsArrayType();

    /// <summary>
    /// Gets a value indicating whether this field type is nullable.
    /// </summary>
    [JsonIgnore]
    public bool IsNullable => _isNullable ??= _type.IsNullableType();

    /// <summary>
    /// Gets a value indicating whether this field has child fields.
    /// </summary>
    [JsonIgnore]
    public bool HasFields => _children is { Count: > 0 };

    /// <summary>
    /// Gets a value indicating whether this field has any inline fragments.
    /// </summary>
    [JsonIgnore]
    public bool HasInlineFragments => _fragments is { Count: > 0 };

    /// <summary>
    /// Returns the existing inline fragment for <paramref name="typeName"/>, or appends a new
    /// one. Used by the builder to merge multiple <c>OnType("Repository", …)</c> calls on the
    /// same parent into a single fragment definition.
    /// </summary>
    internal InlineFragmentDefinition GetOrAddInlineFragment(string typeName)
    {
        _fragments ??= new Dictionary<string, InlineFragmentDefinition>(StringComparer.Ordinal);
        if (!_fragments.TryGetValue(typeName, out var fragment))
        {
            fragment = new InlineFragmentDefinition(typeName);
            _fragments[typeName] = fragment;
        }
        return fragment;
    }

    /// <summary>
    /// When <c>true</c>, the merger treats this field as opaque — it will not be merged with
    /// other fields of the same path; instead it gets aliased (<c>name_1</c>, <c>name_2</c>, …)
    /// during <see cref="NGql.Core.Builders.QueryBuilder.Include(NGql.Core.Builders.QueryBuilder)"/>.
    /// The setter is internal; the flag is set by <see cref="MergingStrategy.NeverMerge"/>.
    /// </summary>
    [JsonIgnore]
    public bool IsNeverMerge { get; internal set; }

    // Methods
    public bool Equals(FieldDefinition? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return string.Equals(Name, other.Name, StringComparison.Ordinal)
            && string.Equals(Path, other.Path, StringComparison.Ordinal)
            && string.Equals(_type, other._type, StringComparison.OrdinalIgnoreCase)
            && string.Equals(_alias, other._alias, StringComparison.OrdinalIgnoreCase)
            && IsNeverMerge == other.IsNeverMerge;
    }

    // FieldDefinition holds mutable internal state by design (in-place merging in QueryMerger).
    // The hash captures identity at evaluation time; callers do not stash hashes across mutations.
#pragma warning disable S2328
    public override int GetHashCode()
        => HashCode.Combine(Name, Path, _type?.ToLowerInvariant(), _alias?.ToLowerInvariant(), IsNeverMerge);
#pragma warning restore S2328

    public override string ToString()
    {
        if (string.IsNullOrWhiteSpace(Type))
        {
            return string.IsNullOrWhiteSpace(Alias) ? Name : $"{Alias}:{Name}";
        }

        return string.IsNullOrWhiteSpace(Alias) ? $"{Type} {Name}" : $"{Type} {Alias}:{Name}";
    }
}
