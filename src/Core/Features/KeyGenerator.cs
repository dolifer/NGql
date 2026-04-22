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
        var counter = 1;
        Span<char> buffer = stackalloc char[baseKey.Length + 16]; // Reserve space for "_" + counter
        baseKey.AsSpan().CopyTo(buffer);
        buffer[baseKey.Length] = '_';

        do
        {
            var counterSpan = buffer[(baseKey.Length + 1)..];
            if (counter.TryFormat(counterSpan, out var charsWritten))
            {
                var keySpan = buffer[..(baseKey.Length + 1 + charsWritten)];
                var uniqueKey = new string(keySpan);

                if (!existingKeySet.Contains(uniqueKey))
                {
                    return uniqueKey;
                }
            }
            counter++;
        }
        while (counter < 10000); // Safety limit

        // Fallback for very large counters
        return $"{baseKey}_{counter}";
    }
}
