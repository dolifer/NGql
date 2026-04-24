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

/// <summary>
/// Contains buffer size constants used in path construction and field factory operations.
/// These are facts extracted from production code for centralized testing.
/// </summary>
internal static class BufferConstants
{
    /// <summary>
    /// The size of the character buffer used by SpanPathBuilder and FieldFactory for path construction.
    /// This buffer is allocated on the stack for performance-critical path-building operations.
    /// </summary>
    public const int PathBufferSize = 512;

    /// <summary>
    /// The additional margin added to path length estimation to account for separators (dots) and other delimiters.
    /// Used by FieldFactory when estimating whether a path fits within the PathBufferSize.
    /// </summary>
    public const int PathEstimationMargin = 10;
}
