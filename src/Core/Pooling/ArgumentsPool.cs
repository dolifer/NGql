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

    public static PooledArguments GetPooled(SortedDictionary<string, object?>? source)
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
/// Pool for HashSet<string> to reduce allocations in key generation
/// </summary>
internal static class HashSetPool
{
    private static readonly ConcurrentQueue<HashSet<string>> _pool = new();
    private const int MaxPoolSize = 16;
    private static int _currentCount = 0;

    public static HashSet<string> Get()
    {
        if (_pool.TryDequeue(out var set))
        {
            Interlocked.Decrement(ref _currentCount);
            return set;
        }
        return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    }

    public static void Return(HashSet<string> set)
    {
        if (set == null || _currentCount >= MaxPoolSize) return;
        set.Clear();
        _pool.Enqueue(set);
        Interlocked.Increment(ref _currentCount);
    }

    public static PooledHashSet GetPooled(IEnumerable<string> source)
    {
        var set = Get();
        foreach (var item in source)
            set.Add(item);
        return new PooledHashSet(set);
    }
}

/// <summary>Zero-allocation ref struct wrappers</summary>
internal ref struct PooledArguments
{
    public readonly SortedDictionary<string, object?> Dictionary;
    public PooledArguments(SortedDictionary<string, object?> dictionary) => Dictionary = dictionary;
    public void Dispose() => ArgumentsPool.Return(Dictionary);
}

internal ref struct PooledHashSet
{
    public readonly HashSet<string> Set;
    public PooledHashSet(HashSet<string> set) => Set = set;
    public void Dispose() => HashSetPool.Return(Set);
}
