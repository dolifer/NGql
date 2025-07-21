using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace NGql.Core.Abstractions;

/// <summary>
///     Represents a query definition.
/// </summary>
/// <param name="Name">The name of the query.</param>
public sealed record QueryDefinition([property: JsonPropertyName("name")] string Name)
{
    /// <summary>
    ///     The collection of fields related to <see cref="QueryDefinition"/>.
    /// </summary>
    [JsonPropertyName("fields")]
    public SortedDictionary<string, FieldDefinition> Fields { get; } = new(StringComparer.OrdinalIgnoreCase);
    
    /// <summary>
    ///     The collection of variables related to fields or arguments.
    /// </summary>
    [JsonIgnore]
    public SortedSet<Variable> Variables { get; } = new();
    
    /// <inheritdoc cref="QueryBlock.ToString()"/>
    public override string ToString() => new QueryTextBuilder().Build(this);
    public static implicit operator string(QueryDefinition query) => query.ToString();
}
