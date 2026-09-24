# Open performance tasks

Completed work and its final metrics are in
[PERFORMANCE_REVIEW.md](PERFORMANCE_REVIEW.md). Every task preserves the public
API and the legacy reflection-based `Include`, and needs a baseline, a regression
check and a measured result before it is closed.

- [ ] Setting an existing `QueryBlock` argument again scans every key to recover
  its stored casing (`QueryBlock.TryGetExistingKey`): O(n) per re-set after an
  O(log n) membership check. First-time keys are unaffected. Measure re-set-heavy
  workloads before changing the storage.
- [ ] Scalar-argument rendering measured about 2.7% slower after the memo-cache
  compaction, with unchanged allocation and no identified cause. Re-measure on a
  quiet host; investigate object layout or code alignment only if it reproduces.
- [ ] Rebuild the retained-heap harness as a tracked project under `tests/` if retained-memory
  figures need to be regenerated; the original lived in the git-ignored `artifacts/` folder and
  was deleted by `make clean`.

## Measured and deferred

These were measured during the post-2.2.0 pass and left out on purpose. Each needs a design
decision before it is worth doing.

- [ ] **Cache the sorted child order per node.** Sorting dominates rendering wide selections:
  500 fields take 20–30 µs to sort with any comparer (ordinal, case-insensitive, or a hand-written
  ASCII loop), and introsort does not detect already-sorted input (18 µs). A cached order would
  make repeated `ToString()` calls several times faster, but every mutation path (append, set,
  in-place merge, alias changes) must invalidate it, and root fields live in a plain `Dictionary`
  that many internal sites mutate directly.
- [ ] **Replace `SortedDictionary` as field-argument storage.** A sorted-array map measured
  60–70% less memory and about 2× faster for one field's argument lifecycle (1–8 arguments), or
  roughly 10% of bytes on argument-heavy queries. It cannot be a plain swap: the public
  `FieldDefinition` constructor keeps the caller's `SortedDictionary` (later changes to it, and
  `Where()` writes, are shared), and `Arguments` returns that live instance. A compatible version
  must switch storage back to `SortedDictionary` on the first public read, handle both forms at
  every internal site, and audit the record `with` copies that share argument storage.
- [ ] **Drop the per-collection lock object in `FieldChildren`** (24 B per node with children,
  2–6% of build allocation on nested shapes). Locking on the instance instead is safe internally,
  but the collection is reachable from user code through `FieldDefinition.Fields`.
