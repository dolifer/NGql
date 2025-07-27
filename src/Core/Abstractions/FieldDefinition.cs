using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace NGql.Core.Abstractions;

/// <summary>
///     Represents a field definition.
/// </summary>
/// <param name="Name">The name of the field.</param>
/// <param name="Alias"></param><param name="Arguments"></param>
public sealed record FieldDefinition(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("alias")] string? Alias = null,
    [property: JsonPropertyName("arguments")] SortedDictionary<string, object?>? Arguments = null)
{
    /// <summary>
    ///     The collection of fields related to <see cref="FieldDefinition"/>.
    /// </summary>
    [JsonPropertyName("fields")]
    public SortedDictionary<string, FieldDefinition> Fields { get; } = new(StringComparer.OrdinalIgnoreCase);
    
    internal string Path { get; set; } = string.Empty;

    public override string ToString() => string.IsNullOrWhiteSpace(Alias) ? Name : $"{Alias}:{Name}";
}
