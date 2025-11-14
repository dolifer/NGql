using System.Runtime.CompilerServices;

namespace NGql.Core.Pooling;

/// <summary>
/// Lock-free pool for HashSet&lt;string&gt; instances with thread-local optimization
/// </summary>
internal static class LockFreeHashSetPool
{
    private const int MaxSize = 128; // Prevent memory bloat from large sets

    private static readonly ThreadLocalPool<HashSet<string>> _pool = new(
        factory: () => new HashSet<string>(StringComparer.OrdinalIgnoreCase),
        reset: set => set.Clear(),
        validateForReturn: set => set.Count <= MaxSize, // Skip very large sets to prevent memory bloat
        poolName: "hashset"
    );

    /// <summary>
    /// Gets an empty pooled HashSet (for warmup/testing)
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static PooledHashSet GetPooled() => new(_pool.Get());

    /// <summary>
    /// Gets a pooled HashSet populated from source
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static PooledHashSet GetPooled(HashSet<string> source)
    {
        var set = _pool.Get();
        foreach (var item in source)
            set.Add(item);
        return new PooledHashSet(set);
    }

    /// <summary>
    /// Gets a pooled HashSet populated from source
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static PooledHashSet GetPooled(IEnumerable<string> source)
    {
        var set = _pool.Get();
        foreach (var item in source)
            set.Add(item);
        return new PooledHashSet(set);
    }

    /// <summary>
    /// Returns HashSet to the pool
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Major Code Smell", "S3398:Move this method inside 'PooledHashSet'", Justification = "ref struct cannot contain static members; method must access static pool field")]
    private static void Return(HashSet<string> set) => _pool.Return(set);

    /// <summary>
    /// Zero-allocation ref struct wrapper with lock-free pooling
    /// </summary>
    internal readonly ref struct PooledHashSet
    {
        public readonly HashSet<string> Set;
        internal PooledHashSet(HashSet<string> set) => Set = set;
        public void Dispose() => Return(Set);
    }
}
