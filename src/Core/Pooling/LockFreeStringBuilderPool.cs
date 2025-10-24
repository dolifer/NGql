using System.Runtime.CompilerServices;
using System.Text;

namespace NGql.Core.Pooling;

/// <summary>
/// Lock-free pool for StringBuilder instances with thread-local optimization
/// </summary>
internal static class LockFreeStringBuilderPool
{
    private const int ThreadLocalCacheSize = 3;
    private const int GlobalPoolSize = 32;
    private const int MaxCapacity = 2048; // Larger capacity limit for better reuse
    private const int InitialCapacity = 256;
    
    // Thread-local storage for per-thread caches
    [ThreadStatic]
    private static ThreadLocalCache? _cache;
    
    // Global lock-free fallback pool with monitoring
    private static readonly MonitoredLockFreeStack<StringBuilder> _globalPool = new();
    private static volatile int _globalCount = 0;

    /// <summary>
    /// Thread-local cache for StringBuilder instances
    /// </summary>
    private sealed class ThreadLocalCache
    {
        private readonly StringBuilder?[] _items = new StringBuilder?[ThreadLocalCacheSize];
        private int _count = 0;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGet(out StringBuilder sb)
        {
            if (_count > 0)
            {
                var index = --_count;
                sb = _items[index]!;
                _items[index] = null;
                ThreadLocalMemoryManager.RecordThreadLocalHit("stringbuilder");
                return true;
            }
            sb = null!;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryReturn(StringBuilder sb)
        {
            if (_count < ThreadLocalCacheSize && sb.Capacity <= MaxCapacity)
            {
                sb.Clear();
                _items[_count++] = sb;
                return true;
            }
            return false;
        }
    }

    /// <summary>
    /// Gets a StringBuilder from thread-local cache first, then global pool, or creates new
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static StringBuilder Get()
    {
        // ULTRA FAST PATH: Thread-local cache hit
        var cache = _cache ??= new ThreadLocalCache();
        if (cache.TryGet(out var sb))
        {
            return sb;
        }

        // FAST PATH: Global lock-free pool
        if (_globalPool.TryPop(out sb))
        {
            Interlocked.Decrement(ref _globalCount);
            return sb;
        }

        // SLOW PATH: Allocate new StringBuilder
        ThreadLocalMemoryManager.RecordAllocation("stringbuilder");
        return new StringBuilder(InitialCapacity);
    }

    /// <summary>
    /// Returns StringBuilder to thread-local cache first, then global pool
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Return(StringBuilder sb)
    {
        if (sb == null) return;

        // Skip if capacity is too large (prevents memory bloat)
        if (sb.Capacity > MaxCapacity) return;

        // ULTRA FAST PATH: Return to thread-local cache
        var cache = _cache ??= new ThreadLocalCache();
        if (cache.TryReturn(sb))
        {
            return;
        }

        // FAST PATH: Return to global pool if not full
        if (_globalCount < GlobalPoolSize)
        {
            sb.Clear();
            _globalPool.Push(sb);
            Interlocked.Increment(ref _globalCount);
        }
    }

    /// <summary>
    /// Gets a pooled StringBuilder instance
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static PooledStringBuilder GetPooled()
    {
        return new PooledStringBuilder(Get());
    }
}