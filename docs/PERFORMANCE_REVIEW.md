# Performance review — 2026-09-17

Reviewed query construction, merge indexing, preservation, rendering, type metadata
caches, and pooling. The implemented changes target rendering in `NGql.Core`;
the demo server and public API are unchanged. Baseline: `9502ece`.

## Implemented changes

- `ValueFormatter`: format floating-point values, decimals, and dates into a
  64-character stack buffer using a generic `ISpanFormattable` helper. This removes
  temporary strings and interface boxing while retaining invariant culture,
  date formatting, and a fallback if a future format exceeds the buffer.
- `QueryTextBuilder`: render nested selections containing at most one field
  directly from their existing span. No sort buffer is needed. Multiple fields
  still use the existing comparer and pooled snapshot. Both paths share the same
  rendering method and take one child-collection snapshot.
- `QueryTextBuilder.ReturnToPool`: check capacity and pool availability before
  clearing the builder. `StringBuilder.Clear()` can allocate a contiguous buffer
  when collapsing chunks; allocating it before discarding the builder is wasted
  work. The existing limit remains 262,144 UTF-16 characters (roughly 512 KiB of
  character storage per retained builder). Oversized builders are still rejected.

## Measurements

BenchmarkDotNet 0.15.7, .NET 9.0.9, Apple M4, macOS 27.0. Both runs used
ShortRun with the in-process toolchain, three warmups and three measured
iterations. Builders and argument values are prepared outside the measured calls.

| Scenario | Before mean | After mean | Before bytes/op | After bytes/op |
| --- | ---: | ---: | ---: | ---: |
| Five scalar arguments, rendered to string | 629.7 ns | 527.4 ns | 928 | 536 |
| Sixteen-level single-child chain, rendered to string | 971.5 ns | 357.4 ns | 2,256 | 2,256 |
| 300,000-character argument, written to `TextWriter.Null` | 177.76 µs | 139.64 µs | 1,234,128 | 617,649 |

Observed mean render times decreased by approximately 16%, 63%, and 21%.
Scalar allocation fell 42%; oversized-render allocation fell 50%. The chain's
allocation is unchanged because the removed buffers were pooled; its gain is
reduced pool traffic and sorting overhead.

These are short local microbenchmarks, not application CPU utilization or
retained-heap measurements. Timing confidence intervals are broad, and process
priority could not be elevated. Allocation reductions are the stronger evidence;
do not extrapolate the timing percentages to an entire service.

Reproduce from the repository root:

```sh
dotnet run --project tests/BenchmarkRunner -c Release -f net9.0 -p:NuGetAudit=false -- --filter '*RenderingBenchmark*' --job short --inProcess --artifacts /tmp/ngql-rendering
dotnet test tests/Core.Tests/Core.Tests.csproj -c Release --no-restore -p:NuGetAudit=false
dotnet test tests/Core.IntegrationTests/Core.IntegrationTests.csproj -c Release --no-restore -p:NuGetAudit=false
```

For a baseline rerun, use `9502ece` in a separate checkout and copy only
`tests/BenchmarkRunner/Benchmarks/RenderingBenchmark.cs` into it. Raw results from
this review were written to `/tmp/ngql-perf-before` and `/tmp/ngql-perf-after`.
`NuGetAudit=false` is a command-local workaround for an existing NU1902 warning
from `Microsoft.Build.Tasks.Git` 8.0.0; dependency versions were not changed.

Validation passed on .NET 8, 9, and 10: 2,052 unit tests and 91 integration tests
per framework. Added 17 scalar-format cases covering extrema, epsilon, signed
zero, decimal scale, dates and offsets under a non-invariant current culture.
Strengthened the oversized-builder pool test to verify rejection and an empty,
bounded replacement, then reran it on all three frameworks. Existing sorting,
fragment, escaping, sink, and concurrent-render regression tests also pass.

## Follow-up fixes

The follow-up review corrected invariant formatting for signed integers and
removed linear scans from root-variable lookup and preservation-path pruning.
The expanded suite passes 2,064 unit tests and 91 integration tests per target
framework.

BenchmarkDotNet ShortRun results on .NET 9.0.9:

| Scenario | Before mean | After mean | Before bytes/op | After bytes/op |
| --- | ---: | ---: | ---: | ---: |
| 1,000 root variables | 4,434.9 µs | 373.7 µs | 294,245 | 110,402 |
| 1,000 unrelated preservation paths | 501.3 µs | 40.3 µs | 97,188 | 73,192 |
| 1,000 signed integers | 10.7 µs | 14.7 µs | 12,032 | 12,032 |

The invariant integer implementation measured slower in this run and guarantees
GraphQL's ASCII minus sign under cultures with a custom negative sign. This is
an observed implementation cost, not proof that invariant formatting must be slower.
It creates no additional managed
allocations. Root-variable lookup is now logarithmic per variable instead of
scanning every argument value. Preservation probes only dotted ancestors; .NET 9
and later use allocation-free alternate hash-set lookups.

A corrected .NET 8.0.23 measurement of the committed preservation implementation
gives 35.2 µs and 73,192 bytes for 1,000 paths, versus 481.0 µs and 97,184 bytes
before. At 10 paths it gives 308.5 ns and 800 bytes, versus 254.0 ns and 1,032
bytes before. The earlier report's 66.5 µs / 208,368-byte result belonged to an
intermediate implementation and has been replaced. The committed minimum-path-
length check avoids prefix allocations for this sibling-path workload; mixed
depths can still allocate lookup strings on .NET 8. The corrected run used three
warmup and three measurement iterations, with raw reports in
`/tmp/ngql-preservation-verified-net8`.

## UTF-8 and argument allocation pass

Baseline: `b837e1f`. Three additional changes:

- UTF-8 output uses `Encoding.UTF8.GetBytes` directly when the rendered text is
  contained in one builder chunk, avoiding an encoder allocation. Multiple chunks
  still share a stateful encoder so split surrogate pairs are preserved.
- Variable extraction, object normalization, structural comparison and rendering
  share the existing property-metadata cache. Property values are still read on
  every operation. This saves repeated metadata-array allocations, at the cost of
  retaining one metadata entry per encountered type; cold cache and collectible
  assembly behavior are not measured here.
- List normalization reserves capacity when the input exposes its count without
  enumeration. Unknown-length inputs retain the growing-list behavior. The change
  removes intermediate backing arrays and their copies.

Measured on the same Apple M4 and .NET 9.0.9 with BenchmarkDotNet 0.15.7,
five warmups and eight measurement iterations. Tests and benchmarks ran separately.
The UTF-8 benchmarks reuse a caller-owned output buffer; setup and cache warmup
are excluded. The large case contains a 10,000-character argument. Reported
allocations cover the entire benchmark operation, not just the changed helper.

| Scenario | Before mean | After, first run | After, repeat | Bytes/op before → after |
| --- | ---: | ---: | ---: | ---: |
| Small UTF-8 query | 101.4 ns | 106.8 ns | 97.6 ns | 48 → 0 |
| Large UTF-8 query | 5.081 µs | 7.533 µs | 5.347 µs | 176 → 128 |
| Object-argument query construction | 886.8 ns | 1,023.9 ns | 768.1 ns | 2,488 → 2,376 |

Both after runs produced the same allocations. CPU results varied substantially
between runs, including changes in direction, so these two optimizations are
accepted for their demonstrated allocation reductions; a CPU speedup is not
established. In particular, no throughput improvement is claimed for large UTF-8
queries. The 48-byte saving depends on builder chunk shape; multi-chunk output
still needs its encoder.

| List normalization | Before mean | After mean | Bytes/op before → after |
| --- | ---: | ---: | ---: |
| 10 items | 225.2 ns | 194.1 ns | 624 → 432 |
| 1,000 items | 7.669 µs | 6.743 µs | 16,896 → 8,352 |

List allocation decreased 31% and 51%, respectively. Observed mean time decreased
14% and 12%, though the 99.9% timing confidence intervals overlap. Allocation
savings are stronger evidence than the timing percentages. These measurements do
not establish a reduction in whole-process retained heap or service CPU usage.

Validation: 2,067 unit tests and 91 integration tests pass on each of .NET 8,
.NET 9 and .NET 10. New coverage checks unpaired-surrogate replacement encoding
and verifies that metadata reuse does not cache mutable object values or variables.

```sh
dotnet run --project tests/BenchmarkRunner -c Release -f net9.0 -p:NuGetAudit=false -- --filter '*AllocationHotspotBenchmark*' --job short --warmupCount 5 --iterationCount 8 --inProcess --artifacts /tmp/ngql-hotspots
dotnet run --project tests/BenchmarkRunner -c Release -f net9.0 -p:NuGetAudit=false -- --filter '*ListArgumentBenchmark*' --job short --warmupCount 5 --iterationCount 8 --inProcess --artifacts /tmp/ngql-lists
```

To reproduce the baseline, copy the two new benchmark source files into a checkout
of `b837e1f` and run the same commands. Raw reports for this pass are under
`/tmp/ngql-hotspots-before-net9`, `/tmp/ngql-hotspots-after-net9`,
`/tmp/ngql-hotspots-repeat-net9`, `/tmp/ngql-lists-before-net9` and
`/tmp/ngql-lists-after-net9`.

## Follow-up loop: argument insertion, rendering, type lookup and retention

Baseline for this pass: `b598a2b`. Public signatures and the legacy `Include`
reflection path remain unchanged. All changes are internal:

- Check the argument tree for membership before scanning for original key casing.
  New keys no longer enumerate every existing key (including its traversal-stack
  allocations). Existing-key updates and case-collision rejection keep their rules.
- Collect rendered arguments in a pre-sized ordinal hash dictionary, then sort
  a pooled key array. This preserves variable precedence and ordinal wire order
  without allocating a tree node per rendered argument. Single-entry dictionaries
  render directly; pooled keys are cleared and returned even when rendering throws.
- On .NET 9/10, look up custom types using the existing span before allocating a
  string. Cache misses retain the concurrent `GetOrAdd` path; .NET 8 retains its
  previous string lookup. Cache lifetime/eviction policy is unchanged.
- Reject hash sets with backing capacity above 256 as well as live count above
  128. The capacity allowance accommodates normal bucket rounding for 128 items.

BenchmarkDotNet 0.15.7, .NET 9.0.9, Apple M4, Release, in-process, three warmups
and five measurement iterations. Inputs are prepared outside measured operations;
rendering reuses queries, while insertion and field creation construct new queries.
Benchmarks and test suites were run separately. Allocation figures include returned
query objects/strings and represent managed allocations per operation, not RSS.

| Workload | Before | After | Allocated before → after |
| --- | ---: | ---: | ---: |
| Insert 10 arguments individually | 1.292 µs | 1.079 µs | 1.67 → 0.80 KiB |
| Insert 1,000 arguments individually | 4,173.3 µs | 387.1 µs | 234.48 → 54.95 KiB |
| Insert batch of 10 arguments | 1.187 µs | 1.063 µs | 2.39 → 1.82 KiB |
| Insert batch of 1,000 arguments | 314.5 µs | 298.3 µs | 196.84 → 142.13 KiB |
| Render 1 scalar argument | 116.3 ns | 114.4 ns | 552 → 528 B |
| Render 1 variable | 113.4 ns | 114.6 ns | 480 → 456 B |
| Render 10 scalar arguments | 885.1 ns | 558.5 ns | 1,408 → 1,160 B |
| Render 10 variables | 1,133.9 ns | 653.8 ns | 1,672 → 1,424 B |
| Render 1,000 scalar arguments | 270.2 µs | 81.5 µs | 92,497 → 67,289 B |
| Render 1,000 variables | 350.3 µs | 94.2 µs | 126,610 → 101,401 B |
| Create field with cached custom type | 163.5 ns | 153.3 ns | 1,040 → 976 B |
| Create field with common String type | 132.7 ns | 132.3 ns | 976 → 976 B |

The 1,000-scalar baseline was noisy (99.9% interval ±112.3 µs); even its lower
bound exceeds the final result's upper bound (81.5 ±4.5 µs). Treat the speedup
ratio as approximate. Single-entry render and common-type timings are effectively
unchanged. Custom-type creation improved about 6%, removing 64 B per operation.

Two experiments were corrected or rejected during the loop:

- A direct single-root-selection renderer measured 270.4 → 270.6 ns for the
  16-level single-child chain, with unchanged 2,256 B allocation. Reverted because
  the refactoring did not establish a benefit.
- The first hash-dictionary renderer rented keys even for one entry, slowing the
  scalar case to 141.5 ns and the variable case to 140.2 ns. The direct-entry path
  restored baseline timing and reduced allocation by 24 B. Larger cases reproduced
  the gains (initial 1,000-variable result 92.6 µs; final 94.2 µs).

Retention validation is separate from throughput benchmarking: regression tests
grow a pooled set beyond 2,000 backing slots, clear or remove its items, and return
it. Both tests failed before the guard because the next rental was the same large
set. Afterward, rentals have at most 256 slots and normal 128-item sets are still
reused. Current production callers do not clear/remove items before return, so
this hardens the pool contract; it is not a demonstrated current-workload RSS gain.

Validation: 2,075 unit tests and 91 integration tests pass on each of .NET 8, 9 and
10. Added coverage checks
ordinal root/nested rendering, variable precedence, mutations between renders,
large-block collision atomicity, sliced/case-sensitive/concurrent type lookups,
and pool high-water retention. No exposed method signature was changed.

```sh
dotnet run --project tests/BenchmarkRunner -c Release -f net9.0 -p:NuGetAudit=false -- --filter '*ArgumentInsertionBenchmark*' '*BlockArgumentRenderingBenchmark*' '*TypeCacheBenchmark*' --job short --warmupCount 3 --iterationCount 5 --inProcess --artifacts /tmp/ngql-followup-loop
```

For the baseline, copy the three benchmark files into a checkout of `b598a2b`.
Raw reports: `/tmp/ngql-insertion-before`, `/tmp/ngql-insertion-after`,
`/tmp/ngql-block-arguments-before`, `/tmp/ngql-block-arguments-after`,
`/tmp/ngql-type-cache-before`, and `/tmp/ngql-loop-final-net9`.

## Follow-up loop: small arguments and alias collisions

Baseline: `49c7bc0`. Three further internal changes passed measurement review:

- Render a lone explicit argument or a lone root variable directly from its source
  collection, avoiding the temporary argument dictionary. Mixed arguments and
  variables continue through the existing precedence/ordering logic.
- Stop variable discovery immediately for empty dictionaries/lists. Non-empty
  collections retain reference-based cycle detection; a later call still sees
  mutations to a previously empty collection.
- On .NET 9/10, test alias suffix candidates as spans and allocate only the chosen
  string. .NET 8 retains string lookup. Alias formatting now bounds stack storage
  to 512 characters and rents/clears/returns a buffer for longer names instead of
  making an input-sized stack allocation.

Same .NET 9/M4 benchmark configuration as the preceding loop (three warmups,
five measurements). The host was substantially slower/noisier during this pass:
compare paired runs below, not absolute timings with earlier sections. These are
managed bytes allocated per operation, not measurements of process retained heap.

| Workload | Before → after | Allocation before → after |
| --- | ---: | ---: |
| Render one scalar argument | 262.2 → 233.6 ns | 528 → 208 B |
| Render one variable | 249.1 → 154.6 ns | 456 → 208 B |
| QueryBlock: add empty list argument | 287.6 → 133.4 ns | 528 → 352 B |
| QueryBlock: add empty dictionary argument | 197.1 → 136.9 ns | 608 → 432 B |
| QueryBuilder: empty list within arguments | 694.7 → 652.9 ns | 1,856 → 1,856 B |
| QueryBuilder: empty dictionary within arguments | 798.2 → 624.6 ns | 1,936 → 1,936 B |
| Generate alias with 10 occupied names | 772.3 → 529.3 ns | 496 → 136 B |
| Generate alias with 1,000 occupied names | 88.5 → 59.8 µs | 113,264 → 73,304 B |

The alias microbenchmark binds a delegate to the internal enumerable overload once
in setup; reflection is not in the timed operation. Its suffix-generation core is
also used by nested field alias conflict resolution. This is not an end-to-end
merge speedup claim. The small alias timing confidence intervals overlap, as do
the scalar singleton intervals; allocation reductions are the stronger evidence.
The 1,000-name alias intervals do not overlap (88.5 ±9.9 vs 59.8 ±10.5 µs).

Larger argument-render controls retained allocations (1,160/1,424 B for 10 scalar/
variable entries; approximately 67,289/101,401 B for 1,000). Timings varied in both
directions: 10 scalar entries 1,229 → 1,360 ns, 10 variables 1,565 → 1,529 ns,
1,000 scalars 245.0 → 175.9 µs, 1,000 variables 197.8 → 207.6 µs. Their paired
99.9% confidence intervals overlap; no large-render improvement is claimed here.

The empty QueryBuilder cases still need cycle tracking for their enclosing argument
dictionary, so they show no allocation reduction. The direct QueryBlock cases
avoid that state entirely, saving 176 B each. No broad CPU percentage is inferred
from these small-workload results.

New regression coverage checks lone nested variable references versus root
declarations, null/replaced scalar arguments, empty-then-mutated cyclic containers,
case-insensitive suffix gaps, effective field aliases, and names around the stack
threshold and at 100,000 characters. Public signatures and legacy `Include`
reflection remain unchanged.

Validation: 2,085 unit tests and 91 integration tests pass on each of .NET 8, 9
and 10. Final source review and `git diff --check` found no additional issues in
this change. At that pass, the architectural candidates were unproven; their
subsequent measurements and resolution follow below.

```sh
dotnet run --project tests/BenchmarkRunner -c Release -f net9.0 -p:NuGetAudit=false -- --filter '*BlockArgumentRenderingBenchmark*' '*EmptyArgumentBenchmark*' '*AliasCollisionBenchmark*' --job short --warmupCount 3 --iterationCount 5 --inProcess --artifacts /tmp/ngql-small-paths
```

Copy the two new benchmark files into `49c7bc0` to reproduce the baseline. Raw
reports: `/tmp/ngql-single-entry-before`, `/tmp/ngql-empty-before-single-after`,
`/tmp/ngql-alias-before-empty-after`, and `/tmp/ngql-alias-after`. The mixed artifact
names reflect the sequential loop: singleton changes preceded empty-container
changes, which preceded alias changes; each baseline ran before its own change.

## Task-by-task resolution of remaining findings

Tasks and status are tracked in `PERFORMANCE_TASKS.md`. Baseline: `9e3edfa`.
Measurements use the same .NET 9/M4 setup, three warmups and five measurements,
without concurrent test/benchmark processes. Timing variability remains material.

### T1 — Root argument rendering

Replaced the temporary dictionary with pooled `(key, value, original order)`
entries. Ordinal key sorting and original-order tie-breaking reproduce explicit
argument last-write precedence followed by first missing-variable precedence.
All populated entries are cleared in `finally`. Singleton paths remain unchanged.
Randomized root/nested output comparisons against the former dictionary algorithm
pass on .NET 8/9/10, including remapped keys and case-distinct variables.

| Entries | Scalar time before → after | Scalar bytes | Variable time before → after | Variable bytes |
| --- | ---: | ---: | ---: | ---: |
| 1 | 172.1 → 181.5 ns | 208 → 208 | 225.7 → 192.2 ns | 208 → 208 |
| 10 | 1,093 → 970 ns | 1,160 → 696 | 1,209 → 888 ns | 1,424 → 960 |
| 1,000 | 201.1 → 155.9 µs | 67,289 → 36,249 | 265.9 → 159.9 µs | 101,402 → 70,361 |

Timing confidence intervals overlap, so allocation savings are the firm result.
Artifacts: `/tmp/ngql-t1-before`, `/tmp/ngql-t1-after`; benchmark:
`BlockArgumentRenderingBenchmark`. No public surface change.

### T2 — Merge-index invalidation

Replaced the process-wide epoch with an index-local version observed by tracked
root fields. Record copies share their tracker, deep clones start untracked,
and genuinely shared roots notify all observing indexes. Secondary observers use
weak references so fields do not retain dead indexes. Detached root builders
establish their tracker before copying can separate their references. Ancestor
memo clearing, count resynchronization and live-fingerprint checks remain intact.

| Workload | Before → after | Bytes before → after |
| --- | ---: | ---: |
| Warm merge, 10 candidates | 258.2 → 287.5 ns | 272 → 272 |
| Unrelated mutation then merge, 10 | 863.1 → 269.3 ns | 2,920 → 296 |
| Warm merge, 1,000 candidates | 272.6 → 280.0 ns | 272 → 272 |
| Unrelated mutation then merge, 1,000 | 81,608 → 289.8 ns | 268,728 → 296 |

The large interleaved case is about 282× faster and allocates 99.9% less per call;
warm-control timing intervals overlap. This removes repeated cache reconstruction,
not the work of the unrelated mutation itself. Tradeoff: trackers add one reference
per field, a small object for observed/root-builder fields, and one version object
per index. Shared observers allocate additional bookkeeping only when needed.
Simple typed-field construction increased from 976 B before T2 to 1,024 B after
T2: this is a deliberate construction-cost tradeoff, not a universal allocation
reduction.

The existing full unit suite passed on all frameworks after the redesign, including
detached builders, argument widening and stale-fingerprint regressions. New tests
cover unrelated mutation, shared roots/record copies, deep-clone isolation and
concurrent observer registration. Artifacts: `/tmp/ngql-t2-before`,
`/tmp/ngql-t2-after`; benchmark: `MergeIsolationBenchmark`.

### T3 — Root selection rendering

The final singleton path renders a reference-backed one-element `ReadOnlySpan`
through the existing renderer. It avoids renting/copying/sorting/clearing an array,
does not allocate an inline collection, and leaves multi-field ordering unchanged.

| Fields | Write before → after | String render before → after |
| --- | ---: | ---: |
| 1 | 44.50 → 32.79 ns | 48.04 → 38.35 ns |
| 10 | 182.02 → 183.34 ns | 193.40 → 193.32 ns |
| 1,000 | 75.04 → 74.83 µs | 76.54 → 76.46 µs |

Allocations are unchanged (zero for the singleton writer; 64 B for its returned
string). The first collection-expression experiment improved the singleton too
but slowed 10-field writes/renders to 203.8/216.2 ns; it was replaced, not retained.
The final reference-span implementation removes that regression. All work stays
in the common renderer, including aliases, directives and fragments.
Artifacts: `/tmp/ngql-t3-before`, `/tmp/ngql-t3-after` (rejected intermediate),
`/tmp/ngql-t3-ref-span` (accepted); benchmark: `RootSelectionBenchmark`.

### T4 — Type caches

All five retention regressions failed before the fix: each metadata cache rooted
its collectible type, all 20,000 generated custom names survived collection, and
a 10,000-character custom name stayed cached. After the fix, collectible types
are released in all three cases, exactly 4,096 generated names survive, and the
oversized name is not retained. Tests pass on .NET 8/9/10.

Metadata uses `ConditionalWeakTable`: values remain reusable while their Type is
live without preventing unloading. The custom-name cache uses bounded FIFO
admission (4,096 names, at most 256 characters each); normal hits stay lock-free,
misses serialize eviction/admission. Longer names still work, but are not cached.
This bounds cached name characters to 1,048,576 (about 2 MiB of UTF-16 payload,
excluding dictionary/queue/object overhead). It is not a total process heap bound.

| Hot workload | Before → after | Allocations |
| --- | ---: | ---: |
| Custom-type field creation | 162.4 → 161.2 ns | 1,024 B, unchanged |
| Common-type field creation | 136.4 → 135.5 ns | 1,024 B, unchanged |
| Reflected object arguments | 491.8 → 506.0 ns | 2.37 KiB, unchanged |

All timing intervals overlap. FIFO intentionally trades re-creation after eviction
for bounded retention: schemas cycling through more than 4,096 custom names, or
names longer than 256 characters, can allocate more strings than an unbounded
cache. Cold-miss contention was not claimed improved. Values and public signatures
are unchanged; permanent string-reference identity is not a public contract.

Artifacts: `/tmp/ngql-t4-before`, `/tmp/ngql-t4-after`; benchmarks:
`TypeCacheBenchmark`, `AllocationHotspotBenchmark.ObjectArguments`.
`CacheRetentionTests` reports retained names and collectible-type reachability
after forced collection; these are retention checks, not RSS measurements.

### T5 — Pool retention

The builder pool now applies its 262,144-character allowance to the sum of retained
builders on a thread, not just each individual builder. It still retains up to four
builders when their combined capacity fits; oversized/full/budget-exceeding returns
are rejected before `Clear`. The common empty-stack return avoids the capacity scan.

High-water tests borrowed four 200,000-character builders per thread. Before:
800,000 characters retained per thread, on both the one-thread and four-thread
runs. After: 200,000 per thread, a 75% reduction. Across four threads this is
6.4 MB → 1.6 MB of character payload (decimal bytes, excluding object overhead).
The configured worst-case retained payload is now 512 KiB/thread instead of 2 MiB.
Four 32,768-character builders are still reused without replacement. All pool and
renderer tests pass across .NET 8/9/10.

| Repeated workload | Before → after | Allocations |
| --- | ---: | ---: |
| Small UTF-8 output | 51.61 → 51.56 ns | 0 B, unchanged |
| 1,024-character argument | 414.7 → 438.5 ns | 128 B, unchanged |
| 32,768-character argument | 11.32 → 11.27 µs | 128 B, unchanged |
| 131,072-character argument | 45.10 → 47.48 µs | 128 B, unchanged |

Timing intervals overlap; this is a retained-capacity improvement, not a CPU
speedup claim. Repeated reentrant bursts exceeding the aggregate budget may
reallocate discarded large builders; ordinary sequential medium renders retain
their builder. The measurements do not imply every application saves 75% RSS.
Artifacts: `/tmp/ngql-t5-before`, `/tmp/ngql-t5-after`; benchmarks:
`MediumRenderBenchmark`, `AllocationHotspotBenchmark.SmallUtf8`; retention tests:
`BuilderPoolBudgetTests` (one/four threads and normal-size reuse).

### T6 — Existing optimizations and final verification

The final sweep exposed a 64-byte comparison adapter allocated by comparer-based
array sorting. Cached comparison delegates passed directly to span sorting remove
it from root/nested field sorting and argument sorting without changing ordering.
New warmed allocation regressions assert zero allocated bytes for sorted root and
nested writer calls on .NET 8/9/10. Snapshot and invalidation guards are unchanged.
The obsolete production argument-dictionary helper was removed; differential tests
retain the former algorithm as their independent reference implementation.

| Workload | Before → after | Bytes before → after |
| --- | ---: | ---: |
| Write 10 root fields | 184.33 → 186.56 ns | 64 → 0 |
| Render 10 root fields to string | 221.66 → 191.80 ns | 328 → 264 |
| Write 1,000 root fields | 75.06 → 73.00 µs | 65 → 1 |
| UTF-8 guardrail | 5.23 → 4.92 µs | 64 → 0 |
| Cached path | 16.35 → 16.15 ns | 0 → 0 |
| Preserve selected paths | 2.57 → 2.62 µs | 4,448 → 4,448 |
| Merge 100 fragments | 55.67 → 46.28 µs | 144,824 → 144,824 |
| Build 100 paths | 16.66 → 17.32 µs | 47,008 → 47,008 |

These are paired T6 measurements, not comparisons to the initial branch baseline.
The 1 B result is the benchmark's amortized allocation reading, not a one-byte
object. Timing intervals overlap for most controls; no broad CPU speedup is
claimed. The initially slower unchanged path-building control was repeated alone
with five warmups/eight iterations: 15.88 ± 0.144 µs, still 47,008 B. Its apparent
4% regression did not reproduce; host/run variation prevents attributing a path
speedup or slowdown to the sorting change.

Final 1,000-entry scalar/variable argument renders measured 59.18/58.83 µs and
36,184/70,296 B. Compared with T1's original 67,289/101,402 B, this saves about
31 KB per render. Do not infer a CPU ratio across those runs: unchanged singleton
timings also shifted substantially between T1 and T6.

Artifacts: `/tmp/ngql-t6-before`, `/tmp/ngql-t6-after`,
`/tmp/ngql-t6-path-repeat`. BenchmarkDotNet 0.15.7, .NET 9.0.9, Apple M4;
three warmups/five iterations, in-process, unless noted above. Benchmarks were
run sequentially, without concurrent test runs.

```sh
dotnet run --project tests/BenchmarkRunner -c Release -f net9.0 -p:NuGetAudit=false -- --filter '*GuardrailBenchmark*' '*RootSelectionBenchmark*' '*BlockArgumentRenderingBenchmark*' --job short --warmupCount 3 --iterationCount 5 --inProcess --artifacts /tmp/ngql-final-check
dotnet test tests/Core.Tests/Core.Tests.csproj -c Release --no-restore -p:NuGetAudit=false
dotnet test tests/Core.IntegrationTests/Core.IntegrationTests.csproj -c Release --no-restore -p:NuGetAudit=false
```

Final validation: 2,101 unit tests and 91 integration tests pass on each of .NET
8, 9 and 10. Public method signatures and legacy reflection-based `Include` are
unchanged. The command-local audit override addresses the existing dependency
audit failure; no dependency versions or repository audit settings were changed.

## Released-package comparison — 2026-09-17

Ran the complete `VersionComparisonBenchmark` against working-copy commit
`815ea8b` and **NGql.Core 2.1.0**, the latest stable release verified against the
[live NuGet version index](https://api.nuget.org/v3-flatcontainer/ngql.core/index.json).
Newer previews were excluded. Both runners compile the same linked benchmark
source on .NET 9.0.9 / Apple M4 / SDK 10.0.102 / BenchmarkDotNet 0.15.7.

The primary comparison uses three warmups/five measured iterations in-process.
The benchmark's built-in configuration also adds an out-of-process ShortRun with
three measured iterations. All 24 shared cases completed in both modes; all six
local-only cases also completed in both modes. Runs were sequential. The first
local ShortRun build failed on the existing NU1902 audit error; rerunning with
process-local `NuGetAudit=false` allowed generated child builds to complete.
No library source, dependencies or repository audit settings were changed.

Primary results (time and managed allocation per benchmark invocation):

| Scenario | Released → local time | Released → local allocation | Time change | Allocation change |
| --- | ---: | ---: | ---: | ---: |
| Simple query | 393.4 → 375.2 ns | 1.56 → 1.72 KiB | −4.6% | +10.3% |
| Complex query with merging | 1.445 → 1.520 µs | 6.23 → 6.91 KiB | +5.2% | +10.9% |
| Classic nesting, depth 10 | 6.538 → 3.019 µs | 53.18 → 12.96 KiB | −53.8% | −75.6% |
| Classic nesting, depth 30 | 38.468 → 9.694 µs | 669.27 → 54.99 KiB | −74.8% | −91.8% |
| Dictionary arguments, 50 repetitions | 60.187 → 50.339 µs | 203.91 → 191.80 KiB | −16.4% | −5.9% |
| Expression preservation, 50 repetitions | 55.176 → 51.534 µs | 176.64 → 148.40 KiB | −6.6% | −16.0% |
| ToString, 50 repetitions | 16.061 → 12.361 µs | 41.03 → 32.27 KiB | −23.0% | −21.4% |
| ToString + UTF-8, 50 repetitions | 17.475 → 13.774 µs | 55.88 → 47.11 KiB | −21.2% | −15.7% |
| Bulk building, 100 queries | 58.711 → 57.108 µs | 220.31 → 246.09 KiB | −2.7% | +11.7% |
| Dotted paths, 200 fields | 73.177 → 74.144 µs | 231.43 → 270.45 KiB | +1.3% | +16.9% |
| Flat selection, 500 fields | 64.520 → 64.978 µs | 126.42 → 149.81 KiB | +0.7% | +18.5% |

This is **not a universal reduction against the release**. The cross-check
reproduces the builder-heavy allocation increases and the large nesting/rendering
savings. Complex merging averages 8.8% slower out-of-process too, although its
ShortRun timing intervals overlap (the primary intervals do not). Simple-query,
large-selection and 50-repetition preservation timing intervals overlap in the
primary run; do not treat those small timing differences as proven improvements
or regressions. Allocation percentages use rounded CSV values. These are
allocation measurements, not retained-heap/RSS measurements or total CPU usage.

The construction allocation increases and complex-merge timing are next profiling
targets; this comparison does not isolate their cause or implement new fixes.
The six previously completed tasks remain scoped findings, not a claim that all
release-relative workloads are faster or smaller.

Full 24-case tables for both modes, uncertainty checks, six local-only results,
commands and raw reports are under
`artifacts/benchmarks/version-815ea8b/` (git-ignored), starting with
[comparison.md](../artifacts/benchmarks/version-815ea8b/comparison.md).
The published runner's compile guard excludes the six local-only cases; they are
not included in release-relative ratios. High-priority scheduling was unavailable
in both runners. Repeated randomized A/B runs would better isolate small timing
differences from host/run variation.

## Complex merging profile — 2026-09-17

Profiled `VersionComparisonBenchmark.ComplexQueryWithMerging` against the actual
NGql.Core 2.1.0 package (repository commit `58c697d`) and working copy `815ea8b`.
The operation builds two four-field fragments, creates a destination, includes
both fragments, and renders the result. It measures construction and rendering
as well as merging. No production implementation was changed in this pass.

### Exact allocation by phase

A standalone .NET 9.0.9 Release harness brackets each phase with
`GC.GetAllocatedBytesForCurrentThread`. Totals storage is outside the operation;
measured instrumentation allocation outside the brackets is zero. After at least
two seconds of warmup, 100,000 operations produce the following bytes/op. Both
process runs reproduce the phase totals (sub-byte runtime noise rounded away).

| Phase | 2.1.0 bytes | Local bytes | Difference |
| --- | ---: | ---: | ---: |
| Build both fragments | 2,904 | 3,416 | +512 |
| Create destination root | 184 | 200 | +16 |
| First Include | 1,784 | 1,976 | +192 |
| Second Include | 944 | 1,088 | +144 |
| Render | 544 | 352 | −192 |
| Total instrumented operation | 6,360 | 7,032 | +672 |

Construction/inclusion adds **864 B/op**, partially offset by **192 B/op** saved
in rendering. About 61% of the gross increase occurs before either Include call.
This is principally a builder/state allocation investigation, not evidence that
the merge-candidate search itself regressed.

The benchmark uses `MergeByDefault`, which does not consult the fingerprint
candidate index (`QueryMerger.ApplyFieldMerge`). Raw instance-size probes using
`RuntimeHelpers.GetUninitializedObject` measure `FieldDefinition` at 96 → 144 B,
`QueryDefinition` at 64 → 80 B, `FieldBuilder` at 24 → 40 B, and `QueryBuilder`
unchanged at 40 B. A local `MergeMemoTracker` occupies 40 B. These sizes exclude
referenced objects and constructor work. The first Include clones four fields;
the second adds three beneath the shared user root. Their 192 B and 144 B
increases match **seven fields × 48 B**, respectively. Destination-root growth
matches the extra 16 B in `QueryDefinition`.

Field growth includes storage for directives, fragment spreads, deep argument
fingerprints, and tracker references, even when those features are unused.
Therefore, the release-relative increase is not solely the cost of the recent
scoped-invalidation fix. Fragment construction also creates tracking state via
discarded temporary builders; that phase has other differences and is not fully
reconciled object-by-object here.

The uninstrumented custom harness measures 6,360 → 7,072 B/op. The local 40-byte
difference from the phase-instrumented method persists after longer warmup; a
control with tiered compilation disabled measures 7,032 B/op in both methods.
This is consistent with JIT/code-shape-dependent allocation, but its exact
elimination/materialization site has not been established. Do not present phase
totals as an exact decomposition of the original BenchmarkDotNet totals: those
were about 6.23 → 6.91 KiB/op, and the harness changes optimization context.

### Timing cross-check

Each cell below is the median of seven batches of 100,000 operations. Each mode
warms for at least two seconds. Runs are sequential, first local then published,
then published followed by local to reverse order. These diagnostic loops do not
replace BenchmarkDotNet confidence intervals.

| Workload | First pair, release → local (ns/op) | Reverse-order repeat (ns/op) |
| --- | ---: | ---: |
| Full original operation | 1,333.5 → 1,382.0 | 1,329.0 → 1,396.2 |
| Build both fragments only | 610.9 → 659.0 | 605.1 → 660.1 |
| Fresh root + Includes, prepared fragments | 419.3 → 443.7 | 423.7 → 454.0 |
| Render a prepared merged query | 198.6 → 160.0 | 199.2 → 164.1 |

Full-operation slowdown reproduces at 3.6–5.1%. Both construction and inclusion
are slower in these paired runs; rendering is faster. Prepared-fragment Include
avoids first-use setup on the source fragments, so isolated times/allocations
must not be summed to reconstruct a full operation.

### Trace evidence and next target

Captured separate 15-second EventPipe traces with runtime GC allocation ticks
and SampleProfiler. The local trace contains `MergeMemoTracker` allocations;
the release trace does not. Both allocate heavily through field creation.
The released renderer allocates `Comparison<FieldDefinition>` delegates, absent
from the local trace, consistent with the measured rendering savings.

Allocation-tick weights are estimates, not object counts or precise type totals.
SampleProfiler records thread stacks, including the harness attachment sleep;
its samples concentrate in `FieldChildren.Append` in both versions. These traces
do **not** establish exclusive CPU percentages or identify a single CPU cause
for the regression. Exact phase allocation and repeated untraced timing provide
the stronger comparison.

Source comparison identifies larger field/query state and tracker creation in
the temporary `FieldBuilder` path as optimization candidates. In particular,
dotted `AddField` calls create a `FieldBuilder` whose return value is discarded,
but its constructor still ensures a merge-memo tracker. Investigate bypassing
that temporary builder/tracker for this path while preserving invalidation for
captured builders, shared roots and argument-bearing merges. Measure that narrow
change before considering a broader field-layout redesign. Do not remove scoped
invalidation wholesale: its benefit for unrelated-query mutation was measured
in T2 above.

Validation: full and instrumented queries have identical text in both versions
(SHA256 `2F440BAEC37FE5A82EDC1090719C73F7868C0CEFFB1680EF896F1DB16BC3AAB6`).
Repeated prepared-fragment Includes preserve both source fragments and reproduce
the full-operation output. No production/test code changed, so the full test
suite was not rerun. The first sandboxed builds stalled and were stopped; final
timing runs were sequential and separate from trace collection.

Harness, commands, exact logs and raw traces are in the git-ignored directory
[`artifacts/benchmarks/complex-merge-profile/`](../artifacts/benchmarks/complex-merge-profile/README.md).
`local-warm.txt`, `published-warm.txt`, `local-repeat.txt`, and
`published-repeat.txt` contain the final timing/allocation runs. Trace reports
are `local-trace.txt` and `published-trace.txt`; their original "CPU samples"
label should be read as thread-stack samples. Preserved collector source under
`trace-tool/` corrects that label.

## Complex merging construction fix — 2026-09-18

Implemented the narrow follow-up in `QueryBuilder.AddFieldFastPath`: non-simple
argument-free field paths now call `FieldFactory.GetOrAddField` directly. The
discarded `FieldBuilder` previously created a merge tracker despite never being
returned to a caller. Parsing still uses the same factory; lookup-cache
invalidation remains in place. Actual captured builders and merge indexes still
create/attach trackers. Field layout, public signatures and the legacy reflection
Include path are unchanged by this patch.

Preserved pre-fix `815ea8b`, fixed, and released 2.1.0 assemblies in separate
directories. The fixed assembly's SHA256 matches the assembly used by the final
unit run. BenchmarkDotNet 0.15.7 / .NET 9.0.9 / Apple M4, five warmups and eight
measured iterations; all workloads ran sequentially without concurrent tests or
profilers. Four wrapper benchmarks call the existing shared workload methods.
The wrappers avoid the shared class's built-in job configuration, so these runs
have exactly one requested job. Priority elevation was unavailable. The isolated
harness build emitted the existing BenchmarkDotNet analyzer AD0001 language-version
warnings; all requested benchmarks built and completed.

### Before/fixed/released comparison

In-process means and bytes per operation (bytes derived from raw GC totals,
rounded to the nearest byte rather than from the rounded KiB display):

| Workload | Before time | Fixed time | 2.1.0 time | Bytes before → fixed → 2.1.0 |
| --- | ---: | ---: | ---: | ---: |
| Complex merging | 1,455.9 ns | 1,448.2 ns | 1,429.3 ns | 7,072 → 6,872 → 6,360 |
| Simple query with dotted fields | 353.2 ns | 330.8 ns | 357.7 ns | 1,760 → 1,640 → 1,600 |
| 200 dotted paths | 70.572 µs | 67.413 µs | 71.720 µs | 276,936 → 260,896 → 236,944 |
| 500 flat fields, unchanged-path control | 64.275 µs | 64.006 µs | 63.435 µs | 153,400 → 153,400 → 129,448 |

Complex merging saves **200 B/op (2.8%)**. Its in-process timing intervals overlap
(before ±7.92 ns, fixed ±24.46 ns), so that run does not establish a speedup.
Simple-query time decreases 6.3% and allocation 6.8%; dotted-path time decreases
4.5% and allocation 5.8%, with non-overlapping timing intervals in these runs.
The flat control retains allocations and has overlapping timing intervals.

The phase harness localizes the saving to construction: each fragment saves
80 B, while root creation, both Includes and rendering retain their prior phase
allocations. Instrumented and uninstrumented fixed operations now both measure
6,872 B/op; the former local 40 B difference between those methods disappears.
Output SHA256 is unchanged and prepared-fragment source-immutability checks pass.

A separate-process complex-merge cross-check, run in fixed/before/released order,
measures **1.373 ±0.007 µs / 1.395 ±0.006 µs / 1.333 ±0.011 µs** (99.9% interval
half-widths). The fixed build is 1.6% faster than before in that run; both modes
reproduce 7,072 → 6,872 B/op. This is a small construction improvement, not a
claim that the broader release-relative regression is eliminated. The fixed
workload still allocates **512 B/op (8.1%) more than 2.1.0**, and the separate-
process run remains about 3% slower than the release. Larger field/query state,
including the previously measured 48 B per cloned field, remains unchanged.
Original suite and wrapper allocation figures differ slightly with JIT context;
use the paired values in this section for this patch.

### Correctness and reproduction

All **2,104 unit tests and 91 integration tests pass on each of .NET 8, 9 and 10**.
Three new cases initialize a plain dotted/typed/aliased path, later capture a
nested builder, warm the merge index, mutate an argument, and verify a matching
Include still merges into one root. The initial test setup incorrectly relied
on detached-builder mutation updating the query; it was corrected before the
final successful run. The 2,101 existing cases passed in both runs.

Raw reports, preserved binaries, wrapper source and TRX results are under
[`artifacts/benchmarks/complex-merge-fix/`](../artifacts/benchmarks/complex-merge-fix/README.md).
The focused phase/output harness result is `after-profile.txt`. The source change
is limited to the non-simple branch of `AddFieldFastPath`; no object-layout
redesign or merge-index invalidation rollback was introduced.

```sh
dotnet test tests/Core.Tests/Core.Tests.csproj -c Release --no-restore -p:NuGetAudit=false
dotnet test tests/Core.IntegrationTests/Core.IntegrationTests.csproj -c Release --no-restore -p:NuGetAudit=false
dotnet run --project artifacts/benchmarks/complex-merge-fix/bdn -c Release -p:ProfileVersion=after -p:NuGetAudit=false -- --filter '*ComplexMergeFixBenchmark*' --job short --inProcess --warmupCount 5 --iterationCount 8 --artifacts artifacts/benchmarks/complex-merge-fix/bdn-after
ProfileVersion=after NuGetAudit=false dotnet run --project artifacts/benchmarks/complex-merge-fix/bdn -c Release -- --filter '*ComplexMergeFixBenchmark.ComplexQueryWithMerging' --job short --warmupCount 5 --iterationCount 8 --artifacts artifacts/benchmarks/complex-merge-fix/oop-after
```

Substitute `before` or `published` for paired runs. `ProfileVersion` is passed in
the environment for separate-process jobs so generated child builds use the
same assembly. The audit override remains command-local.

## Remaining profile-dependent opportunities and tradeoffs

All six findings are now measured and resolved in [PERFORMANCE_TASKS.md](PERFORMANCE_TASKS.md).
The remaining considerations are workload tradeoffs, not unimplemented fixes:

- **Root arguments (T1):** pooled sorting removes the temporary dictionary;
  returned strings and boxed variables still allocate.
- **Merge invalidation (T2):** unrelated queries no longer invalidate each other;
  root trackers add construction/storage overhead in exchange for avoiding rebuilds.
- **Root selection (T3):** the accepted reference-span shortcut improves singleton
  dispatch; the slower collection-expression experiment was discarded.
- **Type caches (T4):** custom names have bounded FIFO retention and collectible
  metadata can unload. Cache churn can allocate more; metadata for permanently
  live types remains cached. Cold-miss contention is not claimed improved.
- **Pool retention (T5):** the aggregate per-thread character budget is bounded;
  repeated large reentrant bursts can reallocate discarded buffers. Measurements
  quantify retained character capacity, not application RSS.
- **Existing optimizations (T6):** sorting adapters are eliminated; traversal,
  merge, path-cache and sink controls retain their correctness guards. Application
  profiles are still needed before claiming end-to-end throughput improvements.
