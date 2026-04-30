using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace NGql.Core.Observability;

/// <summary>
/// Helper class for creating scoped activities with automatic disposal and error handling.
/// Provides convenient RAII pattern for OpenTelemetry-compatible distributed tracing.
/// </summary>
internal readonly ref struct NGqlActivity
{
    private readonly Activity? _activity;

    private NGqlActivity(Activity? activity)
    {
        _activity = activity;
    }

    /// <summary>
    /// Creates a scoped activity for query building operations
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static NGqlActivity StartQuery(string operationName)
    {
        var activity = NGqlTelemetry.StartQueryBuildingActivity(operationName);
        return new NGqlActivity(activity);
    }

    /// <summary>
    /// Creates a scoped activity for field operations
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static NGqlActivity StartField(string operationName)
    {
        var activity = NGqlTelemetry.StartFieldActivity(operationName);
        return new NGqlActivity(activity);
    }

    /// <summary>
    /// Creates a scoped activity for pooling operations
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static NGqlActivity StartPooling(string poolType, string operation)
    {
        var activity = NGqlTelemetry.StartPoolingActivity(poolType, operation);
        return new NGqlActivity(activity);
    }

    /// <summary>
    /// Adds query-related tags to the activity
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly NGqlActivity WithQueryTags(string? queryName, int fieldCount)
    {
        NGqlTelemetry.TagQueryActivity(_activity, queryName, fieldCount);
        return this;
    }

    /// <summary>
    /// Adds field-related tags to the activity
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly NGqlActivity WithFieldTags(string fieldPath, bool hasArguments, bool hasMetadata)
    {
        NGqlTelemetry.TagFieldActivity(_activity, fieldPath, hasArguments, hasMetadata);
        return this;
    }

    /// <summary>
    /// Adds pooling-related tags to the activity
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly NGqlActivity WithPoolingTags(string poolType, string cacheHit, int poolSize)
    {
        NGqlTelemetry.TagPoolingActivity(_activity, poolType, cacheHit, poolSize);
        return this;
    }

    /// <summary>
    /// Adds a custom tag to the activity
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly NGqlActivity WithTag(string key, object? value)
    {
        _activity?.SetTag(key, value);
        return this;
    }

    /// <summary>
    /// Adds a custom status to the activity
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly NGqlActivity WithStatus(ActivityStatusCode statusCode, string? description = null)
    {
        _activity?.SetStatus(statusCode, description);
        return this;
    }

    /// <summary>
    /// Records an exception in the activity. Each call site uses the null-conditional operator
    /// so an inactive activity (no listener registered) is a single null-test branch that's
    /// always reachable in tests.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly NGqlActivity WithException(Exception exception)
    {
        _activity?.SetStatus(ActivityStatusCode.Error, exception.Message);
        _activity?.SetTag("exception.type", exception.GetType().Name);
        _activity?.SetTag("exception.message", exception.Message);
        _activity?.SetTag("exception.stacktrace", exception.StackTrace);
        return this;
    }

    /// <summary>
    /// Adds an event to the activity (equivalent to OpenTelemetry spans events)
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly NGqlActivity AddEvent(string name, DateTimeOffset? timestamp = null)
    {
        _activity?.AddEvent(new ActivityEvent(name, timestamp ?? DateTimeOffset.UtcNow));
        return this;
    }

    /// <summary>
    /// Adds an event with tags to the activity
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly NGqlActivity AddEvent(string name, ActivityTagsCollection tags, DateTimeOffset? timestamp = null)
    {
        _activity?.AddEvent(new ActivityEvent(name, timestamp ?? DateTimeOffset.UtcNow, tags));
        return this;
    }

    /// <summary>
    /// Automatically disposes the activity when leaving scope
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly void Dispose()
    {
        // Activity disposal is automatic when it goes out of scope
        // This explicit disposal ensures proper cleanup for our ref struct
        _activity?.Dispose();
    }

    /// <summary>
    /// Gets whether the activity is active and being recorded
    /// </summary>
    public readonly bool IsRecording => _activity?.IsAllDataRequested == true;

    /// <summary>
    /// Gets the activity ID for correlation (equivalent to OpenTelemetry span ID)
    /// </summary>
    public readonly string? Id => _activity?.Id;

    /// <summary>
    /// Gets the trace ID for distributed tracing correlation
    /// </summary>
    public readonly string? TraceId => _activity?.TraceId.ToString();
}

/// <summary>
/// Extension methods for adding NGql-specific observability to existing code
/// </summary>
internal static class ActivityExtensions
{
    /// <summary>
    /// Safely executes an action with automatic error handling and activity recording
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T WithObservability<T>(this NGqlActivity activity, Func<T> action, string operationName)
    {
        try
        {
            activity.AddEvent($"{operationName}.start");
            var result = action();
            activity.WithStatus(ActivityStatusCode.Ok);
            activity.AddEvent($"{operationName}.complete");
            return result;
        }
        catch (Exception ex)
        {
            activity.WithException(ex);
            activity.AddEvent($"{operationName}.error");
            throw;
        }
    }

    /// <summary>
    /// Safely executes an action with automatic error handling and activity recording
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WithObservability(this NGqlActivity activity, Action action, string operationName)
    {
        try
        {
            activity.AddEvent($"{operationName}.start");
            action();
            activity.WithStatus(ActivityStatusCode.Ok);
            activity.AddEvent($"{operationName}.complete");
        }
        catch (Exception ex)
        {
            activity.WithException(ex);
            activity.AddEvent($"{operationName}.error");
            throw;
        }
    }
}