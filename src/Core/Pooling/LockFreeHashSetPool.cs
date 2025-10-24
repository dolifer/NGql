using System.Runtime.CompilerServices;

namespace NGql.Core.Pooling;

/// <summary>
/// Lock-free pool for HashSet&lt;string&gt; instances with thread-local optimization
/// </summary>
internal static class LockFreeHashSetPool
{
    private const int ThreadLocalCacheSize = 2;
    private const int GlobalPoolSize = 24;
    private const int MaxSize = 128; // Prevent memory bloat from large sets
    
    // Thread-local storage for per-thread caches
    [ThreadStatic]
    private static ThreadLocalCache? _cache;
    
    // Global lock-free fallback pool with monitoring
    private static readonly MonitoredLockFreeStack<HashSet<string>> _globalPool = new();
    private static volatile int _globalCount = 0;

    /// <summary>
    /// Thread-local cache for HashSet instances
    /// </summary>
    private sealed class ThreadLocalCache
    {
        private readonly HashSet<string>?[] _items = new HashSet<string>?[ThreadLocalCacheSize];
        private int _count = 0;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGet(out HashSet<string> set)
        {
            if (_count > 0)
            {
                var index = --_count;
                set = _items[index]!;
                _items[index] = null;
                ThreadLocalMemoryManager.RecordThreadLocalHit("hashset");
                return true;
            }
            set = null!;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryReturn(HashSet<string> set)
        {
            // Only cache reasonably sized sets to prevent memory bloat
            if (_count < ThreadLocalCacheSize && set.Count <= MaxSize)
            {
                set.Clear();
                _items[_count++] = set;
                return true;
            }
            return false;
        }
    }

    /// <summary>
    /// Gets a HashSet from thread-local cache first, then global pool, or creates new
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static HashSet<string> Get()
    {
        // ULTRA FAST PATH: Thread-local cache hit
        var cache = _cache ??= new ThreadLocalCache();
        if (cache.TryGet(out var set))
        {
            return set;
        }

        // FAST PATH: Global lock-free pool
        if (_globalPool.TryPop(out set))
        {
            Interlocked.Decrement(ref _globalCount);
            return set;
        }

        // SLOW PATH: Allocate new HashSet
        ThreadLocalMemoryManager.RecordAllocation("hashset");
        return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Returns HashSet to thread-local cache first, then global pool
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Return(HashSet<string> set)
    {
        if (set == null) return;

        // Skip very large sets to prevent memory bloat
        if (set.Count > MaxSize) return;

        // ULTRA FAST PATH: Return to thread-local cache
        var cache = _cache ??= new ThreadLocalCache();
        if (cache.TryReturn(set))
        {
            return;
        }

        // FAST PATH: Return to global pool if not full
        if (_globalCount < GlobalPoolSize)
        {
            set.Clear();
            _globalPool.Push(set);
            Interlocked.Increment(ref _globalCount);
        }
    }

    /// <summary>
    /// Gets a pooled HashSet populated from source
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static PooledHashSet GetPooled(IEnumerable<string> source)
    {
        var set = Get();
        foreach (var item in source)
            set.Add(item);
        return new PooledHashSet(set);
    }

    /// <summary>
    /// Gets an empty pooled HashSet
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static PooledHashSet GetPooled()
    {
        return new PooledHashSet(Get());
    }
}