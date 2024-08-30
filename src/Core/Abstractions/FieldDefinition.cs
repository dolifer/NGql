using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace NGql.Core.Abstractions;

/// <summary>
///     Represents a field definition.
/// </summary>
/// <param name="Name">The name of the field.</param>
public sealed record FieldDefinition([property:JsonPropertyName("name")] string Name)
{
    /// <summary>
    ///     The collection of fields related to <see cref="FieldDefinition"/>.
    /// </summary>
    [JsonPropertyName("fields")]
    public Dictionary<string, FieldDefinition> Fields { get; } = new();
}
