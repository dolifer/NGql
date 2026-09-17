# Remaining performance tasks

All tasks preserve the public API and legacy reflection-based `Include`. Work is
sequential. Each task requires a baseline, implementation or measured disposition,
regression checks, and findings in `PERFORMANCE_REVIEW.md`. Commits are local only.

- [x] T1 — Root argument rendering: remove temporary dictionaries while preserving
  ordinal ordering, remapped keys, case-sensitive names and collision precedence.
- [x] T2 — Merge-index invalidation: isolate unrelated query mutations without
  weakening descendant, detached-builder or concurrent invalidation guarantees.
- [x] T3 — Root selection rendering: re-measure single-root dispatch and implement
  a beneficial shortcut without changing field ordering or rendering behavior.
- [x] T4 — Type caches: measure dynamic-schema/collectible-type retention and bound
  retention while preserving hot-cache behavior and concurrency correctness.
- [x] T5 — Pool retention: measure high-water storage and medium-query throughput;
  reduce idle builder retention while retaining reuse in measured normal workloads.
- [x] T6 — Existing optimizations: re-measure representative traversal, merge,
  path-cache and sink workloads; strengthen regression/performance coverage for
  invalidation and concurrent snapshots, and resolve any demonstrated regressions.

## Execution log

- Baseline branch: `perf/reduce-render-allocations`, commit `9e3edfa`.
- T1 complete: pooled stable-precedence entries; randomized differential tests pass
  on all frameworks. 1,000 scalar/variable allocations fall by about 31 KB/render.
- T2 complete: observer-scoped invalidation, shared-root and deep-clone isolation.
  Unrelated mutation at 1,000 candidates: 81.6 µs/268,728 B → 0.290 µs/296 B.
- T3 complete: reference-backed singleton span; one-field writes 44.5 → 32.8 ns,
  with unchanged allocations and no meaningful 10/1,000-field control regression.
- T4 complete: 20,000 cached names → 4,096 retained; collectible types released
  by all three metadata caches. Stable hot-cache performance is unchanged.
- T5 complete: total per-thread builder capacity bounded to 262,144 characters;
  measured large-burst retention falls 75%, while four medium builders still reuse.
- T6 complete: removed 64 B sorting adapters; warmed sorted root/nested writes
  allocate zero bytes. Path-building timing variation did not reproduce in an
  isolated repeat. Existing invalidation and snapshot guards remain intact.
- Final validation: 2,101 unit tests and 91 integration tests pass on each of
  .NET 8, 9 and 10. All six scoped findings are resolved; measured tradeoffs remain
  documented in `PERFORMANCE_REVIEW.md`.
