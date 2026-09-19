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
