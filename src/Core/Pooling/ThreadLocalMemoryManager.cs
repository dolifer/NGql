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
    /// Records a thread-local cache hit with observability
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void RecordThreadLocalHit(string poolType)
    {
        PoolingObservability.RecordThreadLocalHit(poolType);
    }

    /// <summary>
    /// Records a global pool hit with observability
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void RecordGlobalPoolHit(string poolType, int poolSize = -1)
    {
        PoolingObservability.RecordGlobalPoolHit(poolType, poolSize);
    }

    /// <summary>
    /// Records a new allocation with observability
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void RecordAllocation(string poolType)
    {
        PoolingObservability.RecordAllocation(poolType);
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
