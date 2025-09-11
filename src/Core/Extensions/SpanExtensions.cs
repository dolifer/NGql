using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using NGql.Core.Abstractions;

namespace NGql.Core.Extensions;

[SuppressMessage("Minor Code Smell", "S3267:Loops should be simplified with \"LINQ\" expressions")]
internal static class SpanExtensions
{
    /// <summary>
    /// Try to get value from dictionary using span key without allocating string
    /// </summary>
    public static bool TryGetValue(this SortedDictionary<string, FieldDefinition> dictionary, ReadOnlySpan<char> key, out FieldDefinition? value)
    {
        value = null;
        
        // Fast path: check if any key matches the span length first
        foreach (var kvp in dictionary)
        {
            if (kvp.Key.AsSpan().SequenceEqual(key))
            {
                value = kvp.Value;
                return true;
            }
        }
        
        return false;
    }
    
    /// <summary>
    /// Set value in dictionary using span key, converting to string only when needed
    /// </summary>
    public static void SetValue(this SortedDictionary<string, FieldDefinition> dictionary, ReadOnlySpan<char> key, FieldDefinition value)
    {
        var keyString = key.ToString();
        dictionary[keyString] = value;
    }
    
    /// <summary>
    /// Get or add simple field using span key
    /// </summary>
    public static FieldDefinition GetOrAddSimpleField(this SortedDictionary<string, FieldDefinition> fieldDefinitions, ReadOnlySpan<char> fieldName, string fieldType, SortedDictionary<string, object?>? arguments, string? parentPath, Dictionary<string, object?>? metadata)
    {
        if (fieldDefinitions.TryGetValue(fieldName, out var existingField) && existingField != null)
        {
            return arguments != null ? existingField.MergeFieldArguments(arguments) : existingField;
        }

        // Build path using spans when possible
        if (string.IsNullOrWhiteSpace(parentPath))
        {
            var field = Helpers.CreateFieldDefinition(fieldName, fieldType.AsSpan(), ReadOnlySpan<char>.Empty, arguments, fieldName, metadata);
            fieldDefinitions.SetValue(fieldName, field);
            return field;
        }
        else
        {
            var fieldPath = $"{parentPath}.{fieldName}";
            var field = Helpers.CreateFieldDefinition(fieldName, fieldType.AsSpan(), ReadOnlySpan<char>.Empty, arguments, fieldPath.AsSpan(), metadata);
            fieldDefinitions.SetValue(fieldName, field);
            return field;
        }
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool HasSpaces(this ReadOnlySpan<char> span) => span.IndexOf(' ') != -1;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool HasColons(this ReadOnlySpan<char> span) => span.IndexOf(':') != -1;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool HasDots(this ReadOnlySpan<char> span) => span.IndexOf('.') != -1;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsSimpleField(this ReadOnlySpan<char> span) => !span.HasDots() && !span.HasSpaces() && !span.HasColons();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsDottedField(this ReadOnlySpan<char> span) => span.HasDots() && !span.HasSpaces() && !span.HasColons();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsComplexField(this ReadOnlySpan<char> span) => span.HasSpaces() || span.HasColons();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool HasLetterOrDigit(this ReadOnlySpan<char> span)
    {
        foreach (var c in span)
        {
            if (char.IsLetterOrDigit(c))
            {
                return true;
            }
        }
        return false;
    }
}
