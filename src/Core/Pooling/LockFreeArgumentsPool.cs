using System.Runtime.CompilerServices;

namespace NGql.Core.Pooling;

/// <summary>
/// Lock-free pool for SortedDictionary arguments with thread-local optimization to eliminate contention
/// </summary>
internal static class LockFreeArgumentsPool
{
    private static readonly ThreadLocalPool<SortedDictionary<string, object?>> _pool = new(
        factory: () => new SortedDictionary<string, object?>(StringComparer.OrdinalIgnoreCase),
        reset: dict => dict.Clear(),
        poolName: "arguments"
    );

    /// <summary>
    /// Gets a pooled dictionary populated from source
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static PooledArguments GetPooled(Dictionary<string, object?> source)
    {
        var dict = _pool.Get();
        foreach (var kvp in source)
            dict[kvp.Key] = kvp.Value;
        return new PooledArguments(dict);
    }

    /// <summary>
    /// Gets a pooled dictionary populated from source
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static PooledArguments GetPooled(SortedDictionary<string, object?>? source)
    {
        var dict = _pool.Get();
        if (source != null)
        {
            foreach (var kvp in source)
                dict[kvp.Key] = kvp.Value;
        }
        return new PooledArguments(dict);
    }

    /// <summary>
    /// Returns dictionary to the pool
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Major Code Smell", "S3398:Move this method inside 'PooledArguments'", Justification = "ref struct cannot contain static members; method must access static pool field")]
    private static void Return(SortedDictionary<string, object?> dict) => _pool.Return(dict);

    /// <summary>
    /// Zero-allocation ref struct wrapper with lock-free pooling
    /// </summary>
    internal readonly ref struct PooledArguments
    {
        public readonly SortedDictionary<string, object?> Dictionary;
        internal PooledArguments(SortedDictionary<string, object?> dictionary) => Dictionary = dictionary;
        public void Dispose() => Return(Dictionary);
    }
}
