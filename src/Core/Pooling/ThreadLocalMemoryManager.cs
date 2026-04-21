using System.Runtime.CompilerServices;
using NGql.Core.Observability;

namespace NGql.Core.Pooling;

/// <summary>
/// Centralized thread-local memory management with integrated observability.
/// Provides thread-affinity optimizations and comprehensive telemetry.
/// </summary>
internal static class ThreadLocalMemoryManager
{
    /// <summary>
    /// Provides statistics about thread-local cache efficiency
    /// </summary>
    public readonly struct CacheStats
    {
        public readonly long ThreadLocalHits;
        public readonly long GlobalPoolHits;
        public readonly long Allocations;
        public readonly double ThreadLocalHitRatio;

        internal CacheStats(long threadLocalHits, long globalPoolHits, long allocations)
        {
            ThreadLocalHits = threadLocalHits;
            GlobalPoolHits = globalPoolHits;
            Allocations = allocations;
            var totalRequests = threadLocalHits + globalPoolHits + allocations;
            ThreadLocalHitRatio = totalRequests > 0 ? (double)threadLocalHits / totalRequests : 0.0;
        }
    }

    // Thread-local counters for performance monitoring
    [ThreadStatic]
    private static long _threadLocalHits;
    [ThreadStatic] 
    private static long _globalPoolHits;
    [ThreadStatic]
    private static long _allocations;

    /// <summary>
    /// Records a thread-local cache hit with observability
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void RecordThreadLocalHit(string poolType)
    {
        _threadLocalHits++;
        PoolingObservability.RecordThreadLocalHit(poolType);
    }

    /// <summary>
    /// Records a global pool hit with observability
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void RecordGlobalPoolHit(string poolType, int poolSize = -1)
    {
        _globalPoolHits++;
        PoolingObservability.RecordGlobalPoolHit(poolType, poolSize);
    }

    /// <summary>
    /// Records a new allocation with observability
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void RecordAllocation(string poolType)
    {
        _allocations++;
        PoolingObservability.RecordAllocation(poolType);
    }

    /// <summary>
    /// Gets cache performance statistics for the current thread
    /// </summary>
    internal static CacheStats GetThreadStats()
    {
        return new CacheStats(_threadLocalHits, _globalPoolHits, _allocations);
    }

    /// <summary>
    /// Resets performance counters for the current thread (useful for benchmarking)
    /// </summary>
    internal static void ResetThreadStats()
    {
        _threadLocalHits = 0;
        _globalPoolHits = 0;
        _allocations = 0;
    }

    /// <summary>
    /// Reports pool efficiency for monitoring and alerting
    /// </summary>
    internal static void ReportPoolEfficiency(string poolType)
    {
        var stats = GetThreadStats();
        var efficiency = new PoolingObservability.PoolEfficiencyStats(
            stats.ThreadLocalHits, 
            stats.GlobalPoolHits, 
            stats.Allocations);

        PoolingObservability.RecordPoolEfficiency(poolType, efficiency);
    }

    /// <summary>
    /// Warms up thread-local caches with observability tracking
    /// </summary>
    internal static void WarmupThreadLocalCaches()
    {
        using var activity = NGqlActivity.StartQuery("cache_warmup")
            .WithTag("operation.type", "warmup");

        activity.WithObservability(() =>
        {
            // Pre-populate caches with a few instances to avoid allocation spikes
            var dict1 = LockFreeArgumentsPool.GetPooled(new Dictionary<string, object?>());
            var dict2 = LockFreeArgumentsPool.GetPooled(new Dictionary<string, object?>());
            dict1.Dispose();
            dict2.Dispose();

            var sb1 = LockFreeStringBuilderPool.GetPooled();
            var sb2 = LockFreeStringBuilderPool.GetPooled();
            sb1.Dispose();
            sb2.Dispose();

            var set1 = LockFreeHashSetPool.GetPooled();
            set1.Dispose();
        }, "warmup_caches");
    }

}

/// <summary>
/// Enhanced lock-free stack with performance monitoring integration
/// </summary>
/// <typeparam name="T">Type of items in the stack</typeparam>
internal sealed class MonitoredLockFreeStack<T> where T : class
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
    /// Attempts to pop an item from the stack using compare-and-swap with monitoring
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryPop(out T item)
    {
        Node? currentHead;
        do
        {
            currentHead = _head;
            if (currentHead == null)
            {
                item = default!;
                return false;
            }
        } while (Interlocked.CompareExchange(ref _head, currentHead.Next, currentHead) != currentHead);

        item = currentHead.Item;
        ThreadLocalMemoryManager.RecordGlobalPoolHit("unknown", -1);
        return true;
    }
}
