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
    /// Maps name to its SLOT INDEX in <see cref="_items"/> (not the <see cref="FieldDefinition"/> itself),
    /// so a hit gives both the current value (via <c>_items[slot]</c>) and the position needed for an
    /// in-place replace. All access (including reads) is guarded by <see cref="_lock"/> because
    /// <see cref="Dictionary{TKey,TValue}"/> is not safe for concurrent read+write.
    /// <para>
    /// <b>"Last occurrence wins" is the intended invariant for <see cref="AppendLocked"/> and
    /// <see cref="BuildIndexLocked"/>, but <see cref="ReplaceReference"/>'s linear-scan fallback
    /// (reached only for the rare same-name/different-alias collision the index cannot fully
    /// disambiguate) can re-point a name's slot at an EARLIER same-named sibling's position via
    /// <see cref="UpdateIndexKeyLocked"/>, technically breaking that invariant for the remainder of
    /// that name's entries.</b> No observable misbehavior is currently known: every caller that
    /// legitimately needs to distinguish same-named/different-alias siblings
    /// (<c>NGql.Core.Extensions.Helpers.FindExistingField(FieldChildren, FieldDefinition)</c>,
    /// <c>NGql.Core.Extensions.FieldDefinitionExtensions.FindChildByNameAndAlias</c>) already falls back to an exhaustive
    /// scan rather than trusting a bare name-index hit as authoritative. Documented here rather than
    /// changed because a fix would need to touch the same re-bucketing/index machinery under active,
    /// separate revision — flag before altering.
    /// </para></summary>
    private Dictionary<string, int>? _index;
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
            return _index!.TryGetValue(name.ToString(), out var slot) ? _items![slot] : null;
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
            return _index!.TryGetValue(name, out var slot) ? _items![slot] : null;
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
                if (_index != null)
                {
                    if (_index.TryGetValue(name, out var slot))
                    {
                        items[slot] = child;
                        UpdateIndexKeyLocked(name, child.Name, slot);
                        return;
                    }
                }
                else
                {
                    for (int i = 0; i < _count; i++)
                    {
                        if (string.Equals(items[i].Name, name, StringComparison.OrdinalIgnoreCase))
                        {
                            items[i] = child;
                            return;
                        }
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
    /// guaranteed to exist; below the index threshold the loop simply walks to the position of
    /// the known match, and once indexed the index resolves that position directly.
    /// </summary>
    internal void Set(ReadOnlySpan<char> name, FieldDefinition child)
    {
        lock (_lock)
        {
            var items = _items!;
            if (_index != null)
            {
                var slot = _index[name.ToString()];
                items[slot] = child;
                UpdateIndexKeyLocked(name, child.Name, slot);
                return;
            }

            int i = 0;
            while (!name.Equals(items[i].Name.AsSpan(), StringComparison.OrdinalIgnoreCase)) i++;
            items[i] = child;
        }
    }

    /// <summary>
    /// After an in-place replace, keeps <see cref="_index"/> consistent when the replacement's
    /// name differs from the looked-up key (e.g. alias/name changes on the field object) — removes
    /// the stale key and (re)inserts the new one pointing at the same slot. Must be called under
    /// <see cref="_lock"/> with <see cref="_index"/> known non-null.
    /// </summary>
    private void UpdateIndexKeyLocked(ReadOnlySpan<char> oldKey, string newKey, int slot)
    {
        if (!oldKey.Equals(newKey.AsSpan(), StringComparison.OrdinalIgnoreCase))
        {
            _index!.Remove(oldKey.ToString());
        }
        _index![newKey] = slot;
    }

    /// <summary>
    /// Replaces <paramref name="oldChild"/> with <paramref name="newChild"/> in the exact slot
    /// <paramref name="oldChild"/> occupies, identified by REFERENCE — never by name. Name-keyed
    /// lookup/replace cannot disambiguate two children that legitimately share a
    /// <see cref="FieldDefinition.Name"/> but differ by alias (distinct GraphQL response keys); a
    /// caller that already holds the exact matched instance (e.g. after an alias-aware scan) uses
    /// this to guarantee the replace lands on that instance's own slot, preserving both its
    /// position (render order) and every OTHER same-named sibling untouched.
    /// </summary>
    /// <remarks>
    /// Common case is O(1): <see cref="_index"/> is keyed by <see cref="FieldDefinition.Name"/>, and
    /// its remembered slot for <paramref name="oldChild"/>'s name almost always already holds
    /// <paramref name="oldChild"/> itself, since a caller only reaches here after a same-name hit.
    /// Only when that slot no longer holds <paramref name="oldChild"/> (a genuine same-name/
    /// different-alias collision, where the index can remember only one of the colliding slots) does
    /// this fall back to a linear scan by reference — exactly the same rare case that already forces
    /// a linear scan in <see cref="NGql.Core.Extensions.Helpers.FindExistingField(FieldChildren, FieldDefinition)"/>'s
    /// slow path.
    /// </remarks>
    internal void ReplaceReference(FieldDefinition oldChild, FieldDefinition newChild)
    {
        lock (_lock)
        {
            var items = _items!;

            if (_index != null && _index.TryGetValue(oldChild.Name, out var hintSlot)
                && ReferenceEquals(items[hintSlot], oldChild))
            {
                items[hintSlot] = newChild;
                UpdateIndexKeyLocked(oldChild.Name.AsSpan(), newChild.Name, hintSlot);
                return;
            }

            for (int i = 0; i < _count; i++)
            {
                if (ReferenceEquals(items[i], oldChild))
                {
                    items[i] = newChild;
                    if (_index != null)
                    {
                        UpdateIndexKeyLocked(oldChild.Name.AsSpan(), newChild.Name, i);
                    }
                    return;
                }
            }
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

        var newSlot = _count;
        items[newSlot] = child;
        // Release-store the new count last so any reader that observes it also sees the slot above.
        Volatile.Write(ref _count, newSlot + 1);

        if (_index != null)
        {
            _index[child.Name] = newSlot;
        }
        else if (_count >= IndexThreshold)
        {
            BuildIndexLocked();
        }
    }

    private void BuildIndexLocked()
    {
        var built = new Dictionary<string, int>(_count, StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < _count; i++)
            built[_items![i].Name] = i;
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
