using System.Diagnostics;
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

    /// <summary>
    /// Records a return operation to thread-local cache
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void RecordThreadLocalReturn(string poolType)
    {
        NGqlTelemetry.RecordPoolOperation(poolType, "return", ThreadLocalCache, -1);
    }

    /// <summary>
    /// Records a return operation to global pool
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void RecordGlobalPoolReturn(string poolType, int newPoolSize)
    {
        NGqlTelemetry.RecordPoolOperation(poolType, "return", GlobalPool, newPoolSize);
    }

    /// <summary>
    /// Records when an object is discarded (pool full)
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void RecordPoolDiscard(string poolType, string reason)
    {
        NGqlTelemetry.RecordPoolOperation(poolType, "discard", reason, -1);
    }

    /// <summary>
    /// Creates a detailed activity for pool operations with comprehensive tagging
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static NGqlActivity StartDetailedPoolActivity(string poolType, string operation)
    {
        return NGqlActivity.StartPooling(poolType, operation)
            .WithTag("pool.implementation", "lock_free")
            .WithTag("pool.strategy", "thread_local_fallback");
    }

    /// <summary>
    /// Records pool efficiency statistics for monitoring
    /// </summary>
    internal static void RecordPoolEfficiency(string poolType, PoolEfficiencyStats stats)
    {
        using var activity = NGqlActivity.StartPooling(poolType, "efficiency_report")
            .WithTag("pool.thread_local_hit_rate", stats.ThreadLocalHitRate)
            .WithTag("pool.global_pool_hit_rate", stats.GlobalPoolHitRate) 
            .WithTag("pool.allocation_rate", stats.AllocationRate)
            .WithTag("pool.total_operations", stats.TotalOperations);

        // These could be exposed as gauges for monitoring dashboards
        activity.AddEvent("efficiency_calculated", new ActivityTagsCollection
        {
            ["thread_local_efficiency"] = stats.ThreadLocalHitRate,
            ["global_pool_efficiency"] = stats.GlobalPoolHitRate,
            ["cache_effectiveness"] = stats.ThreadLocalHitRate + stats.GlobalPoolHitRate
        });
    }

    /// <summary>
    /// Pool efficiency statistics for detailed monitoring
    /// </summary>
    internal readonly struct PoolEfficiencyStats
    {
        public readonly double ThreadLocalHitRate;
        public readonly double GlobalPoolHitRate;
        public readonly double AllocationRate;
        public readonly long TotalOperations;

        public PoolEfficiencyStats(long threadLocalHits, long globalHits, long allocations)
        {
            TotalOperations = threadLocalHits + globalHits + allocations;
            ThreadLocalHitRate = TotalOperations > 0 ? (double)threadLocalHits / TotalOperations : 0;
            GlobalPoolHitRate = TotalOperations > 0 ? (double)globalHits / TotalOperations : 0;
            AllocationRate = TotalOperations > 0 ? (double)allocations / TotalOperations : 0;
        }
    }
}