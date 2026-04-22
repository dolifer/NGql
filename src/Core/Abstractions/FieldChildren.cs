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
    /// </summary>
    internal FieldChildren Clone()
    {
        if (_items == null) return new FieldChildren();

        var clone = new FieldChildren
        {
            _items = _items[.._count],
            _count = _count,
        };

        if (_index != null)
            clone._index = new Dictionary<string, FieldDefinition>(_index, StringComparer.OrdinalIgnoreCase);

        return clone;
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

    IEnumerator<KeyValuePair<string, FieldDefinition>> IEnumerable<KeyValuePair<string, FieldDefinition>>.GetEnumerator()
    {
        if (_items == null) yield break;
        for (int i = 0; i < _count; i++)
            yield return new KeyValuePair<string, FieldDefinition>(_items[i].Name, _items[i]);
    }

    IEnumerator IEnumerable.GetEnumerator()
        => ((IEnumerable<KeyValuePair<string, FieldDefinition>>)this).GetEnumerator();

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void BuildIndex()
    {
        _index = new Dictionary<string, FieldDefinition>(_count, StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < _count; i++)
            _index[_items![i].Name] = _items[i];
    }
}
