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
/// <para>
/// Bucketing has TWO tiers. The outer tier is by <c>Name</c> (OrdinalIgnoreCase) — mirrors the
/// cheap check <c>FindMergeTarget</c> already performed before falling through to the expensive
/// <see cref="FieldDefinitionExtensions.CanMergeFields"/> call. The inner tier further narrows by
/// <see cref="FieldDefinitionExtensions.ComputeDeepFingerprint"/>, so a chain of N same-named
/// incoming fields that are all mutually incompatible (the production shape — every fragment
/// shares one root field name but carries a different deep argument filter) lands in N distinct,
/// small fingerprint buckets instead of one O(N) name bucket. <see cref="GetMergeCandidatesByFingerprint"/>
/// is the O(1)-average entry point <c>QueryMerger.FindMergeTarget</c> uses to exploit this.
/// </para>
///
/// </summary>
internal sealed class FieldMergeIndex
{
    // Name (OrdinalIgnoreCase) -> ordered list of root-dictionary keys sharing that name, in the
    // order they were inserted. Preserving insertion order keeps FindMergeTarget's "first match
    // wins" behavior identical to a linear scan of the dictionary. This remains the ground truth
    // for resync and for NextUniqueKey; GetMergeCandidatesByFingerprint below is a narrower, faster
    // view derived from the same underlying keys.
    private readonly Dictionary<string, List<string>> _byName = new(StringComparer.OrdinalIgnoreCase);

    // Name (OrdinalIgnoreCase) -> deep fingerprint -> ordered list of keys currently believed to
    // share that (Name, fingerprint) pair. Built lazily per name (see EnsureFingerprintBucketsBuilt)
    // the first time GetMergeCandidatesByFingerprint is asked for that name. Entries can become
    // stale (see class remarks); staleness is detected and healed per-entry at read time, never
    // assumed away.
    private readonly Dictionary<string, Dictionary<ulong, List<string>>> _byNameFingerprint = new(StringComparer.OrdinalIgnoreCase);

    // Names whose fingerprint sub-buckets have been built at least once, so a second call for the
    // same name during the same synced generation reuses them instead of rebuilding.
    private readonly HashSet<string> _fingerprintBucketsBuilt = new(StringComparer.OrdinalIgnoreCase);

    // Name (OrdinalIgnoreCase) -> key -> the fingerprint that key is CURRENTLY bucketed under in
    // _byNameFingerprint[name]. Reverse index of the same data _byNameFingerprint holds forward,
    // maintained in lock-step everywhere a key's bucket membership changes (build, IndexAdd,
    // HealBucketInPlace, ReindexFingerprint) so ReindexFingerprint's re-key can look up "which
    // bucket currently holds this key" in O(1) instead of scanning every fingerprint bucket under
    // the name — the fix for the production shape where a name accumulates many singleton buckets
    // (one per distinct incoming filter) and re-keying after every successful merge would otherwise
    // cost O(bucket count) each time, i.e. O(N) per merge / O(N^2) over a chain of N Includes.
    private readonly Dictionary<string, Dictionary<string, ulong>> _keyFingerprint = new(StringComparer.OrdinalIgnoreCase);

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

    private long _syncedMergeMemoEpoch = -1;
    private readonly MergeMemoScope _memoScope = new();

    /// <summary>
    /// Registers a newly inserted root-level field's KEY so future
    /// <see cref="GetMergeCandidatesByFingerprint"/> and <see cref="NextUniqueKey"/> calls see it.
    /// Must be called at every site that adds a NEW key to the owning <see cref="QueryDefinition"/>'s
    /// root field dictionary. Never-merge fields are still indexed (harmless — <c>FindMergeTarget</c>
    /// itself skips <c>IsNeverMerge</c> candidates) so a later field of the same name still gets an
    /// accurate candidate list.
    /// </summary>
    public void IndexAdd(string key, FieldDefinition field)
    {
        GetOrCreateBucket(field.Name).Add(key);
        _allKeys.Add(key);
        _syncedCount++;

        // Only append to a name's fingerprint sub-buckets if they have already been built for this
        // name (i.e. GetMergeCandidatesByFingerprint has been called for it before) — otherwise
        // leave it to the lazy build, which will pick this key up from _byName along with every
        // other key for that name the first time it is needed. _fingerprintBucketsBuilt and
        // _byNameFingerprint entries are always created together (see EnsureFingerprintBucketsBuilt),
        // so the outer dictionary lookup below is guaranteed to hit.
        if (_fingerprintBucketsBuilt.Contains(field.Name))
        {
            field.EnsureMergeMemoTracker().Attach(_memoScope);
            var fingerprint = FieldDefinitionExtensions.ComputeDeepFingerprint(field);
            GetOrCreateFingerprintBucket(_byNameFingerprint[field.Name], fingerprint).Add(key);
            SetKeyFingerprint(field.Name, key, fingerprint);
        }
    }

    // Records `key`'s current bucket fingerprint under `name` in the reverse index, creating the
    // per-name map on first use. Every write to a _byNameFingerprint bucket has a matching call
    // here so the two stay in lock-step.
    private void SetKeyFingerprint(string name, string key, ulong fingerprint)
    {
        if (!_keyFingerprint.TryGetValue(name, out var perName))
        {
            perName = new Dictionary<string, ulong>(StringComparer.Ordinal);
            _keyFingerprint[name] = perName;
        }
        perName[key] = fingerprint;
    }

    // Every call site adds exactly one entry immediately after creating a fresh bucket (see
    // IndexAdd, ReindexFingerprint, EnsureFingerprintBucketsBuilt), and the production shape — every
    // fragment carrying a distinct deep-argument fingerprint — means the overwhelming majority of
    // buckets ever created hold exactly one entry for their entire lifetime. Pre-sizing to 1 avoids
    // List{T}'s default first-growth to a 4-slot backing array, which would otherwise waste 3 slots
    // per singleton bucket.
    private const int InitialBucketCapacity = 1;

    private List<string> GetOrCreateBucket(string name)
    {
        if (!_byName.TryGetValue(name, out var bucket))
        {
            bucket = new List<string>(InitialBucketCapacity);
            _byName[name] = bucket;
        }
        return bucket;
    }

    private static List<string> GetOrCreateFingerprintBucket(Dictionary<ulong, List<string>> byFingerprint, ulong fingerprint)
    {
        if (!byFingerprint.TryGetValue(fingerprint, out var bucket))
        {
            bucket = new List<string>(InitialBucketCapacity);
            byFingerprint[fingerprint] = bucket;
        }
        return bucket;
    }

    /// <summary>
    /// Returns the keys sharing BOTH <paramref name="name"/> AND the live deep fingerprint
    /// <paramref name="fingerprint"/> — the O(1)-average entry point <c>QueryMerger.FindMergeTarget</c>
    /// uses instead of scanning every same-named candidate. Self-heals: any visited entry whose
    /// live fingerprint no longer matches the bucket it was found in is moved to its correct
    /// current bucket and excluded from this call's result (see class remarks on the two ways a
    /// fingerprint can drift after indexing). The returned list, if non-null, is safe for the
    /// caller to enumerate directly — no further filtering is required.
    /// </summary>
    public List<string>? GetMergeCandidatesByFingerprint(Dictionary<string, FieldDefinition> fields, string name, ulong fingerprint)
    {
        EnsureSynced(fields);
        EnsureFingerprintEpochCurrent();
        var byFingerprint = EnsureFingerprintBucketsBuilt(fields, name);

        if (!byFingerprint.TryGetValue(fingerprint, out var bucket) || bucket.Count == 0)
            return null;

        HealBucketInPlace(fields, name, byFingerprint, fingerprint, bucket);
        return bucket.Count > 0 ? bucket : null;
    }

    // Walks `bucket` (believed to hold `expectedFingerprint` under `name`'s sub-index) and, for
    // every entry whose LIVE field fingerprint no longer matches, removes it from `bucket` and
    // re-inserts it into its correct current bucket within the same `byFingerprint` map. Runs in a
    // single forward pass with swap-remove, so healing is O(entries actually stale) beyond the
    // unavoidable O(bucket size) fingerprint recompute — each recompute is itself O(1) when the
    // field's own memo is valid.
    private void HealBucketInPlace(
        Dictionary<string, FieldDefinition> fields,
        string name,
        Dictionary<ulong, List<string>> byFingerprint,
        ulong expectedFingerprint,
        List<string> bucket)
    {
        for (var i = bucket.Count - 1; i >= 0; i--)
        {
            var key = bucket[i];

            // The key itself can vanish from the root dictionary between indexing and this read
            // only via a path that changes Count (caught by EnsureSynced above and rebuilt from
            // scratch) — so fields.TryGetValue here is expected to always hit. Guard defensively
            // rather than throw: treat a miss as "no longer a candidate" and drop it.
            if (!fields.TryGetValue(key, out var liveField))
            {
                bucket.RemoveAt(i);

                // _keyFingerprint[name] is written in lock-step with every _byNameFingerprint bucket
                // insertion (see SetKeyFingerprint), so reaching a populated bucket under `name`
                // guarantees the per-name reverse map already exists.
                _keyFingerprint[name].Remove(key);
                continue;
            }

            var liveFingerprint = FieldDefinitionExtensions.ComputeDeepFingerprint(liveField);
            if (liveFingerprint == expectedFingerprint) continue;

            bucket.RemoveAt(i);
            GetOrCreateFingerprintBucket(byFingerprint, liveFingerprint).Add(key);
            SetKeyFingerprint(name, key, liveFingerprint);
        }
    }

    /// <summary>
    /// Notifies the index that the field at <paramref name="key"/> (sharing <paramref name="name"/>)
    /// was just mutated in place by <see cref="FieldDefinitionExtensions.MergeFieldsInPlace"/> and
    /// its deep fingerprint may have changed, so it must be re-bucketed under its new live
    /// fingerprint. Must be called immediately after every in-place merge that goes through
    /// <see cref="FieldDefinitionExtensions.MergeFieldsInPlace"/> — otherwise the entry would sit
    /// under its stale pre-merge fingerprint until it happens to be visited and healed by
    /// <see cref="GetMergeCandidatesByFingerprint"/> for that old bucket, which a bucketing scheme
    /// keyed on the NEW fingerprint would never trigger on its own.
    /// </summary>
    public void ReindexFingerprint(Dictionary<string, FieldDefinition> fields, string name, string key)
    {
        // If this name's fingerprint sub-buckets were never built, there is nothing to fix — the
        // next GetMergeCandidatesByFingerprint call for this name builds them fresh from the live
        // (already-mutated) fields, picking up the correct fingerprint directly.
        if (!_byNameFingerprint.TryGetValue(name, out var byFingerprint)) return;
        if (!fields.TryGetValue(key, out var liveField)) return;

        liveField.EnsureMergeMemoTracker().Attach(_memoScope);
        var liveFingerprint = FieldDefinitionExtensions.ComputeDeepFingerprint(liveField);

        // Remove the key from whichever bucket currently holds it. _keyFingerprint[name] records
        // exactly that bucket's fingerprint, so this is an O(1) dictionary lookup + O(bucket size)
        // removal from that ONE bucket — never a scan across every fingerprint bucket under the
        // name. That per-name scan was the quadratic hazard this reverse index exists to remove:
        // the production shape accumulates many singleton buckets (one per distinct incoming
        // filter) under one name, so scanning all of them on every successful merge was O(N) per
        // merge / O(N^2) over a chain of N Includes. A miss here (key not yet tracked, e.g. it was
        // added to _byName but its fingerprint buckets were built before this key existed under an
        // old code path) is harmless — it just means there is no stale bucket entry to remove.
        if (_keyFingerprint.TryGetValue(name, out var perName)
            && perName.TryGetValue(key, out var oldFingerprint)
            && oldFingerprint != liveFingerprint
            && byFingerprint.TryGetValue(oldFingerprint, out var oldBucket))
        {
            oldBucket.Remove(key);
        }

        var targetBucket = GetOrCreateFingerprintBucket(byFingerprint, liveFingerprint);
        if (!targetBucket.Contains(key))
        {
            targetBucket.Add(key);
        }
        SetKeyFingerprint(name, key, liveFingerprint);
    }

    private void EnsureFingerprintEpochCurrent()
    {
        var liveEpoch = _memoScope.Version;
        if (liveEpoch == _syncedMergeMemoEpoch) return;

        _byNameFingerprint.Clear();
        _fingerprintBucketsBuilt.Clear();
        _keyFingerprint.Clear();
        _syncedMergeMemoEpoch = liveEpoch;
    }

    // Returns `name`'s fingerprint sub-index, building it on first use for this synced/epoch
    // generation. Always returns a live, non-null dictionary (created here if absent) — callers
    // never need to re-check for its existence afterwards.
    private Dictionary<ulong, List<string>> EnsureFingerprintBucketsBuilt(Dictionary<string, FieldDefinition> fields, string name)
    {
        if (_fingerprintBucketsBuilt.Contains(name))
        {
            return _byNameFingerprint[name];
        }

        var byFingerprint = new Dictionary<ulong, List<string>>();
        _byNameFingerprint[name] = byFingerprint;

        if (_byName.TryGetValue(name, out var keys))
        {
            foreach (var key in keys)
            {
                if (!fields.TryGetValue(key, out var field)) continue;
                field.EnsureMergeMemoTracker().Attach(_memoScope);
                var fingerprint = FieldDefinitionExtensions.ComputeDeepFingerprint(field);

                GetOrCreateFingerprintBucket(byFingerprint, fingerprint).Add(key);
                SetKeyFingerprint(name, key, fingerprint);
            }
        }

        _fingerprintBucketsBuilt.Add(name);
        return byFingerprint;
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
        _byNameFingerprint.Clear();
        _fingerprintBucketsBuilt.Clear();
        _keyFingerprint.Clear();
        _allKeys.Clear();
        _suffixCounters.Clear();
        foreach (var (key, field) in fields)
        {
            GetOrCreateBucket(field.Name).Add(key);
            _allKeys.Add(key);
        }
        _syncedCount = fields.Count;

        // The fingerprint sub-buckets were just cleared and will rebuild lazily against the
        // now-current field tree — resync the epoch snapshot too, so EnsureFingerprintEpochCurrent
        // does not immediately consider that fresh rebuild stale on the very next check.
        _syncedMergeMemoEpoch = _memoScope.Version;
    }
}
