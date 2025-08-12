using System;
using NGql.Core.Abstractions;

namespace NGql.Core.Extensions;

/// <summary>
/// Extensions for working with field types.
/// </summary>
public static class TypeExtensions
{
    /// <summary>
    /// Determines if a field type should be converted to an object type when nested fields are added.
    /// </summary>
    /// <param name="fieldDefinition">The field definition to check.</param>
    /// <returns>True if the type should be converted to object, false if it should preserve its type.</returns>
    public static bool ShouldConvertToObjectType(this FieldDefinition fieldDefinition)
    {
        ArgumentNullException.ThrowIfNull(fieldDefinition);

        // If it's already an object type, no need to convert
        if (fieldDefinition.Type == Constants.ObjectFieldType)
            return false;

        // If it's the default string type, it should be converted
        if (fieldDefinition.Type == Constants.DefaultFieldType || string.IsNullOrWhiteSpace(fieldDefinition.Type))
            return true;

        // Special types like arrays or custom complex types should maintain their type
        // even when nested fields are added
        if (fieldDefinition.IsArray || fieldDefinition.IsNullable || 
            fieldDefinition.Type.Contains('[') || fieldDefinition.Type.Contains(']') ||
            fieldDefinition.Type.EndsWith('?'))
            return false;

        // Check for common primitive types that should be converted
        var lowerType = fieldDefinition.Type.ToLowerInvariant();
        var isPrimitiveType = lowerType == "int" || 
                             lowerType == "integer" ||
                             lowerType == "string" ||
                             lowerType == "boolean" ||
                             lowerType == "bool" ||
                             lowerType == "float" ||
                             lowerType == "double" ||
                             lowerType == "decimal";

        return isPrimitiveType;
    }

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

        if (string.IsNullOrEmpty(fieldDefinition.Type))
            return Constants.DefaultFieldType;

        var type = fieldDefinition.Type;

        // Handle standalone type markers
        if (type == Constants.ArrayTypeMarker)
            return stripArrayMarker ? Constants.DefaultFieldType : Constants.DefaultFieldType + Constants.ArrayTypeMarker;

        if (type == Constants.NullableTypeMarker)
            return stripNullableMarker ? Constants.DefaultFieldType : Constants.DefaultFieldType + Constants.NullableTypeMarker;

        // Handle array notation with brackets
        var arrayStart = type.IndexOf('[');
        if (arrayStart > 0)
        {
            var baseType = type[..arrayStart];
            return stripArrayMarker ? baseType : type;
        }

        // Handle nullable notation
        if (type.EndsWith(Constants.NullableTypeMarker) && type.Length > 1)
        {
            var baseType = type[..^1];
            return stripNullableMarker ? baseType : type;
        }

        return type;
    }

    /// <summary>
    /// Gets just the base type name without any markers.
    /// </summary>
    /// <param name="fieldDefinition">The field definition.</param>
    /// <returns>The base type name without array or nullable markers.</returns>
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
}
