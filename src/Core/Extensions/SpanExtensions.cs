using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using NGql.Core.Abstractions;
using NGql.Core.Builders;

namespace NGql.Core.Extensions;

[SuppressMessage("Minor Code Smell", "S3267:Loops should be simplified with \"LINQ\" expressions")]
internal static class SpanExtensions
{
    /// <summary>
    /// Try to get value from a dictionary using a span key without allocating string
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryGetValue(this Dictionary<string, FieldDefinition> dictionary, ReadOnlySpan<char> key, out FieldDefinition? value)
    {
        value = null;
        
        // Fast path: check if any key matches the span length first to avoid unnecessary comparisons
        var keyLength = key.Length;
        foreach (var kvp in dictionary)
        {
            if (kvp.Key.Length != keyLength || !kvp.Key.AsSpan().SequenceEqual(key))
            {
                continue;
            }

            value = kvp.Value;
            return true;
        }
        
        return false;
    }
    
    /// <summary>
    /// Set value in a dictionary using a span key, converting to string only when needed
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void SetValue(this Dictionary<string, FieldDefinition> dictionary, ReadOnlySpan<char> key, FieldDefinition value)
    {
        var keyString = key.ToString();
        dictionary[keyString] = value;
    }
    
    /// <summary>
    /// Get or add a simple field using a span key with optimized path building
    /// </summary>
    public static FieldDefinition GetOrAddSimpleField(this Dictionary<string, FieldDefinition> fieldDefinitions, ReadOnlySpan<char> fieldName, ReadOnlySpan<char> fieldType, IDictionary<string, object?>? arguments, string? parentPath, Dictionary<string, object?>? metadata)
    {
        if (fieldDefinitions.TryGetValue(fieldName, out var existingField) && existingField != null)
        {
            // Merge arguments if provided
            if (arguments is { Count: > 0 })
            {
                existingField = existingField.MergeFieldArguments(arguments);
            }
            
            // Merge metadata if provided
            if (metadata is { Count: > 0 })
            {
                var mergedMetadata = Helpers.MergeNullableMetadata(existingField._metadata, metadata);
                existingField = existingField with { Metadata = mergedMetadata };
            }
            
            // Update the dictionary with the merged field
            fieldDefinitions.SetValue(fieldName, existingField);
            return existingField;
        }

        // Build path using spans when possible
        if (string.IsNullOrWhiteSpace(parentPath))
        {
            var field = Helpers.CreateFieldDefinition(fieldName, fieldType, ReadOnlySpan<char>.Empty, arguments, fieldName, metadata);
            fieldDefinitions.SetValue(fieldName, field);
            return field;
        }

        // Use pooled char array for path building to avoid string concatenation
        var estimatedLength = parentPath.Length + 1 + fieldName.Length;
        if (estimatedLength <= 256)
        {
            Span<char> pathBuffer = stackalloc char[estimatedLength];
            var pathBuilder = new SpanPathBuilder(pathBuffer);
            pathBuilder.Append(parentPath.AsSpan());
            pathBuilder.Append(fieldName);
                
            var field = Helpers.CreateFieldDefinition(fieldName, fieldType, ReadOnlySpan<char>.Empty, arguments, pathBuilder.AsSpan(), metadata);
            fieldDefinitions.SetValue(fieldName, field);
            return field;
        }
        else
        {
            // Fallback to string concatenation for very long paths
            var fieldPath = $"{parentPath}.{fieldName}";
            var field = Helpers.CreateFieldDefinition(fieldName, fieldType, ReadOnlySpan<char>.Empty, arguments, fieldPath.AsSpan(), metadata);
            fieldDefinitions.SetValue(fieldName, field);
            return field;
        }
    }

    /// <summary>
    /// Vectorized field classification - checks all conditions in one pass
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static (bool HasSpaces, bool HasDots, bool HasColons) ClassifyFieldFast(this ReadOnlySpan<char> span)
    {
        bool hasSpaces = false, hasDots = false, hasColons = false;
        
        // ULTRA FAST PATH: Single pass through the span
        foreach (var c in span)
        {
            switch (c)
            {
                case ' ':
                    hasSpaces = true;
                    break;
                case '.':
                    hasDots = true;
                    break;
                case ':':
                    hasColons = true;
                    break;
            }

            // Early exit if all conditions found
            if (hasSpaces && hasDots && hasColons) break;
        }
        
        return (hasSpaces, hasDots, hasColons);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsSimpleField(this ReadOnlySpan<char> span)
    {
        var (hasSpaces, hasDots, hasColons) = span.ClassifyFieldFast();
        return !hasSpaces && !hasDots && !hasColons;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsDottedField(this ReadOnlySpan<char> span)
    {
        var (hasSpaces, hasDots, hasColons) = span.ClassifyFieldFast();
        return hasDots && !hasSpaces && !hasColons;
    }

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

    /// <summary>
    /// Efficient case-insensitive comparison for spans
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool EqualsIgnoreCase(this ReadOnlySpan<char> span, ReadOnlySpan<char> other)
    {
        return span.Equals(other, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Trim whitespace and dots from span efficiently
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlySpan<char> TrimEndDotsAndSpaces(this ReadOnlySpan<char> span)
    {
        var end = span.Length - 1;
        while (end >= 0 && (span[end] == '.' || char.IsWhiteSpace(span[end])))
        {
            end--;
        }
        return span[..(end + 1)];
    }

    /// <summary>
    /// Extract the field name from a segment by taking everything after the last colon
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlySpan<char> ExtractFieldName(this ReadOnlySpan<char> segment)
    {
        var colonIndex = segment.LastIndexOf(':');
        var fieldName = colonIndex == -1 ? segment : segment[(colonIndex + 1)..];
        return fieldName.Trim();
    }
}
