using System.Text.Json.Serialization;
using NGql.Core.Builders;

namespace NGql.Core.Abstractions;

/// <summary>
///     Represents a query definition.
/// </summary>
public sealed record QueryDefinition(string Name, string Description = "")
{
    /// <summary>
    /// The name of the query.
    /// </summary>
    [property: JsonPropertyName("name")]
    public string Name { get; init; } = Name;

    /// <summary>
    /// The description of the query.
    /// </summary>
    [property: JsonPropertyName("description")]
    public string Description { get; init; } = Description;

    /// <summary>
    ///     The collection of fields related to <see cref="QueryDefinition"/>.
    /// </summary>
    [JsonPropertyName("fields")]
    public SortedDictionary<string, FieldDefinition> Fields
    {
        get => _fields ??= new(StringComparer.OrdinalIgnoreCase);
        set => _fields = value;
    }

    internal SortedDictionary<string, FieldDefinition>? _fields;

    /// <summary>
    ///     The collection of variables related to fields or arguments.
    /// </summary>
    [JsonIgnore]
    public SortedSet<Variable> Variables
    {
        get => _variables ??= new();
        internal set => _variables = value;
    }

    internal SortedSet<Variable>? _variables;

    /// <summary>
    /// Metadata associated with the query definition.
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

    internal Dictionary<string, object?>? _metadata;

    /// <inheritdoc cref="QueryBlock.ToString()"/>
    public override string ToString() => new QueryTextBuilder().Build(this);

    public static implicit operator string(QueryDefinition query) => query.ToString();

    /// <summary>
    /// Merging strategy for this query definition.
    /// </summary>
    public MergingStrategy MergingStrategy { get; set; } = MergingStrategy.MergeByDefault;
}
