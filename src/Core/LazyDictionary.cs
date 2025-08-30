using System.Runtime.CompilerServices;

namespace NGql.Core;

/// <summary>
/// Lazy wrapper for dictionaries to avoid creating empty collections that are never used
/// </summary>
internal readonly struct LazyDictionary<TKey, TValue> where TKey : notnull
{
    private readonly SortedDictionary<TKey, TValue>? _dictionary;
    private readonly Func<SortedDictionary<TKey, TValue>>? _factory;

    public LazyDictionary(SortedDictionary<TKey, TValue>? dictionary)
    {
        _dictionary = dictionary;
        _factory = null;
    }

    public LazyDictionary(Func<SortedDictionary<TKey, TValue>> factory)
    {
        _dictionary = null;
        _factory = factory;
    }

    public bool HasValue => _dictionary != null || _factory != null;

    public bool IsEmpty => !HasValue || (_dictionary?.Count == 0);

    public int Count => _dictionary?.Count ?? 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public SortedDictionary<TKey, TValue> GetOrCreate()
    {
        if (_dictionary != null) return _dictionary;
        if (_factory != null) return _factory();
        return new SortedDictionary<TKey, TValue>();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public SortedDictionary<TKey, TValue>? GetIfExists() => _dictionary;

    public static implicit operator LazyDictionary<TKey, TValue>(SortedDictionary<TKey, TValue>? dictionary)
        => new(dictionary);

    public static readonly LazyDictionary<TKey, TValue> Empty = new((SortedDictionary<TKey, TValue>?)null);
}

/// <summary>
/// Lazy wrapper for regular dictionaries
/// </summary>
internal readonly struct LazyMetadata
{
    private readonly Dictionary<string, object?>? _dictionary;

    public LazyMetadata(Dictionary<string, object?>? dictionary)
    {
        _dictionary = dictionary;
    }

    public bool HasValue => _dictionary != null;

    public bool IsEmpty => !HasValue || _dictionary!.Count == 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Dictionary<string, object?>? GetIfExists() => _dictionary;

    public static implicit operator LazyMetadata(Dictionary<string, object?>? dictionary)
        => new(dictionary);

    public static readonly LazyMetadata Empty = new((Dictionary<string, object?>?)null);
}
