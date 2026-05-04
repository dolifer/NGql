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
    /// The marker for array types as a span.
    /// </summary>
    internal static ReadOnlySpan<char> ArrayTypeMarkerSpan => ArrayTypeMarker.AsSpan();

    /// <summary>
    /// The default field type as span.
    /// </summary>
    internal static ReadOnlySpan<char> DefaultFieldTypeSpan => DefaultFieldType.AsSpan();

    /// <summary>
    /// The object field type as span.
    /// </summary>
    internal static ReadOnlySpan<char> ObjectFieldTypeSpan => ObjectFieldType.AsSpan();

    /// <summary>
    /// The marker for nullable types as a span.
    /// </summary>
    internal static ReadOnlySpan<char> NullableTypeMarkerSpan => NullableTypeMarker.AsSpan();
}
