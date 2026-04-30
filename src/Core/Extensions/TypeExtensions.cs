using NGql.Core.Abstractions;

namespace NGql.Core.Extensions;

/// <summary>
/// Extensions for working with field types.
/// </summary>
internal static class TypeExtensions
{
    /// <summary>
    /// Determines if a field type should be converted to an object type when nested fields are added.
    /// </summary>
    /// <param name="fieldDefinition">The field definition to check.</param>
    /// <returns>True if the type should be converted to an object, false if it should preserve its type.</returns>
    public static bool ShouldConvertToObjectType(this FieldDefinition fieldDefinition)
    {
        ArgumentNullException.ThrowIfNull(fieldDefinition);

        var type = fieldDefinition._type;

        // Already object — no conversion needed.
        if (type == Constants.ObjectFieldType) return false;

        // Default String type or unset — convert.
        if (type == Constants.DefaultFieldType || string.IsNullOrWhiteSpace(type)) return true;

        // Array marker stays as-is.
        if (type == Constants.ArrayTypeMarker) return false;

        // Array / nullable / type-decorated names retain their type.
        if (HasArrayOrNullableMarker(fieldDefinition, type)) return false;

        // Primitive scalars promote to object when subfields are added.
        return IsPrimitiveTypeName(type);
    }

    private static bool HasArrayOrNullableMarker(FieldDefinition fieldDefinition, string type)
        => fieldDefinition.IsArray
        || fieldDefinition.IsNullable
        || type.Contains('[')
        || type.Contains(']')
        || type.EndsWith('?');

    private static readonly HashSet<string> PrimitiveTypeNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "int", "integer", "string", "boolean", "bool", "float", "double", "decimal",
    };

    private static bool IsPrimitiveTypeName(string type) => PrimitiveTypeNames.Contains(type);

    /// <summary>
    /// Gets the core type name.
    /// </summary>
    /// <param name="fieldDefinition">The field definition.</param>
    /// <param name="stripArrayMarker">Whether to strip the array marker.</param>
    /// <param name="stripNullableMarker">Whether to strip the nullable marker.</param>
    /// <returns>The processed type name according to the parameters.</returns>
    public static string GetCoreTypeName(this FieldDefinition fieldDefinition, bool stripArrayMarker = false, bool stripNullableMarker = false)
    {
        ArgumentNullException.ThrowIfNull(fieldDefinition);

        if (string.IsNullOrEmpty(fieldDefinition.Type)) return Constants.DefaultFieldType;

        var type = fieldDefinition.Type;
        if (TryHandleStandaloneMarker(type, stripArrayMarker, stripNullableMarker, out var standalone))
            return standalone;

        return ResolveDecoratedType(type, stripArrayMarker, stripNullableMarker);
    }

    private static bool TryHandleStandaloneMarker(string type, bool stripArrayMarker, bool stripNullableMarker, out string result)
    {
        if (type == Constants.ArrayTypeMarker)
        {
            result = stripArrayMarker ? Constants.DefaultFieldType : Constants.DefaultFieldType + Constants.ArrayTypeMarker;
            return true;
        }
        if (type == Constants.NullableTypeMarker)
        {
            result = stripNullableMarker ? Constants.DefaultFieldType : Constants.DefaultFieldType + Constants.NullableTypeMarker;
            return true;
        }
        result = string.Empty;
        return false;
    }

    private static string ResolveDecoratedType(string type, bool stripArrayMarker, bool stripNullableMarker)
    {
        var typeSpan = type.AsSpan();

        var arrayStart = typeSpan.IndexOf('[');
        if (arrayStart > 0)
        {
            return stripArrayMarker ? typeSpan[..arrayStart].ToString() : type;
        }

        if (typeSpan.EndsWith(Constants.NullableTypeMarkerSpan) && typeSpan.Length > 1)
        {
            return stripNullableMarker ? typeSpan[..^1].ToString() : type;
        }

        return type;
    }

    /// <summary>
    /// Gets just the base type name without any markers.
    /// </summary>
    /// <param name="fieldDefinition">The field definition.</param>
    /// <returns>The base type name without an array or nullable markers.</returns>
    public static string GetBaseTypeName(this FieldDefinition fieldDefinition)
        => GetCoreTypeName(fieldDefinition, true, true);

    /// <summary>
    /// Determines if the type is a pure type marker ([] or ?)
    /// </summary>
    /// <param name="fieldDefinition">The field definition to check.</param>
    /// <returns>True if the type is a pure marker, false otherwise.</returns>
    public static bool IsPureTypeMarker(this FieldDefinition fieldDefinition)
    {
        ArgumentNullException.ThrowIfNull(fieldDefinition);

        return fieldDefinition.Type
            is Constants.ArrayTypeMarker
            or Constants.NullableTypeMarker;
    }

    /// <summary>
    /// Determines if a type string represents an array type.
    /// </summary>
    /// <param name="type">The type string to check.</param>
    /// <returns>True if the type is an array type, false otherwise.</returns>
    public static bool IsArrayType(this string? type)
        => type != null && (type == Constants.ArrayTypeMarker || type.Contains('['));

    /// <summary>
    /// Determines if a type string represents a nullable type.
    /// </summary>
    /// <param name="type">The type string to check.</param>
    /// <returns>True if the type is nullable, false otherwise.</returns>
    public static bool IsNullableType(this string? type)
        => type != null && (type == Constants.NullableTypeMarker || type.EndsWith('?'));
}
