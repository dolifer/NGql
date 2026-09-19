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
- [x] T7 — Dotted-path construction: stop allocating a merge tracker for the
  discarded builder in argument-free dotted `AddField` calls (`30dcbc8`).
- [x] T8 — Field state and merge clones: compact memo caches (144 → 128 B), reuse
  name/alias/path strings in merge clones, create the path index lazily (`6a50a29`).
- [x] T9 — Optional field state: fragments, spreads, directives and metadata behind
  one copy-on-write reference; computed effective name (128 → 96 B) (`feb2e75`).
- [x] T10 — Release timing check: paired complex-merging and simple-query timing
  against NGql.Core 2.1.0 on the T9 assembly.
- [x] T11 — Code review fixes: stale merge index from dotted-path builders, short
  UTF-8 destination spans, .NET 8 preservation prefix strings, pool budget with
  nested renders, single argument/variable rendering.
- [x] T13 — Second review pass: stale index after captured-builder `AddField` with
  arguments; discarded builder, chain lookup and pool scratch trimmed.
- [ ] T12 — Open review findings: FIFO type-name cache under churn; eager merge
  tracker on every root `FieldBuilder`.

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
- T7 complete: complex merging 7,072 → 6,872 B; simple query 1,760 → 1,640 B;
  200 dotted paths 276,936 → 260,896 B. 2,104 unit / 91 integration tests.
- T8 complete: allocation falls or is flat in 40 of 41 paired cases; complex merging
  6,871 → 5,202 B and 1,439 → 1,275 ns. Scalar-argument render stays about 2.7%
  slower across four ordered reruns (cause not established). 2,113 / 91 tests.
- T9 complete: every shared construction workload allocates at or below 2.1.0.
  Cost: cold metadata builds +12% time and +80 B, cold inline-fragment builds +6%
  time. 2,123 / 91 tests on each of .NET 8, 9 and 10.
- T10 complete: in published / local / local / published order, complex merging
  measures 1,462.4 / 1,217.4 / 1,202.9 / 1,455.5 ns (about 17% faster than 2.1.0)
  and simple query 365.0 / 320.9 / 318.5 / 360.4 ns (about 12% faster). All 99.9%
  intervals are below ±18 ns and do not overlap between versions. The earlier
  release-relative complex-merging slowdown (+5.2% at `815ea8b`) is reversed.
- T9 review follow-up: holder replacement is compare-and-swap, `DeepClone` builds
  one holder. Allocation unchanged in six paired cases; feature-bearing cold builds
  cost 1–3% more time. 2,125 / 91 tests. Source: `cas-check-1-layout96` … `-4-`.
- T11 complete: four stale-index regressions fail before and pass after; bounded
  UTF-8 writer no longer throws; .NET 8 preservation 114,576 B of prefix strings
  → under 16 KB for 512 deep paths; pool keeps the 200,000-character builder in
  either return order; single argument 63.0 → 44.0 ns and 208 → 80 B, single
  variable 69.1 → 40.8 ns and 208 → 88 B. 2,140 / 91 tests.
- T13 complete: two stale-index regressions fail before and pass after. Dictionary
  arguments ×50: 49.03 / 49.15 → 48.20 / 48.44 µs and 185.55 → 181.64 KB; three
  controls unchanged. 2,144 / 91 tests. Source: `complex-merge-opt/review2-*`.

## Measurement ledger (T7–T10)

All values: .NET 9.0.9, Apple M4, BenchmarkDotNet 0.15.7, in-process. Bytes are
managed allocation per operation unless marked retained. Sources are directories
under the git-ignored `artifacts/benchmarks/`. 2.1.0 values come from the
release comparison and T7 runs unless a row says otherwise.

| Measure | 2.1.0 | Before T7 | After T7 | After T8 | After T9 |
| --- | ---: | ---: | ---: | ---: | ---: |
| Complex merging, bytes | 6,360 | 7,072 | 6,872 | 5,202 | 4,721 |
| Complex merging, ns (suite run) | 1,429 | 1,456 | 1,448 / 1,439 | 1,275 | 1,224 |
| Simple query, bytes | 1,600 | 1,760 | 1,640 | 1,516 | 1,413 |
| 200 dotted paths, bytes | 236,944 | 276,936 | 260,896 | 251,259 | 232,018 |
| 500 flat fields, bytes | 129,448 | 153,400 | 153,400 | 145,285 | 129,321 |
| Bulk building ×100, bytes | 225,597 | — | 236,001 | 219,996 | 204,001 |
| Merge 100 fragments, bytes | — | — | 144,826 | 139,962 | 130,393 |
| Arguments pool stress, bytes | 40,366 | — | 43,008 | 39,649 | 37,724 |
| Cold directives build, bytes | — | — | 4,328 | 4,200 | 4,200 |
| Cold inline fragments build, bytes | — | — | 4,728 | 4,520 | 4,360 |
| Cold named fragments build, bytes | — | — | 2,976 | 2,816 | 2,752 |
| Cold metadata build, bytes | — | — | 2,464 | 2,304 | 2,384 |
| `FieldDefinition` instance, bytes | 96 | 144 | 144 | 128 | 96 (derived) |
| Retained heap, merged builder only | 3,008 | — | 3,360 | 2,576 | 2,352 |
| Retained heap, fragments + merged | 5,752 | — | 6,520 | 4,848 | 4,368 |
| Unit / integration tests passing | — | 2,101 / 91 | 2,104 / 91 | 2,113 / 91 | 2,123 / 91 |

Ordered reruns (before / after / after / before), five warmups, ten iterations:

| Case | Pass | Before runs | After runs | Verdict |
| --- | --- | ---: | ---: | --- |
| Scalar-argument render, ns | T8 | 579.7 / 587.8 | 603.7 / 595.5 | about 2.7% slower, cause unknown |
| Oversized render, µs | T8 | 132.12 / 131.18 | 132.17 / 132.31 | not reproduced |
| Expression preservation ×10, µs | T8 | 10.64 / 12.22 | 11.74 / 11.90 | not reproduced |
| ToString ×10, µs | T8 | 3.223 / 3.187 | 3.180 / 3.267 | not reproduced |
| Cold metadata build, ns | T9 | 434.0 / 444.4 | 511.2 / 475.0 | about 12% slower, accepted cost |
| Cold inline fragments build, ns | T9 | 934.6 / 937.5 | 1,014.0 / 969.3 | about 6% slower, accepted cost |
| Complex merging vs 2.1.0, ns | T10 | 1,462.4 / 1,455.5 | 1,217.4 / 1,202.9 | about 17% faster than release |
| Simple query vs 2.1.0, ns | T10 | 365.0 / 360.4 | 320.9 / 318.5 | about 12% faster than release |
| Preserve guardrail, µs | T9 | 2.419 / 2.454 | 2.494 / 2.406 | not reproduced |
| UTF-8 guardrail, µs | T9 | 4.934 / 4.785 | 4.929 / 4.838 | not reproduced |

T11 argument rendering (`review-fixes/args-before` → `args-after`):

| Entries | Arguments ns | Arguments B | Variables ns | Variables B |
| --- | ---: | ---: | ---: | ---: |
| 1 | 63.01 → 44.04 | 208 → 80 | 69.12 → 40.75 | 208 → 88 |
| 10 | 412.2 → 341.0 | 632 → 472 | 404.5 → 346.3 | 896 → 848 |
| 1,000 | 58,233 → 54,060 | 36,184 → 36,024 | 58,628 → 55,225 | 70,296 → 70,248 |

Sources: T7 `complex-merge-fix/` (`bdn-before`, `bdn-after`, `bdn-published`,
`after-profile.txt`); T8 `complex-merge-opt/` (`bdn-before`, `bdn-after`,
`recheck-1-before` … `recheck-4-before`, `heap-before-final.txt`, `heap-after.txt`,
`heap-published.txt`); T9 `complex-merge-opt/` (`bdn-layout96`,
`recheck96-1-after` … `recheck96-4-after`, `heap-layout96.txt`); T10
`complex-merge-opt/release-check-1-published` … `release-check-4-published`
("before" is the 2.1.0 package, "after" the T9 assembly). The "After T7"
suite values are T8's `bdn-before` run; the "Before T7" and first "After T7"
timing come from T7's own wrapper benchmark, so compare columns within a pass.
