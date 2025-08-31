using System;

namespace NGql.Core.Extensions;

/// <summary>
/// Ref struct for holding field type and name as spans to avoid string allocations
/// </summary>
internal readonly ref struct SpanFieldInfo
{
    public readonly ReadOnlySpan<char> FieldType;
    public readonly ReadOnlySpan<char> FieldName;

    public SpanFieldInfo(ReadOnlySpan<char> fieldType, ReadOnlySpan<char> fieldName)
    {
        FieldType = fieldType;
        FieldName = fieldName;
    }

    public readonly (string fieldType, string fieldName) ToStrings()
    {
        return (FieldType.ToString(), FieldName.ToString());
    }
}
