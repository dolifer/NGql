using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace NGql.Core.Caching;

/// <summary>
/// Simple cache for common GraphQL types with memory and CPU optimizations
/// </summary>
[SuppressMessage("Minor Code Smell", "S3267:Loops should be simplified with \"LINQ\" expressions")]
internal static class TypeCache
{
    private static readonly ConcurrentDictionary<string, string> CustomTypes = new();

    // Pre-intern the most common GraphQL types (ordered by frequency)
    private static readonly string[] CommonTypes =
    [
        Constants.DefaultFieldType,    // "String" - most common
        "Int", "Boolean", "ID",        // Other common scalars
        Constants.ObjectFieldType,     // "object" - for nested fields
        "String!", "Int!", "Boolean!", // Non-null variants
        "Float", "Float!",             // Less common but still frequent
        Constants.ArrayTypeMarker      // "[]" - array marker
    ];

    // Pre-intern common nullable type patterns
    private static readonly string[] CommonNullableTypes =
    [
        "String?", "Int?", "Boolean?", "ID?", "Float?"
    ];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string GetInternedType(ReadOnlySpan<char> type)
    {
        // ULTRA FAST PATH: Empty type defaults to String
        if (type.IsEmpty)
        {
            return Constants.DefaultFieldType;
        }

        // FAST PATH: Check most common types first (ordered by frequency)
        foreach (var common in CommonTypes)
        {
            if (type.SequenceEqual(common.AsSpan()))
            {
                return common; // Already interned
            }
        }

        // FAST PATH: Check nullable variants  
        foreach (var nullable in CommonNullableTypes)
        {
            if (type.SequenceEqual(nullable.AsSpan()))
            {
                return nullable; // Already interned
            }
        }

        // Standard path for other types
        var typeString = type.ToString();
        return CustomTypes.GetOrAdd(typeString, typeString);

    }

    /// <summary>
    /// Interns a type string for memory efficiency - alias for GetInternedType
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string InternType(ReadOnlySpan<char> type) => GetInternedType(type);
}
