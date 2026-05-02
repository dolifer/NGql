using System.Text.Json.Serialization;
using NGql.Core.Builders;

namespace NGql.Core.Abstractions;

/// <summary>
///     Represents a query definition.
/// </summary>
public sealed record QueryDefinition(string Name, string Description = "")
{
    internal Dictionary<string, FieldDefinition>? _fields;
    internal SortedSet<Variable>? _variables;
    internal Dictionary<string, object?>? _metadata;

    /// <summary>
    /// The name of the query.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; init; } = Name;

    /// <summary>
    /// The description of the query.
    /// </summary>
    [JsonPropertyName("description")]
    public string Description { get; init; } = Description;

    /// <summary>
    ///     The collection of fields related to <see cref="QueryDefinition"/>.
    /// </summary>
    [JsonPropertyName("fields")]
    public Dictionary<string, FieldDefinition> Fields
    {
        get => _fields ??= new(StringComparer.OrdinalIgnoreCase);
        internal set => _fields = value;
    }

    /// <summary>
    ///     The collection of variables related to fields or arguments.
    /// </summary>
    [JsonIgnore]
    public SortedSet<Variable> Variables
    {
        get => _variables ??= new();
        internal set => _variables = value;
    }

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

    /// <summary>
    /// Merging strategy for this query definition.
    /// </summary>
    [JsonPropertyName("mergingStrategy")]
    public MergingStrategy MergingStrategy { get; set; } = MergingStrategy.MergeByDefault;

    /// <summary>
    /// The kind of root operation rendered by this definition (query or mutation).
    /// Selected by the factory on <see cref="QueryBuilder"/>; consumers do not set this directly.
    /// </summary>
    [JsonIgnore]
    internal OperationType OperationType { get; set; } = OperationType.Query;

    /// <inheritdoc cref="QueryBlock.ToString()"/>
    public override string ToString()
    {
        var builder = QueryTextBuilder.GetFromPool();
        try
        {
            return builder.Build(this);
        }
        finally
        {
            QueryTextBuilder.ReturnToPool(builder);
        }
    }

    public static implicit operator string(QueryDefinition query) => query.ToString();
}
