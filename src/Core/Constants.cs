namespace NGql.Core;

/// <summary>
/// Contains constant values used throughout the application.
/// </summary>
public static class Constants
{
    /// <summary>
    /// The default field type when no type is specified.
    /// </summary>
    public const string DefaultFieldType = "String";

    /// <summary>
    /// The field type used for object/container fields that have nested fields.
    /// </summary>
    public const string ObjectFieldType = "object";

    /// <summary>
    /// The marker for array types.
    /// </summary>
    public const string ArrayTypeMarker = "[]";

    /// <summary>
    /// The marker for nullable types.
    /// </summary>
    public const string NullableTypeMarker = "?";

    /// <summary>
    /// Empty arguments dictionary for performance optimization.
    /// </summary>
    internal static readonly SortedDictionary<string, object?> EmptyArguments = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Empty metadata dictionary for performance optimization.
    /// </summary>
    public static readonly Dictionary<string, object?> EmptyMetadata = [];
}
