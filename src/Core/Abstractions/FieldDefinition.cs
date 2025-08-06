using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace NGql.Core.Abstractions;

/// <summary>
///     Represents a field definition.
/// </summary>
public sealed record FieldDefinition
{
    public FieldDefinition(string name, string type = "string", string? alias = null) : this(name, type, alias, [], [])
    {
    }

    public FieldDefinition(string name, string type, string? alias, SortedDictionary<string, object?> sortedArguments, SortedDictionary<string, FieldDefinition> fields)
    {
        Name = name;
        Type = type;
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

    /// <summary>The type of the field.</summary>
    [JsonPropertyName("type")]
    public string? Type { get; init; }

    /// <summary></summary>
    [JsonPropertyName("alias")] public string? Alias { get; init; }

    /// <summary></summary>
    [JsonPropertyName("arguments")]
    public SortedDictionary<string, object?>? Arguments { get; init; }

    /// <summary></summary>
    [JsonPropertyName("metadata")]
    public Dictionary<string, object?>? Metadata { get; set; } = [];

    public override string ToString()
    {
        if (string.IsNullOrWhiteSpace(Type))
            return string.IsNullOrWhiteSpace(Alias) ? Name : $"{Alias}:{Name}";
        
        return string.IsNullOrWhiteSpace(Alias) ? $"{Type} {Alias}:{Name}" : $"{Type} {Name}";
    }
}
