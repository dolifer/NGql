using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace NGql.Core.Abstractions;

/// <summary>
///     Represents a query definition.
/// </summary>
/// <param name="Name">The name of the query.</param>
/// <param name="Alias">The alias of the query.</param>
public sealed record QueryDefinition([property: JsonPropertyName("name")] string Name, [property: JsonPropertyName("alias")] string? Alias = null)
{
    /// <summary>
    ///     The collection of fields related to <see cref="QueryDefinition"/>.
    /// </summary>
    [JsonPropertyName("fields")]
    public Dictionary<string, FieldDefinition> Fields { get; } = new(StringComparer.InvariantCultureIgnoreCase);
}
