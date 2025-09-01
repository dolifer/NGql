using System.Text.Json.Serialization;

namespace NGql.Core.Abstractions;

/// <summary>
///     Represents a field definition.
/// </summary>
public sealed record FieldDefinition
{
    public FieldDefinition(string name, string? type = null, string? alias = null)
        : this(name, type ?? Constants.DefaultFieldType, alias, null)
    {
    }

    public FieldDefinition(string name, string type, string? alias, SortedDictionary<string, object?>? sortedArguments = null, SortedDictionary<string, FieldDefinition>? fields = null)
    {
        Name = name;
        _alias = alias;
        _type = type;
        _arguments = sortedArguments?.Count > 0 ? sortedArguments : null;
        _fields = fields?.Count > 0 ? fields : null;
    }

    /// <summary>
    /// Gets a value indicating whether this field type is an array.
    /// </summary>
    [JsonIgnore]
    public bool IsArray => _type != null && (_type == Constants.ArrayTypeMarker || _type.Contains('['));

    /// <summary>
    /// Gets a value indicating whether this field type is nullable.
    /// </summary>
    [JsonIgnore]
    public bool IsNullable => _type != null && (_type == Constants.NullableTypeMarker || _type.EndsWith('?'));

    /// <summary>
    ///     The collection of fields related to <see cref="FieldDefinition"/>.
    /// </summary>
    [JsonPropertyName("fields")]
    public SortedDictionary<string, FieldDefinition> Fields
    {
        get => _fields ??= new(StringComparer.OrdinalIgnoreCase);
        set => _fields = value;
    }

    internal SortedDictionary<string, FieldDefinition>? _fields;

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
    public string? Type
    {
        get => _type;
        init => _type = value;
    }

    internal string? _type;

    /// <summary>
    /// The alias of the field, if any. This is used to provide a more readable or meaningful name for the field in queries.
    /// If not specified, the field will use its original name.
    /// </summary>
    [JsonPropertyName("alias")]
    public string? Alias
    {
        get => _alias;
        init => _alias = value;
    }

    internal string? _alias;

    /// <summary></summary>
    [JsonPropertyName("arguments")]
    public SortedDictionary<string, object?> Arguments
    {
        get => _arguments ??= new(StringComparer.OrdinalIgnoreCase);
        set => _arguments = value;
    }

    internal SortedDictionary<string, object?>? _arguments;

    /// <summary>
    /// Metadata associated with the field definition.
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

    public override string ToString()
    {
        if (string.IsNullOrWhiteSpace(Type))
        {
            return string.IsNullOrWhiteSpace(Alias) ? Name : $"{Alias}:{Name}";
        }

        return string.IsNullOrWhiteSpace(Alias) ? $"{Type} {Alias}:{Name}" : $"{Type} {Name}";
    }
}
