using System.Collections.Concurrent;
using NGql.Core.Abstractions;

namespace NGql.Core.Pooling;

/// <summary>
/// Pool for SortedDictionary arguments to reduce allocations
/// </summary>
internal static class ArgumentsPool
{
    private static readonly ConcurrentQueue<SortedDictionary<string, object?>> _pool = new();
    private const int MaxPoolSize = 32;
    private static int _currentCount = 0;

    public static SortedDictionary<string, object?> Get()
    {
        if (_pool.TryDequeue(out var dict))
        {
            Interlocked.Decrement(ref _currentCount);
            return dict;
        }
        return new SortedDictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
    }

    public static void Return(SortedDictionary<string, object?> dict)
    {
        if (dict == null || _currentCount >= MaxPoolSize) return;
        dict.Clear();
        _pool.Enqueue(dict);
        Interlocked.Increment(ref _currentCount);
    }

    public static PooledArguments GetPooled(Dictionary<string, object?> source)
    {
        var dict = Get();
        foreach (var kvp in source)
            dict[kvp.Key] = kvp.Value;
        return new PooledArguments(dict);
    }
}

/// <summary>
/// Pool for SortedDictionary fields to reduce allocations
/// </summary>
internal static class FieldsPool
{
    private static readonly ConcurrentQueue<SortedDictionary<string, FieldDefinition>> _pool = new();
    private const int MaxPoolSize = 16;
    private static int _currentCount = 0;

    public static SortedDictionary<string, FieldDefinition> Get()
    {
        if (_pool.TryDequeue(out var dict))
        {
            Interlocked.Decrement(ref _currentCount);
            return dict;
        }
        return new SortedDictionary<string, FieldDefinition>(StringComparer.OrdinalIgnoreCase);
    }

    public static void Return(SortedDictionary<string, FieldDefinition> dict)
    {
        if (dict == null || _currentCount >= MaxPoolSize) return;
        dict.Clear();
        _pool.Enqueue(dict);
        Interlocked.Increment(ref _currentCount);
    }

    public static PooledFields GetPooled(SortedDictionary<string, FieldDefinition> source)
    {
        var dict = Get();
        foreach (var kvp in source)
            dict[kvp.Key] = kvp.Value;
        return new PooledFields(dict);
    }
}

/// <summary>
/// Pool for Dictionary metadata to reduce allocations
/// </summary>
internal static class MetadataPool
{
    private static readonly ConcurrentQueue<Dictionary<string, object?>> _pool = new();
    private const int MaxPoolSize = 16;
    private static int _currentCount = 0;

    public static Dictionary<string, object?> Get()
    {
        if (_pool.TryDequeue(out var dict))
        {
            Interlocked.Decrement(ref _currentCount);
            return dict;
        }
        return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
    }

    public static void Return(Dictionary<string, object?> dict)
    {
        if (dict == null || _currentCount >= MaxPoolSize) return;
        dict.Clear();
        _pool.Enqueue(dict);
        Interlocked.Increment(ref _currentCount);
    }
}

/// <summary>Zero-allocation ref struct wrappers</summary>
internal ref struct PooledArguments
{
    public readonly SortedDictionary<string, object?> Dictionary;
    public PooledArguments(SortedDictionary<string, object?> dictionary) => Dictionary = dictionary;
    public void Dispose() => ArgumentsPool.Return(Dictionary);
}

internal ref struct PooledFields
{
    public readonly SortedDictionary<string, FieldDefinition> Dictionary;
    public PooledFields(SortedDictionary<string, FieldDefinition> dictionary) => Dictionary = dictionary;
    public void Dispose() => FieldsPool.Return(Dictionary);
}
