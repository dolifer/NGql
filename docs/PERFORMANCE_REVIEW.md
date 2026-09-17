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

## Remaining profile-dependent opportunities and tradeoffs

These are source-level findings, not measured improvements in this change:

- **Root argument rendering:** tree-node allocations are removed. The temporary
  hash dictionary remains; removing it needs a merge that preserves ordinal order,
  case-distinct variable names, remapped argument keys and collision precedence.
- **Merge-index invalidation:** a static, process-wide merge-memo epoch invalidates
  fingerprint buckets in unrelated query definitions. Per-definition versioning
  could reduce cross-query cache churn and atomic traffic under concurrency.
- **Root selection rendering:** the direct single-entry experiment above did not
  establish a benefit and was reverted.
- **Type caches:** `TypeCache.CustomTypes` and the reflection metadata dictionaries
  have no eviction. Span lookups now avoid temporary custom-type strings on .NET
  9/10. Stable schemas bound cache growth naturally; dynamic schemas or collectible
  types warrant a retained-heap profile before choosing limits or weak references.
- **Pool retention:** rendering can retain four builders per active thread, each
  up to roughly 512 KiB of character storage. Lower limits could reduce idle memory
  but increase allocations for medium-sized queries. The hash-set pool now bounds
  capacity as well as count. Measure builder high-water capacity and thread counts
  before retuning its limits.
- **Existing optimizations:** field children already use spans and a lazy index;
  merge candidates use name/fingerprint buckets; path lookups are cached; sinks
  avoid a final string allocation. Keep their invalidation and concurrency guards:
  removing them for speed risks stale merges or dropped fields.
