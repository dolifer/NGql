using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace NGql.Core.Abstractions;

/// <summary>
///     Represents a field definition.
/// </summary>
public sealed record FieldDefinition
{
    public FieldDefinition(string name, string? alias = null) : this(name, alias, [], [])
    {
    }

    public FieldDefinition(string name, string? alias, SortedDictionary<string, object?> sortedArguments, SortedDictionary<string, FieldDefinition> fields)
    {
        Name = name;
        Alias = alias;
        Arguments = sortedArguments;
        Fields = fields;
    }

    /// <summary>
    ///     The collection of fields related to <see cref="FieldDefinition"/>.
    /// </summary>
    [JsonPropertyName("fields")]
    public SortedDictionary<string, FieldDefinition> Fields { get; } = new(StringComparer.OrdinalIgnoreCase);
    
    internal string Path { get; set; } = string.Empty;

    /// <summary>The name of the field.</summary>
    [JsonPropertyName("name")]
    public string Name { get; init; }

    /// <summary></summary>
    [JsonPropertyName("alias")] public string? Alias { get; init; }

    /// <summary></summary>
    [JsonPropertyName("arguments")]
    public SortedDictionary<string, object?>? Arguments { get; init; }

    public override string ToString() => string.IsNullOrWhiteSpace(Alias) ? Name : $"{Alias}:{Name}";
}
