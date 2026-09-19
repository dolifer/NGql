using System.Buffers;
using NGql.Core.Abstractions;
using NGql.Core.Pooling;

namespace NGql.Core.Features;

/// <summary>
/// Utility class for generating unique keys.
/// </summary>
internal static class KeyGenerator
{
    /// <summary>
    /// Generates a unique key using a <see cref="FieldMergeIndex"/>'s live key set and
    /// per-base-name suffix counter instead of rebuilding a <see cref="HashSet{T}"/> from every
    /// existing key: O(1) amortized per inserted field across a chain of <c>Include()</c> calls.
    /// </summary>
    internal static string GenerateUniqueKey(FieldMergeIndex mergeIndex, Dictionary<string, FieldDefinition> fields, string baseKey)
        => mergeIndex.NextUniqueKey(fields, baseKey);

    /// <summary>
    /// Generates a unique key from field definitions' effective names (zero-alloc for span iteration).
    /// </summary>
    internal static string GenerateUniqueKey(string baseKey, ReadOnlySpan<FieldDefinition> fields)
    {
        using var pooledSet = LockFreeHashSetPool.GetPooled();
        var existingKeySet = pooledSet.Set;

        // Populate set with effective names from span — zero-alloc iteration
        for (int i = 0; i < fields.Length; i++)
            existingKeySet.Add(fields[i]._effectiveName);

        if (!existingKeySet.Contains(baseKey))
        {
            return baseKey;
        }

        return GenerateUniqueKeyCore(baseKey, existingKeySet);
    }

    private static string GenerateUniqueKeyCore(string baseKey, HashSet<string> existingKeySet)
    {
        // 16 chars holds "_" plus a 15-digit counter — counter is int, max ~10 digits.
        var length = checked(baseKey.Length + 16);
        char[]? rented = length > 512 ? ArrayPool<char>.Shared.Rent(length) : null;
        Span<char> buffer = rented is null ? stackalloc char[length] : rented;
        try
        {
            baseKey.AsSpan().CopyTo(buffer);
            buffer[baseKey.Length] = '_';
#if NET9_0_OR_GREATER
            var lookup = existingKeySet.GetAlternateLookup<ReadOnlySpan<char>>();
#endif
#pragma warning disable S1994
            for (int counter = 1; ; counter++)
#pragma warning restore S1994
            {
                counter.TryFormat(buffer[(baseKey.Length + 1)..], out var charsWritten);
                var candidate = buffer[..(baseKey.Length + 1 + charsWritten)];
#if NET9_0_OR_GREATER
                if (!lookup.Contains(candidate)) return new string(candidate);
#else
                var uniqueKey = new string(candidate);
                if (!existingKeySet.Contains(uniqueKey)) return uniqueKey;
#endif
            }
        }
        finally
        {
            if (rented is not null) ArrayPool<char>.Shared.Return(rented, clearArray: true);
        }
    }
}
