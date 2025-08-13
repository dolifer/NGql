using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace NGql.Core.Abstractions;

/// <summary>
///     Represents a field definition.
/// </summary>
public sealed record FieldDefinition
{
    public FieldDefinition(string name, string? type = null, string? alias = null)
        : this(name, type ?? Constants.DefaultFieldType, alias, [], [])
    {
    }

    public FieldDefinition(string name, string type, string? alias, SortedDictionary<string, object?> sortedArguments)
        : this(name, type, alias, sortedArguments, [])
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
    /// Gets a value indicating whether this field type is an array.
    /// </summary>
    [JsonIgnore]
    public bool IsArray => Type != null && (Type == Constants.ArrayTypeMarker || Type.Contains('['));

    /// <summary>
    /// Gets a value indicating whether this field type is nullable.
    /// </summary>
    [JsonIgnore]
    public bool IsNullable => Type != null && (Type == Constants.NullableTypeMarker || Type.EndsWith('?'));

    /// <summary>
    ///     The collection of fields related to <see cref="FieldDefinition"/>.
    /// </summary>
    [JsonPropertyName("fields")]
    public SortedDictionary<string, FieldDefinition> Fields { get; } = new(StringComparer.OrdinalIgnoreCase);
    
    internal string Path { get; set; } = string.Empty;

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
    public string? Type { get; init; }

    /// <summary>
    /// The alias of the field, if any. This is used to provide a more readable or meaningful name for the field in queries.
    /// If not specified, the field will use its original name.
    /// </summary>
    [JsonPropertyName("alias")]
    public string? Alias { get; init; }

    /// <summary></summary>
    [JsonPropertyName("arguments")]
    public SortedDictionary<string, object?>? Arguments { get; init; }

    /// <summary>
    /// Metadata associated with the field definition.
    /// This can include additional information such as descriptions, tags, or any other relevant data.
    ///
    /// Not used during query text generation but can be useful for documentation or introspection purposes.
    /// </summary>
    [JsonPropertyName("metadata")]
    public Dictionary<string, object?>? Metadata { get; set; } = [];

    public override string ToString()
    {
        if (string.IsNullOrWhiteSpace(Type))
            return string.IsNullOrWhiteSpace(Alias) ? Name : $"{Alias}:{Name}";
        
        return string.IsNullOrWhiteSpace(Alias) ? $"{Type} {Alias}:{Name}" : $"{Type} {Name}";
    }
}
