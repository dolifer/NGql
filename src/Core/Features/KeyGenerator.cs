using NGql.Core.Abstractions;
using NGql.Core.Pooling;

namespace NGql.Core.Features;

/// <summary>
/// Utility class for generating unique keys.
/// </summary>
internal static class KeyGenerator
{
    /// <summary>
    /// Generates a unique key by appending a counter suffix if the base key already exists.
    /// </summary>
    /// <param name="baseKey">The base key to make unique</param>
    /// <param name="existingKeys">Collection of existing keys to check against</param>
    /// <returns>A unique key that doesn't exist in the collection</returns>
    internal static string GenerateUniqueKey(string baseKey, IEnumerable<string> existingKeys)
    {
        using var pooledSet = LockFreeHashSetPool.GetPooled(existingKeys);
        var existingKeySet = pooledSet.Set;

        if (!existingKeySet.Contains(baseKey))
        {
            return baseKey;
        }

        return GenerateUniqueKeyCore(baseKey, existingKeySet);
    }

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
        Span<char> buffer = stackalloc char[baseKey.Length + 16];
        baseKey.AsSpan().CopyTo(buffer);
        buffer[baseKey.Length] = '_';

        // Loop terminates as soon as the formatted candidate isn't in the existingKeySet.
        // existingKeySet has finite capacity bounded by the number of fields in the merged tree,
        // so a not-present key is always reachable; counter is int, more than enough headroom.
#pragma warning disable S1994
        for (int counter = 1; ; counter++)
#pragma warning restore S1994
        {
            counter.TryFormat(buffer[(baseKey.Length + 1)..], out var charsWritten);
            var uniqueKey = new string(buffer[..(baseKey.Length + 1 + charsWritten)]);

            if (!existingKeySet.Contains(uniqueKey))
            {
                return uniqueKey;
            }
        }
    }
}
