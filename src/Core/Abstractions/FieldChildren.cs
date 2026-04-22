using System.Collections;
using System.Diagnostics.CodeAnalysis;

namespace NGql.Core.Abstractions;

/// <summary>
/// Array-backed ordered collection of child <see cref="FieldDefinition"/> instances.
/// Uses linear scan for small counts and a lazy dictionary index for larger counts.
/// Null <see cref="FieldDefinition._children"/> on a leaf node costs nothing — no allocation occurs
/// until the first child is added.
/// </summary>
internal sealed class FieldChildren : IReadOnlyDictionary<string, FieldDefinition>
{
    private const int InitialCapacity = 4;
    private const int IndexThreshold = 16;

    internal FieldDefinition[]? _items;
    internal int _count;
    private Dictionary<string, FieldDefinition>? _index; // lazy, built when _count >= IndexThreshold

    // ── Counts ────────────────────────────────────────────────────────────────

    int IReadOnlyCollection<KeyValuePair<string, FieldDefinition>>.Count => _count;
    internal int Count => _count;

    // ── Span access ───────────────────────────────────────────────────────────

    /// <summary>Returns a span over the items for zero-alloc iteration.</summary>
    internal ReadOnlySpan<FieldDefinition> AsSpan()
        => _items == null ? ReadOnlySpan<FieldDefinition>.Empty : _items.AsSpan(0, _count);

    // ── Lookup ────────────────────────────────────────────────────────────────

    /// <summary>Find a child by name (case-insensitive). Returns null if not found.</summary>
    internal FieldDefinition? Find(ReadOnlySpan<char> name)
    {
        if (_items == null) return null;

        if (_index != null)
            return _index.TryGetValue(name.ToString(), out var indexed) ? indexed : null;

        // Linear scan for small collections — avoids dictionary overhead.
        for (int i = 0; i < _count; i++)
        {
            if (name.Equals(_items[i].Name.AsSpan(), StringComparison.OrdinalIgnoreCase))
                return _items[i];
        }
        return null;
    }

    internal bool TryGetValue(ReadOnlySpan<char> name, [MaybeNullWhen(false)] out FieldDefinition value)
    {
        value = Find(name)!;
        return value != null;
    }

    bool IReadOnlyDictionary<string, FieldDefinition>.TryGetValue(string key, [MaybeNullWhen(false)] out FieldDefinition value)
        => TryGetValue(key.AsSpan(), out value);

    bool IReadOnlyDictionary<string, FieldDefinition>.ContainsKey(string key)
        => Find(key.AsSpan()) != null;

    FieldDefinition IReadOnlyDictionary<string, FieldDefinition>.this[string key]
    {
        get
        {
            var found = Find(key.AsSpan());
            return found ?? throw new KeyNotFoundException($"Key '{key}' not found.");
        }
    }

    // ── Mutation ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Appends a new child. The caller must ensure the name does not already exist
    /// (use <see cref="Set(string,FieldDefinition)"/> when an update may be needed).
    /// </summary>
    internal void Append(FieldDefinition child)
    {
        if (_items == null)
            _items = new FieldDefinition[InitialCapacity];
        else if (_count == _items.Length)
            Array.Resize(ref _items, _items.Length * 2);

        _items[_count++] = child;

        if (_index != null)
            _index[child.Name] = child;
        else if (_count >= IndexThreshold)
            BuildIndex();
    }

    /// <summary>Adds or replaces a child by name (case-insensitive).</summary>
    internal void Set(string name, FieldDefinition child)
    {
        if (_items != null)
        {
            for (int i = 0; i < _count; i++)
            {
                if (string.Equals(_items[i].Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    _items[i] = child;
                    if (_index != null) _index[child.Name] = child;
                    return;
                }
            }
        }
        Append(child);
    }

    /// <summary>Adds or replaces a child by span name (case-insensitive).</summary>
    internal void Set(ReadOnlySpan<char> name, FieldDefinition child)
    {
        if (_items != null)
        {
            for (int i = 0; i < _count; i++)
            {
                if (name.Equals(_items[i].Name.AsSpan(), StringComparison.OrdinalIgnoreCase))
                {
                    _items[i] = child;
                    if (_index != null) _index[child.Name] = child;
                    return;
                }
            }
        }
        Append(child);
    }

    /// <summary>
    /// Supports collection initializer syntax: <c>new FieldChildren { new FieldDefinition("x") }</c>.
    /// Delegates to <see cref="Append"/>.
    /// </summary>
    internal void Add(FieldDefinition child) => Append(child);

    // ── Clone ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a shallow clone (array copy) for use during merge operations.
    /// The clone shares the same <see cref="FieldDefinition"/> references as the original.
    /// Note: Index is NOT copied; it rebuilds lazily on next insertion if needed.
    /// </summary>
    internal FieldChildren Clone()
    {
        if (_items == null) return new FieldChildren();

        return new FieldChildren
        {
            _items = _items[.._count],
            _count = _count,
            _index = null  // Skip index copy — will rebuild lazily if needed
        };
    }

    // ── IReadOnlyDictionary (Keys / Values / Enumerator) ─────────────────────

    IEnumerable<string> IReadOnlyDictionary<string, FieldDefinition>.Keys
    {
        get
        {
            if (_items == null) yield break;
            for (int i = 0; i < _count; i++) yield return _items[i].Name;
        }
    }

    IEnumerable<FieldDefinition> IReadOnlyDictionary<string, FieldDefinition>.Values
    {
        get
        {
            if (_items == null) yield break;
            for (int i = 0; i < _count; i++) yield return _items[i];
        }
    }

    /// <summary>
    /// Returns a zero-alloc struct enumerator for this collection.
    /// C# foreach will prefer this over the interface implementation, avoiding allocations.
    /// </summary>
    public FieldChildrenEnumerator GetEnumerator() => new FieldChildrenEnumerator(this);

    // Explicit interface implementation — provides fallback for code that uses IEnumerable<T> directly
    IEnumerator<KeyValuePair<string, FieldDefinition>> IEnumerable<KeyValuePair<string, FieldDefinition>>.GetEnumerator()
        => GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator()
        => GetEnumerator();

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void BuildIndex()
    {
        _index = new Dictionary<string, FieldDefinition>(_count, StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < _count; i++)
            _index[_items![i].Name] = _items[i];
    }
}

/// <summary>
/// Zero-allocation struct enumerator for <see cref="FieldChildren"/>.
/// Implements IEnumerator to support foreach without heap allocation.
/// </summary>
internal struct FieldChildrenEnumerator : IEnumerator<KeyValuePair<string, FieldDefinition>>
{
    private readonly FieldChildren? _children;
    private int _index;

    internal FieldChildrenEnumerator(FieldChildren children)
    {
        _children = children;
        _index = -1;
    }

    public KeyValuePair<string, FieldDefinition> Current
    {
        get
        {
            if (_children?._items == null || _index < 0 || _index >= _children._count)
                throw new InvalidOperationException("Enumeration has not started or has ended.");
            var item = _children._items[_index];
            return new KeyValuePair<string, FieldDefinition>(item.Name, item);
        }
    }

    object System.Collections.IEnumerator.Current => Current;

    public bool MoveNext()
    {
        if (_children == null) return false;
        _index++;
        return _index < _children._count;
    }

    public void Reset() => _index = -1;

    public void Dispose() { }
}
