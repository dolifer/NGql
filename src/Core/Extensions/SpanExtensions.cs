using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using NGql.Core.Abstractions;
using NGql.Core.Builders;

namespace NGql.Core.Extensions;

[SuppressMessage("Minor Code Smell", "S3267:Loops should be simplified with \"LINQ\" expressions")]
internal static class SpanExtensions
{
    /// <summary>
    /// Looks up a root field by span through the dictionary's own comparer (case-insensitive for
    /// <see cref="QueryDefinition.Fields"/>), so span and string lookups agree. Allocation-free on
    /// .NET 9+ via the alternate lookup; .NET 8 materializes the key.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryGetValue(this Dictionary<string, FieldDefinition> dictionary, ReadOnlySpan<char> key, [NotNullWhen(true)] out FieldDefinition? value)
    {
#if NET9_0_OR_GREATER
        return dictionary.GetAlternateLookup<ReadOnlySpan<char>>().TryGetValue(key, out value);
#else
        return dictionary.TryGetValue(key.ToString(), out value);
#endif
    }

    /// <summary>
    /// Sets a value by span key. Replacing an existing entry keeps its stored key and, on .NET 9+,
    /// allocates nothing.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void SetValue(this Dictionary<string, FieldDefinition> dictionary, ReadOnlySpan<char> key, FieldDefinition value)
    {
#if NET9_0_OR_GREATER
        dictionary.GetAlternateLookup<ReadOnlySpan<char>>()[key] = value;
#else
        dictionary[key.ToString()] = value;
#endif
    }

    /// <summary>
    /// Get or add a simple field using a span key with optimized path building — FieldChildren variant
    /// </summary>
    public static FieldDefinition GetOrAddSimpleField(this FieldChildren children, ReadOnlySpan<char> fieldName, ReadOnlySpan<char> fieldType, IDictionary<string, object?>? arguments, string? parentPath, Dictionary<string, object?>? metadata, string? fieldNameText = null)
    {
        if (children.TryGetValue(fieldName, out var existingField) && existingField is not null)
        {
            existingField = MergeArgumentsAndMetadata(existingField, arguments, metadata);
            children.Set(fieldName, existingField);
            return existingField;
        }

        var name = fieldNameText ?? fieldName.ToString();
        var field = Helpers.CreateFieldDefinition(name, fieldType, null, arguments, JoinPath(parentPath, name), metadata);
        children.Append(field);
        return field;
    }

    /// <summary>
    /// Get or add a simple field using a span key with optimized path building
    /// </summary>
    internal static FieldDefinition GetOrAddSimpleField(this Dictionary<string, FieldDefinition> fieldDefinitions, ReadOnlySpan<char> fieldName, ReadOnlySpan<char> fieldType, IDictionary<string, object?>? arguments, string? parentPath, Dictionary<string, object?>? metadata, string? fieldNameText = null)
    {
        if (fieldDefinitions.TryGetValue(fieldName, out var existingField))
        {
            var merged = MergeArgumentsAndMetadata(existingField, arguments, metadata);
            if (!ReferenceEquals(merged, existingField)) fieldDefinitions.SetValue(fieldName, merged);
            return merged;
        }

        // One string serves as the dictionary key, the field name and, at the root, its path.
        var name = fieldNameText ?? fieldName.ToString();
        var field = Helpers.CreateFieldDefinition(name, fieldType, null, arguments, JoinPath(parentPath, name), metadata);
        fieldDefinitions[name] = field;
        return field;
    }

    private static string JoinPath(string? parentPath, string name)
        => string.IsNullOrWhiteSpace(parentPath) ? name : string.Concat(parentPath, ".", name);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static FieldDefinition MergeArgumentsAndMetadata(FieldDefinition existing, IDictionary<string, object?>? arguments, Dictionary<string, object?>? metadata)
    {
        if (arguments is { Count: > 0 })
        {
            existing = existing.MergeFieldArguments(arguments);
        }
        if (metadata is { Count: > 0 })
        {
            var mergedMetadata = Helpers.MergeNullableMetadata(existing._metadata, metadata);
            existing = existing with { Metadata = mergedMetadata };
        }
        return existing;
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
