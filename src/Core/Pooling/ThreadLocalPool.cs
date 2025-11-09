using System.Runtime.CompilerServices;

namespace NGql.Core.Pooling;

/// <summary>
/// Generic thread-local pool with global fallback for any type.
/// Provides lock-free pooling with thread-local optimization to eliminate contention.
/// </summary>
/// <typeparam name="T">Type to pool (must be a reference type)</typeparam>
internal sealed class ThreadLocalPool<T> where T : class
{
    private const int ThreadLocalCacheSize = 4;
    private const int GlobalPoolSize = 64;

    // Thread-local storage for per-thread caches to eliminate contention
    [ThreadStatic]
    private static ThreadLocalCache? _cache;

    // Global lock-free fallback pool using atomic operations
    private readonly MonitoredLockFreeStack<T> _globalPool = new();
    private volatile int _globalCount;

    private readonly Func<T> _factory;
    private readonly Action<T> _reset;
    private readonly Func<T, bool>? _validateForReturn;
    private readonly string _poolName;

    /// <summary>
    /// Creates a new thread-local pool.
    /// </summary>
    /// <param name="factory">Factory function to create new instances</param>
    /// <param name="reset">Action to reset/clear an instance before returning to pool</param>
    /// <param name="validateForReturn">Optional validation - return false to reject item from pool</param>
    /// <param name="poolName">Name for diagnostics/metrics</param>
    public ThreadLocalPool(
        Func<T> factory,
        Action<T> reset,
        Func<T, bool>? validateForReturn = null,
        string poolName = "unknown")
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        _reset = reset ?? throw new ArgumentNullException(nameof(reset));
        _validateForReturn = validateForReturn;
        _poolName = poolName;
    }

    /// <summary>
    /// Thread-local cache to minimize global pool access
    /// </summary>
    private sealed class ThreadLocalCache
    {
        private readonly T?[] _items = new T?[ThreadLocalCacheSize];
        private int _count;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGet(out T? item)
        {
            if (_count > 0)
            {
                var index = --_count;
                item = _items[index]!;
                _items[index] = null; // Clear reference
                return true;
            }
            item = null;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryReturn(T item)
        {
            if (_count < ThreadLocalCacheSize)
            {
                _items[_count++] = item;
                return true;
            }
            return false;
        }
    }

    /// <summary>
    /// Gets an item from thread-local cache first, then global pool, or creates new
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T Get()
    {
        // ULTRA FAST PATH: Thread-local cache hit (no contention)
        var cache = _cache ??= new ThreadLocalCache();
        if (cache.TryGet(out var item) && item != null)
        {
            ThreadLocalMemoryManager.RecordThreadLocalHit(_poolName);
            return item;
        }

        // FAST PATH: Global lock-free pool
        if (_globalPool.TryPop(out item) && item != null)
        {
            Interlocked.Decrement(ref _globalCount);
            return item;
        }

        // SLOW PATH: Allocate new instance
        ThreadLocalMemoryManager.RecordAllocation(_poolName);
        return _factory();
    }

    /// <summary>
    /// Returns item to thread-local cache first, then global pool
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Return(T? item)
    {
        if (item == null) return;

        // Validate item if validator is provided
        if (_validateForReturn != null && !_validateForReturn(item))
        {
            return; // Item rejected, let GC handle it
        }

        // Reset/clear the item
        _reset(item);

        // ULTRA FAST PATH: Return to thread-local cache (no contention)
        var cache = _cache ??= new ThreadLocalCache();
        if (cache.TryReturn(item))
        {
            return;
        }

        // FAST PATH: Return to global pool if not full (atomic increment with bounds check)
        var newCount = Interlocked.Increment(ref _globalCount);
        if (newCount <= GlobalPoolSize)
        {
            _globalPool.Push(item);
        }
        else
        {
            // Pool is full, undo the increment and let GC handle it
            Interlocked.Decrement(ref _globalCount);
        }
    }

    /// <summary>
    /// Gets the approximate count of items in the global pool (for diagnostics)
    /// </summary>
    public int ApproximateCount => _globalCount;
}
