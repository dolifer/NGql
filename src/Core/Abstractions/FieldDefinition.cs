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
    public FieldDefinition(string name, string type, string? alias, SortedDictionary<string, object?>? sortedArguments = null, SortedDictionary<string, FieldDefinition>? fields = null)
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
    public FieldDefinition(string name, string type, string? alias, IDictionary<string, object?>? arguments, SortedDictionary<string, FieldDefinition>? fields = null)
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

    private static FieldChildren? AsChildren(SortedDictionary<string, FieldDefinition>? fields)
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
