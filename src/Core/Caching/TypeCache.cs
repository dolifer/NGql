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

        // OPTIMIZED PATH: Single combined check for both common and nullable types
        // This eliminates the O(n+m) double-loop pattern and does a single pass
        var allTypes = CombinedCommonTypes.Value;
        foreach (var commonType in allTypes)
        {
            if (type.SequenceEqual(commonType.AsSpan()))
            {
                return commonType; // Already interned
            }
        }

        // Standard path for other types
        var typeString = type.ToString();
        return CustomTypes.GetOrAdd(typeString, typeString);
    }

    // Lazy-initialized combined array to avoid allocation at static init time
    private static readonly Lazy<string[]> CombinedCommonTypes = new(() =>
    {
        var combined = new string[CommonTypes.Length + CommonNullableTypes.Length];
        CommonTypes.CopyTo(combined, 0);
        CommonNullableTypes.CopyTo(combined, CommonTypes.Length);
        return combined;
    });

    /// <summary>
    /// Interns a type string for memory efficiency - alias for GetInternedType
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string InternType(ReadOnlySpan<char> type) => GetInternedType(type);
}
