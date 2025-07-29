using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

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
    public SortedDictionary<string, FieldDefinition> Fields { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    
    /// <summary>
    ///     The collection of variables related to fields or arguments.
    /// </summary>
    [JsonIgnore]
    public SortedSet<Variable> Variables { get; internal set; } = new();

    /// <summary>
    /// The tags for the query.
    /// </summary>
    [JsonPropertyName("tags")]
    public string[] Tags { get; init; } = [];

    /// <inheritdoc cref="QueryBlock.ToString()"/>
    public override string ToString() => new QueryTextBuilder().Build(this);
    public static implicit operator string(QueryDefinition query) => query.ToString();
}
