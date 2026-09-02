using NGql.Core.Abstractions;
using NGql.Core.Extensions;

namespace NGql.Core.Features;

/// <summary>
/// Bucketed candidate index for <c>QueryMerger.FindMergeTarget</c>, owned by a single
/// <see cref="QueryDefinition"/>'s root field dictionary. Without this index, finding a merge
/// target scans every existing root field for every incoming field — O(N) per <c>Include()</c>,
/// O(N&#0178;) for a chain of N includes that all share one field <c>Name</c> (the production shape:
/// N fragments at the same field path with differing argument filters).
///
/// Bucketing is by <c>Name</c> (OrdinalIgnoreCase) only — mirrors the cheap check
/// <c>FindMergeTarget</c> already performed before falling through to the expensive
/// <see cref="FieldDefinitionExtensions.CanMergeFields"/> call. Argument fingerprints
/// (<see cref="Helpers.ComputeArgumentFingerprint"/>) are deliberately NOT cached in the bucket:
/// a root-level field's arguments can be mutated in place by code paths outside
/// <see cref="QueryMerger"/> (e.g. plain <c>AddField</c> merging new arguments into an existing
/// field) without changing the dictionary's <c>Count</c>, which a count-based staleness check
/// would miss — and a stale fingerprint here would risk the one truly forbidden outcome: a false
/// split that skips a genuine merge candidate. Fingerprints are instead recomputed from each
/// candidate's LIVE <see cref="FieldDefinition._arguments"/> at <see cref="GetMergeCandidates"/>
/// call time, which is still O(bucket size) — only the ones sharing the same <c>Name</c> — not
/// O(N) across the whole root dictionary.
///
/// The index also tracks every key currently in the root dictionary plus a per-base-name suffix
/// counter, so <see cref="KeyGenerator"/> can hand out the next unique key directly instead of
/// rebuilding a <see cref="HashSet{T}"/> from every key on every call.
///
/// The index is maintained incrementally by every root-dictionary insertion made through
/// <see cref="QueryMerger"/> (the only consumer), so a chain of <c>Include()</c> calls never pays
/// a rebuild. Any out-of-band mutation of the root dictionary's KEY SET (plain <c>AddField</c>
/// adding a brand-new root field, <c>Preserve</c> building a fresh definition, etc. — anything
/// that does not go through <see cref="QueryMerger"/>) is detected cheaply via a field-count
/// comparison and triggers one full resync — a stale index is the worst failure mode here (a
/// silent wrong merge), so staleness is actively detected rather than assumed away.
/// </summary>
internal sealed class FieldMergeIndex
{
    // Name (OrdinalIgnoreCase) -> ordered list of root-dictionary keys sharing that name, in the
    // order they were inserted. Preserving insertion order keeps FindMergeTarget's "first match
    // wins" behavior identical to a linear scan of the dictionary. Only the KEY is cached —
    // fingerprints are computed fresh from the live field at lookup time (see class remarks).
    private readonly Dictionary<string, List<string>> _byName = new(StringComparer.OrdinalIgnoreCase);

    // Every key currently in the root dictionary, plus a per-base-name suffix counter — backs
    // KeyGenerator.GenerateUniqueKey(FieldMergeIndex, ...) so it can produce the next unique key
    // directly instead of rebuilding a HashSet from fields.Keys on every call.
    private readonly HashSet<string> _allKeys = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, int> _suffixCounters = new(StringComparer.OrdinalIgnoreCase);

    // Snapshot of the root dictionary's Count as of the last time this index was known to be
    // fully consistent with it. Compared against the live Count on every read — an O(1) staleness
    // check that catches any insertion which bypassed IndexAdd (e.g. a plain AddField/Preserve
    // call mixed into the same builder as Include() calls).
    private int _syncedCount;

    /// <summary>
    /// Registers a newly inserted root-level field's KEY so future <see cref="GetMergeCandidates"/>
    /// and <see cref="NextUniqueKey"/> calls see it. Must be called at every site that adds a NEW
    /// key to the owning <see cref="QueryDefinition"/>'s root field dictionary. Never-merge fields
    /// are still indexed (harmless — <c>FindMergeTarget</c> itself skips <c>IsNeverMerge</c>
    /// candidates) so a later field of the same name still gets an accurate candidate list.
    /// </summary>
    public void IndexAdd(string key, FieldDefinition field)
    {
        GetOrCreateBucket(field.Name).Add(key);
        _allKeys.Add(key);
        _syncedCount++;
    }

    private List<string> GetOrCreateBucket(string name)
    {
        if (!_byName.TryGetValue(name, out var bucket))
        {
            bucket = new List<string>();
            _byName[name] = bucket;
        }
        return bucket;
    }

    /// <summary>
    /// Returns the same-name candidate key bucket (or null if empty) for <paramref name="name"/>,
    /// resyncing against <paramref name="fields"/> first if the key set is stale. The caller
    /// (<c>QueryMerger.FindMergeTarget</c>) resolves each key to its live field and computes its
    /// fingerprint there — this method only narrows by <c>Name</c>, never allocates a filtered
    /// copy, and never risks handing back a fingerprint that has drifted from the live field.
    /// </summary>
    public List<string>? GetMergeCandidates(Dictionary<string, FieldDefinition> fields, string name)
    {
        EnsureSynced(fields);
        return _byName.TryGetValue(name, out var bucket) && bucket.Count > 0 ? bucket : null;
    }

    /// <summary>
    /// Produces the next unique key for <paramref name="baseKey"/> in O(1) amortized time: a
    /// per-base-name counter is advanced past every suffix already handed out, instead of
    /// rebuilding a <see cref="HashSet{T}"/> from all existing keys on every call (the prior
    /// quadratic source — <c>AddFieldWithUniqueKey</c> was calling
    /// <c>KeyGenerator.GenerateUniqueKey(name, fields.Keys)</c>, an O(N) rebuild, for every one of
    /// N inserted fields).
    /// </summary>
    public string NextUniqueKey(Dictionary<string, FieldDefinition> fields, string baseKey)
    {
        EnsureSynced(fields);

        if (!_allKeys.Contains(baseKey))
        {
            return baseKey;
        }

        var counter = _suffixCounters.GetValueOrDefault(baseKey, 0);
        string candidate;
        do
        {
            counter++;
            candidate = $"{baseKey}_{counter}";
        } while (_allKeys.Contains(candidate));

        _suffixCounters[baseKey] = counter;
        return candidate;
    }

    private void EnsureSynced(Dictionary<string, FieldDefinition> fields)
    {
        if (fields.Count == _syncedCount) return;

        _byName.Clear();
        _allKeys.Clear();
        _suffixCounters.Clear();
        foreach (var (key, field) in fields)
        {
            GetOrCreateBucket(field.Name).Add(key);
            _allKeys.Add(key);
        }
        _syncedCount = fields.Count;
    }
}
