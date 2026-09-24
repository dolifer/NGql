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

## Measured and not planned

Measured during the post-2.2.0 pass. Each would change public behavior or trade memory for
speed, so none is planned while the public API and its observable behavior must stay as they are.

- **Cache the sorted child order per node.** Sorting dominates rendering wide selections: 500
  fields take 20–30 µs with any comparer (ordinal, case-insensitive, or a hand-written ASCII
  loop), and introsort does not detect already-sorted input (18 µs). Sorting children in place
  would change the order `FieldDefinition.Fields` and `QueryDefinition.Fields` enumerate in, and
  which same-named sibling internal lookups find first. A separate cached array keeps both but
  adds an allocation to every first render (the common build-once, render-once pattern) and
  retained memory to held queries, and every mutation path would have to invalidate it.
- **Replace `SortedDictionary` as field-argument storage.** A sorted-array map measured 60–70%
  less memory and about 2× faster for one field's argument lifecycle (1–8 arguments), roughly 10%
  of bytes on argument-heavy queries. The public `FieldDefinition` constructor keeps the caller's
  `SortedDictionary` (later changes to it, and `Where()` writes, are shared) and `Arguments`
  returns that live instance, so a swap changes observable behavior. A compatible version would
  switch storage back on the first public read and handle both forms at every internal site.
- **Drop the per-collection lock object in `FieldChildren`** (24 B per node with children, 2–6% of
  build allocation on nested shapes). Locking on the instance would contend with any user code
  that locks on the collection returned by `FieldDefinition.Fields`.
