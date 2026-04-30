using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Runtime.CompilerServices;

namespace NGql.Core.Observability;

/// <summary>
/// Centralized telemetry provider for NGql using .NET's built-in observability primitives.
/// Provides OpenTelemetry-compatible traces and metrics using only BCL components (zero dependencies).
/// </summary>
internal static class NGqlTelemetry
{
    // OpenTelemetry-standard naming convention: {company}.{product}
    private const string ActivitySourceName = "NGql.Core";
    private const string MeterName = "NGql.Core";
    private const string Version = "1.0.0"; // Should match assembly version

    // Activity source for distributed tracing (OpenTelemetry-compatible)
    private static readonly ActivitySource ActivitySource = new(ActivitySourceName, Version);

    // Meter for metrics collection (OpenTelemetry-compatible)  
    private static readonly Meter Meter = new(MeterName, Version);

    #region Traces (Activities)

    /// <summary>
    /// Starts a new activity for query building operations
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Activity? StartQueryBuildingActivity(string operationName)
    {
        return ActivitySource.StartActivity($"ngql.query.{operationName}");
    }

    /// <summary>
    /// Starts a new activity for field operations
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Activity? StartFieldActivity(string operationName)
    {
        return ActivitySource.StartActivity($"ngql.field.{operationName}");
    }

    /// <summary>
    /// Starts a new activity for pooling operations
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Activity? StartPoolingActivity(string poolType, string operation)
    {
        return ActivitySource.StartActivity($"ngql.pool.{poolType}.{operation}");
    }

    /// <summary>
    /// Adds standard tags to an activity for query operations
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void TagQueryActivity(Activity? activity, string? queryName, int fieldCount)
    {
        if (activity == null) return;

        activity.SetTag("ngql.query.name", queryName);
        activity.SetTag("ngql.query.field_count", fieldCount);
        activity.SetTag("ngql.operation.type", "query_building");
    }

    /// <summary>
    /// Adds standard tags to an activity for field operations
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void TagFieldActivity(Activity? activity, string fieldPath, bool hasArguments, bool hasMetadata)
    {
        if (activity == null) return;

        activity.SetTag("ngql.field.path", fieldPath);
        activity.SetTag("ngql.field.has_arguments", hasArguments);
        activity.SetTag("ngql.field.has_metadata", hasMetadata);
        activity.SetTag("ngql.operation.type", "field_building");
    }

    /// <summary>
    /// Adds standard tags to an activity for pooling operations
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void TagPoolingActivity(Activity? activity, string poolType, string cacheHit, int poolSize)
    {
        if (activity == null) return;

        activity.SetTag("ngql.pool.type", poolType);
        activity.SetTag("ngql.pool.cache_hit", cacheHit);
        activity.SetTag("ngql.pool.size", poolSize);
        activity.SetTag("ngql.operation.type", "pooling");
    }

    #endregion

    #region Metrics

    // Counters (monotonic increasing values)
    private static readonly Counter<long> QueryBuiltCounter = Meter.CreateCounter<long>(
        "ngql_queries_built_total",
        description: "Total number of GraphQL queries built");

    private static readonly Counter<long> FieldsAddedCounter = Meter.CreateCounter<long>(
        "ngql_fields_added_total", 
        description: "Total number of fields added to queries");

    private static readonly Counter<long> PoolOperationsCounter = Meter.CreateCounter<long>(
        "ngql_pool_operations_total",
        description: "Total number of pooling operations");

    // Histograms (value distributions)
    private static readonly Histogram<double> QueryBuildDuration = Meter.CreateHistogram<double>(
        "ngql_query_build_duration_seconds",
        unit: "s",
        description: "Duration of query building operations");

    private static readonly Histogram<double> SerializationDuration = Meter.CreateHistogram<double>(
        "ngql_serialization_duration_seconds", 
        unit: "s",
        description: "Duration of query serialization to string");

    private static readonly Histogram<long> QueryFieldCount = Meter.CreateHistogram<long>(
        "ngql_query_field_count",
        description: "Number of fields in built queries");

#if NET7_0_OR_GREATER
    // UpDownCounters (can increase or decrease) - Available in .NET 7+
    private static readonly UpDownCounter<long> ActiveQueriesGauge = Meter.CreateUpDownCounter<long>(
        "ngql_active_queries",
        description: "Number of queries currently being built");

    private static readonly UpDownCounter<long> PoolSizeGauge = Meter.CreateUpDownCounter<long>(
        "ngql_pool_size",
        description: "Current size of object pools");
#else
    // For .NET 6, we'll use regular counters and manage state manually
    private static readonly Counter<long> ActiveQueriesChangeCounter = Meter.CreateCounter<long>(
        "ngql_active_queries_changes_total",
        description: "Total changes to active query count");

    private static readonly Counter<long> PoolSizeChangeCounter = Meter.CreateCounter<long>(
        "ngql_pool_size_changes_total",
        description: "Total changes to pool sizes");
        
    // Manual state tracking for .NET 6 compatibility
    private static long _currentActiveQueries = 0;
#endif

    #region Metric Recording Methods

    /// <summary>
    /// Records a query built event with contextual tags
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void RecordQueryBuilt(string? queryName, int fieldCount, double durationSeconds)
    {
        var tags = new TagList
        {
            { "query.name", queryName },
            { "operation", "build" }
        };

        QueryBuiltCounter.Add(1, tags);
        QueryBuildDuration.Record(durationSeconds, tags);
        QueryFieldCount.Record(fieldCount, tags);
    }

    /// <summary>
    /// Records a field added event
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void RecordFieldAdded(string fieldPath, bool hasArguments, bool hasMetadata)
    {
        var tags = new TagList
        {
            { "field.has_arguments", hasArguments },
            { "field.has_metadata", hasMetadata },
            { "field.is_nested", fieldPath.Contains('.') }
        };

        FieldsAddedCounter.Add(1, tags);
    }

    /// <summary>
    /// Records query serialization metrics
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void RecordSerialization(double durationSeconds, int outputLength)
    {
        var tags = new TagList
        {
            { "operation", "serialize" },
            { "output.size_category", GetSizeCategory(outputLength) }
        };

        SerializationDuration.Record(durationSeconds, tags);
    }

    /// <summary>
    /// Records pool operation metrics
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void RecordPoolOperation(string poolType, string operation, string cacheLevel, int poolSize)
    {
        var tags = new TagList
        {
            { "pool.type", poolType },
            { "pool.operation", operation },
            { "pool.cache_level", cacheLevel }
        };

        PoolOperationsCounter.Add(1, tags);
        
#if NET7_0_OR_GREATER
        PoolSizeGauge.Add(poolSize, new TagList { { "pool.type", poolType } });
#else
        // For .NET 6, record as counter change
        PoolSizeChangeCounter.Add(Math.Abs(poolSize), new TagList { { "pool.type", poolType } });
#endif
    }

    /// <summary>
    /// Records active query count changes
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void RecordActiveQueryChange(int delta)
    {
#if NET7_0_OR_GREATER
        ActiveQueriesGauge.Add(delta);
#else
        // For .NET 6, track manually and record as counter changes
        var newCount = Interlocked.Add(ref _currentActiveQueries, delta);
        ActiveQueriesChangeCounter.Add(Math.Abs(delta), new TagList { { "operation", "active_query_change" } });
        
        // Optionally record the current value in activities for observability
        using var activity = ActivitySource.StartActivity("ngql.metrics.active_queries");
        activity?.SetTag("current_count", newCount);
        activity?.SetTag("delta", delta);
#endif
    }

    #endregion

    #endregion

    #region Utility Methods

    /// <summary>
    /// Categorizes output size for better metric cardinality control
    /// </summary>
    private static string GetSizeCategory(int size)
    {
        return size switch
        {
            < 1024 => "small",      // < 1KB
            < 10240 => "medium",    // < 10KB  
            < 102400 => "large",    // < 100KB
            _ => "very_large"       // >= 100KB
        };
    }

    /// <summary>
    /// Creates a scoped measurement for timing operations
    /// </summary>
    internal static IDisposable CreateTimedScope(string operationName, TagList tags)
    {
        return new TimedScope(operationName, tags);
    }

    #endregion

    #region Timed Scope Helper

    /// <summary>
    /// RAII helper for automatic timing measurements
    /// </summary>
    private readonly struct TimedScope : IDisposable
    {
        private readonly string _operationName;
        private readonly TagList _tags;
        private readonly long _startTicks;

        public TimedScope(string operationName, TagList tags)
        {
            _operationName = operationName;
            _tags = tags;
            _startTicks = Stopwatch.GetTimestamp();
        }

        public void Dispose()
        {
            var elapsed = GetElapsedSeconds(_startTicks);
            
            // Record to appropriate histogram based on operation
            if (_operationName.Contains("serialize"))
            {
                SerializationDuration.Record(elapsed, _tags);
            }
            else
            {
                QueryBuildDuration.Record(elapsed, _tags);
            }


            // <summary>
            // Gets elapsed seconds since the given timestamp (cross-.NET version compatible)
            // </summary>
            static double GetElapsedSeconds(long startTimestamp)
            {
#if NET7_0_OR_GREATER
        return Stopwatch.GetElapsedTime(startTimestamp).TotalSeconds;
#else
                var elapsed = Stopwatch.GetTimestamp() - startTimestamp;
                return (double)elapsed / Stopwatch.Frequency;
#endif
            }
        }
    }

    #endregion

}
