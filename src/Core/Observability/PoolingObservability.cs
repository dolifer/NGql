using System.Runtime.CompilerServices;

namespace NGql.Core.Observability;

/// <summary>
/// Specialized observability for pooling operations with detailed cache performance tracking
/// </summary>
internal static class PoolingObservability
{
    // Cache levels for standardized naming
    private const string ThreadLocalCache = "thread_local";
    private const string GlobalPool = "global_pool";
    private const string Allocation = "allocation";

    /// <summary>
    /// Records a thread-local cache hit with observability
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void RecordThreadLocalHit(string poolType)
    {
        // Metrics
        NGqlTelemetry.RecordPoolOperation(poolType, "get", ThreadLocalCache, -1);

        // Tracing (only if active listener exists)
        using var activity = NGqlActivity.StartPooling(poolType, "get")
            .WithPoolingTags(poolType, ThreadLocalCache, -1);
    }

    /// <summary>
    /// Records a global pool hit with observability
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void RecordGlobalPoolHit(string poolType, int currentPoolSize)
    {
        // Metrics
        NGqlTelemetry.RecordPoolOperation(poolType, "get", GlobalPool, currentPoolSize);

        // Tracing (only if active listener exists)
        using var activity = NGqlActivity.StartPooling(poolType, "get")
            .WithPoolingTags(poolType, GlobalPool, currentPoolSize);
    }

    /// <summary>
    /// Records a new allocation (cache miss) with observability
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void RecordAllocation(string poolType)
    {
        // Metrics
        NGqlTelemetry.RecordPoolOperation(poolType, "allocate", Allocation, 0);

        // Tracing (only if active listener exists)  
        using var activity = NGqlActivity.StartPooling(poolType, "allocate")
            .WithPoolingTags(poolType, Allocation, 0);
    }

}