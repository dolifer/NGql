using System.Runtime.CompilerServices;

namespace NGql.Core.Pooling;

/// <summary>
/// Lock-free pool for SortedDictionary arguments with thread-local optimization to eliminate contention
/// </summary>
internal static class LockFreeArgumentsPool
{
    private const int ThreadLocalCacheSize = 4;
    private const int GlobalPoolSize = 64;
    
    // Thread-local storage for per-thread caches to eliminate contention
    [ThreadStatic]
    private static ThreadLocalCache? _cache;
    
    // Global lock-free fallback pool using atomic operations with monitoring
    private static readonly MonitoredLockFreeStack<SortedDictionary<string, object?>> _globalPool = new();
    private static volatile int _globalCount = 0;

    /// <summary>
    /// Thread-local cache to minimize global pool access
    /// </summary>
    private sealed class ThreadLocalCache
    {
        private readonly SortedDictionary<string, object?>?[] _items = new SortedDictionary<string, object?>?[ThreadLocalCacheSize];
        private int _count = 0;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGet(out SortedDictionary<string, object?> dict)
        {
            if (_count > 0)
            {
                var index = --_count;
                dict = _items[index]!;
                _items[index] = null; // Clear reference
                ThreadLocalMemoryManager.RecordThreadLocalHit("arguments");
                return true;
            }
            dict = null!;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryReturn(SortedDictionary<string, object?> dict)
        {
            if (_count < ThreadLocalCacheSize)
            {
                dict.Clear();
                _items[_count++] = dict;
                return true;
            }
            return false;
        }
    }

    /// <summary>
    /// Gets a dictionary from thread-local cache first, then global pool, or creates new
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static SortedDictionary<string, object?> Get()
    {
        // ULTRA FAST PATH: Thread-local cache hit (no contention)
        var cache = _cache ??= new ThreadLocalCache();
        if (cache.TryGet(out var dict))
        {
            return dict;
        }

        // FAST PATH: Global lock-free pool
        if (_globalPool.TryPop(out dict))
        {
            Interlocked.Decrement(ref _globalCount);
            return dict;
        }

        // SLOW PATH: Allocate new dictionary
        ThreadLocalMemoryManager.RecordAllocation("arguments");
        return new SortedDictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Returns dictionary to thread-local cache first, then global pool
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Return(SortedDictionary<string, object?> dict)
    {
        if (dict == null) return;

        // ULTRA FAST PATH: Return to thread-local cache (no contention)
        var cache = _cache ??= new ThreadLocalCache();
        if (cache.TryReturn(dict))
        {
            return;
        }

        // FAST PATH: Return to global pool if not full
        if (_globalCount < GlobalPoolSize)
        {
            dict.Clear();
            _globalPool.Push(dict);
            Interlocked.Increment(ref _globalCount);
        }
        // If global pool is full, let GC handle it
    }

    /// <summary>
    /// Gets a pooled dictionary populated from source
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static PooledArguments GetPooled(Dictionary<string, object?> source)
    {
        var dict = Get();
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
        var dict = Get();
        if (source != null)
        {
            foreach (var kvp in source)
                dict[kvp.Key] = kvp.Value;
        }
        return new PooledArguments(dict);
    }
}

/// <summary>
/// Lock-free stack implementation using atomic operations
/// </summary>
/// <typeparam name="T">Type of items in the stack</typeparam>
internal sealed class LockFreeStack<T> where T : class
{
    private volatile Node? _head;

    private sealed class Node
    {
        public readonly T Item;
        public Node? Next;

        public Node(T item) => Item = item;
    }

    /// <summary>
    /// Pushes an item onto the stack using compare-and-swap
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Push(T item)
    {
        var newNode = new Node(item);
        Node? currentHead;
        do
        {
            currentHead = _head;
            newNode.Next = currentHead;
        } while (Interlocked.CompareExchange(ref _head, newNode, currentHead) != currentHead);
    }

    /// <summary>
    /// Attempts to pop an item from the stack using compare-and-swap
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryPop(out T? item)
    {
        Node? currentHead;
        do
        {
            currentHead = _head;
            if (currentHead == null)
            {
                item = null;
                return false;
            }
        } while (Interlocked.CompareExchange(ref _head, currentHead.Next, currentHead) != currentHead);

        item = currentHead.Item;
        return true;
    }
}