using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Threading;

namespace NGql.Core.Abstractions;

/// <summary>
/// Array-backed ordered collection of child <see cref="FieldDefinition"/> instances.
/// Uses linear scan for small counts and a lazy dictionary index for larger counts.
/// Null <see cref="FieldDefinition._children"/> on a leaf node costs nothing — no allocation occurs
/// until the first child is added.
///
/// THREAD-SAFETY: Reads on small (no-index) collections are lock-free — readers do volatile loads
/// of <see cref="_count"/> and <see cref="_items"/> and walk a contiguous array. Reads against the
/// index, and all writes, take <see cref="_lock"/>. Writers publish updates with explicit volatile
/// stores: the new slot is written before <see cref="_count"/> bumps, so any reader observing the
/// new count is also guaranteed to see the corresponding initialized slot.
///
/// The index threshold is intentionally moderate: tiny collections do not need an index, and avoiding
/// it on the read fast-path eliminates lock acquisition for the common case (most query nodes have
/// fewer than a dozen direct children).
/// </summary>
internal sealed class FieldChildren : IReadOnlyDictionary<string, FieldDefinition>
{
    private const int InitialCapacity = 4;
    private const int IndexThreshold = 16;

    internal FieldChildren()
    {
    }

    /// <summary>
    /// Creates the collection pre-sized for a known child count, avoiding the grow-and-copy
    /// cycles of the default 4-slot start. Used by clone paths where the count is known upfront.
    /// </summary>
    internal FieldChildren(int capacity)
        => _items = new FieldDefinition[Math.Max(capacity, InitialCapacity)];

    /// <summary>Backing storage. Written only under <see cref="_lock"/>; readers do a single volatile load.</summary>
    private FieldDefinition[]? _items;
    /// <summary>Number of valid entries in <see cref="_items"/>. Volatile-stored last on writes
    /// (release semantics) so readers observing the new value also see the corresponding slot.</summary>
    private int _count;
    /// <summary>Lazy lookup index, built when <see cref="_count"/> reaches <see cref="IndexThreshold"/>.
    /// All access (including reads) is guarded by <see cref="_lock"/> because <see cref="Dictionary{TKey,TValue}"/>
    /// is not safe for concurrent read+write.</summary>
    private Dictionary<string, FieldDefinition>? _index;
    private readonly object _lock = new();

    // ── Counts ────────────────────────────────────────────────────────────────

    int IReadOnlyCollection<KeyValuePair<string, FieldDefinition>>.Count => Volatile.Read(ref _count);
    internal int Count => Volatile.Read(ref _count);

    // ── Span access ───────────────────────────────────────────────────────────

    /// <summary>Returns a span over the items for zero-alloc iteration.
    /// Reads count first, then items — combined with the writer's items-then-count publication this
    /// guarantees the observed array contains every slot below the observed count.</summary>
    internal ReadOnlySpan<FieldDefinition> AsSpan()
    {
        var count = Volatile.Read(ref _count);
        var items = Volatile.Read(ref _items);
        return items == null ? ReadOnlySpan<FieldDefinition>.Empty : items.AsSpan(0, count);
    }

    // ── Lookup ────────────────────────────────────────────────────────────────

    /// <summary>Find a child by name (case-insensitive). Returns null if not found.</summary>
    internal FieldDefinition? Find(ReadOnlySpan<char> name)
    {
        // Fast path: no index yet. Lock-free volatile snapshot + linear scan.
        if (Volatile.Read(ref _index) == null)
        {
            var count = Volatile.Read(ref _count);
            var items = Volatile.Read(ref _items);
            if (items == null) return null;
            for (int i = 0; i < count; i++)
            {
                if (name.Equals(items[i].Name.AsSpan(), StringComparison.OrdinalIgnoreCase))
                    return items[i];
            }
            return null;
        }

        // Slow path: indexed lookup needs the lock since Dictionary is not concurrent-read-safe.
        // _index is monotone: once published non-null by BuildIndexLocked it never reverts, so
        // re-checking under the lock would be a dead branch.
        lock (_lock)
        {
            return _index!.TryGetValue(name.ToString(), out var indexed) ? indexed : null;
        }
    }

    /// <summary>Find a child by name (case-insensitive). String overload avoids span/string round-tripping.</summary>
    internal FieldDefinition? Find(string name)
    {
        if (Volatile.Read(ref _index) == null)
        {
            var count = Volatile.Read(ref _count);
            var items = Volatile.Read(ref _items);
            if (items == null) return null;
            for (int i = 0; i < count; i++)
            {
                if (string.Equals(items[i].Name, name, StringComparison.OrdinalIgnoreCase))
                    return items[i];
            }
            return null;
        }

        lock (_lock)
        {
            return _index!.TryGetValue(name, out var indexed) ? indexed : null;
        }
    }

    internal bool TryGetValue(ReadOnlySpan<char> name, [MaybeNullWhen(false)] out FieldDefinition value)
    {
        value = Find(name)!;
        return value != null;
    }

    internal bool TryGetValue(string name, [MaybeNullWhen(false)] out FieldDefinition value)
    {
        value = Find(name)!;
        return value != null;
    }

    bool IReadOnlyDictionary<string, FieldDefinition>.TryGetValue(string key, [MaybeNullWhen(false)] out FieldDefinition value)
        => TryGetValue(key, out value);

    bool IReadOnlyDictionary<string, FieldDefinition>.ContainsKey(string key)
        => Find(key) != null;

    FieldDefinition IReadOnlyDictionary<string, FieldDefinition>.this[string key]
    {
        get
        {
            var found = Find(key);
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
        lock (_lock)
        {
            AppendLocked(child);
        }
    }

    /// <summary>Adds or replaces a child by name (case-insensitive).</summary>
    internal void Set(string name, FieldDefinition child)
    {
        lock (_lock)
        {
            var items = _items;
            if (items != null)
            {
                for (int i = 0; i < _count; i++)
                {
                    if (string.Equals(items[i].Name, name, StringComparison.OrdinalIgnoreCase))
                    {
                        items[i] = child;
                        if (_index != null) _index[child.Name] = child;
                        return;
                    }
                }
            }
            AppendLocked(child);
        }
    }

    /// <summary>
    /// Replaces an existing child by span name (case-insensitive). The only call site —
    /// <c>FieldFactory.ProcessDottedSegment</c> — invokes this strictly after a successful
    /// <see cref="TryGetValue(ReadOnlySpan{char},out FieldDefinition)"/>, so the entry is
    /// guaranteed to exist; the loop is simply walking to the index of the known match.
    /// </summary>
    internal void Set(ReadOnlySpan<char> name, FieldDefinition child)
    {
        lock (_lock)
        {
            var items = _items!;
            int i = 0;
            while (!name.Equals(items[i].Name.AsSpan(), StringComparison.OrdinalIgnoreCase)) i++;
            items[i] = child;
            if (_index != null) _index[child.Name] = child;
        }
    }

    /// <summary>
    /// Supports collection initializer syntax: <c>new FieldChildren { new FieldDefinition("x") }</c>.
    /// Delegates to <see cref="Append"/>.
    /// </summary>
    internal void Add(FieldDefinition child) => Append(child);

    // ── Private helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Append logic used within locked sections. Publishes the new slot before bumping
    /// <see cref="_count"/> so concurrent readers always see fully-initialized data.
    /// </summary>
    private void AppendLocked(FieldDefinition child)
    {
        var items = _items;
        if (items == null)
        {
            items = new FieldDefinition[InitialCapacity];
            Volatile.Write(ref _items, items);
        }
        else if (_count == items.Length)
        {
            // Grow: allocate a new array, copy, then publish. Concurrent readers either see the old
            // array (with their bounded count) or the new array — both are coherent snapshots.
            var grown = new FieldDefinition[items.Length * 2];
            Array.Copy(items, grown, _count);
            items = grown;
            Volatile.Write(ref _items, items);
        }

        items[_count] = child;
        // Release-store the new count last so any reader that observes it also sees the slot above.
        Volatile.Write(ref _count, _count + 1);

        if (_index != null)
        {
            _index[child.Name] = child;
        }
        else if (_count >= IndexThreshold)
        {
            BuildIndexLocked();
        }
    }

    private void BuildIndexLocked()
    {
        var built = new Dictionary<string, FieldDefinition>(_count, StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < _count; i++)
            built[_items![i].Name] = _items[i];
        // Publish the index pointer last so readers that observe non-null _index see a fully populated dict.
        Volatile.Write(ref _index, built);
    }

    // ── IReadOnlyDictionary (Keys / Values / Enumerator) ─────────────────────

    IEnumerable<string> IReadOnlyDictionary<string, FieldDefinition>.Keys
    {
        get
        {
            var count = Volatile.Read(ref _count);
            var items = Volatile.Read(ref _items);
            if (items == null) return [];
            var keys = new List<string>(count);
            for (int i = 0; i < count; i++)
                keys.Add(items[i].Name);
            return keys;
        }
    }

    IEnumerable<FieldDefinition> IReadOnlyDictionary<string, FieldDefinition>.Values
    {
        get
        {
            var count = Volatile.Read(ref _count);
            var items = Volatile.Read(ref _items);
            if (items == null) return [];
            var values = new List<FieldDefinition>(count);
            for (int i = 0; i < count; i++)
                values.Add(items[i]);
            return values;
        }
    }

    /// <summary>
    /// Returns a zero-alloc struct enumerator over a snapshot of this collection.
    /// </summary>
    public FieldChildrenEnumerator GetEnumerator()
    {
        var count = Volatile.Read(ref _count);
        var items = Volatile.Read(ref _items);
        return new FieldChildrenEnumerator(items, count);
    }

    // Explicit interface implementation — provides fallback for code that uses IEnumerable<T> directly
    IEnumerator<KeyValuePair<string, FieldDefinition>> IEnumerable<KeyValuePair<string, FieldDefinition>>.GetEnumerator()
        => GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator()
        => GetEnumerator();
}

/// <summary>
/// Zero-allocation struct enumerator for <see cref="FieldChildren"/>.
/// Operates on a snapshot taken at construction time, so concurrent appends do not
/// affect an in-flight enumeration.
/// </summary>
internal struct FieldChildrenEnumerator : IEnumerator<KeyValuePair<string, FieldDefinition>>
{
    private readonly FieldDefinition[]? _snapshot;
    private readonly int _count;
    private int _index;

    internal FieldChildrenEnumerator(FieldDefinition[]? snapshot, int count)
    {
        _snapshot = snapshot;
        _count = count;
        _index = -1;
    }

    public KeyValuePair<string, FieldDefinition> Current
    {
        get
        {
            if (_snapshot == null || _index < 0 || _index >= _count)
                throw new InvalidOperationException("Enumeration has not started or has ended.");
            var item = _snapshot[_index];
            return new KeyValuePair<string, FieldDefinition>(item.Name, item);
        }
    }

    object System.Collections.IEnumerator.Current => Current;

    public bool MoveNext()
    {
        if (_snapshot == null) return false;
        _index++;
        return _index < _count;
    }

    public void Reset() => _index = -1;

    public void Dispose() { }
}
