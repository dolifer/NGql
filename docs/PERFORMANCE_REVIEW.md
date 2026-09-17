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

The integer path is slower because it now guarantees GraphQL's ASCII minus sign
under cultures with a custom negative sign. It creates no additional managed
allocations. Root-variable lookup is now logarithmic per variable instead of
scanning every argument value. Preservation probes only dotted ancestors; .NET 9
and later use allocation-free alternate hash-set lookups. The .NET 8 fallback
measured 66.5 µs and 208,368 bytes for 1,000 paths, versus 481.0 µs and 97,184
bytes before; its temporary prefix strings trade memory for a large CPU reduction.

## Further opportunities and tradeoffs

These are source-level findings, not measured improvements in this change:

- **Root argument rendering:** the classic `QueryBlock` path still creates a
  `SortedDictionary` on every render. A direct merge of its already-sorted
  arguments and variables could remove the remaining tree-node allocations.
- **Merge-index invalidation:** a static, process-wide merge-memo epoch invalidates
  fingerprint buckets in unrelated query definitions. Per-definition versioning
  could reduce cross-query cache churn and atomic traffic under concurrency.
- **Argument reflection:** variable extraction and argument normalization call
  `Type.GetProperties()` separately for arbitrary objects. Sharing cached metadata
  would reduce reflection work without changing the legacy inclusion API.
- **Root selection rendering:** root dictionaries containing one field still rent,
  populate, clear and return a sort buffer. A direct single-entry path can mirror
  the nested-field optimization already implemented.
- **Type caches:** `TypeCache.CustomTypes` and the reflection metadata dictionaries
  have no eviction. Custom type lookups also materialize a string before checking
  the cache. Stable schemas bound this naturally; dynamic schemas or collectible
  types warrant a retained-heap profile before choosing limits or weak references.
- **Pool retention:** rendering can retain four builders per active thread, each
  up to roughly 512 KiB of character storage. Lower limits could reduce idle memory
  but increase allocations for medium-sized queries. The hash-set pool validates
  `Count`, which does not bound backing capacity after removals or a caller clear.
  Measure high-water capacity and thread counts before retuning these pools.
- **Existing optimizations:** field children already use spans and a lazy index;
  merge candidates use name/fingerprint buckets; path lookups are cached; sinks
  avoid a final string allocation. Keep their invalidation and concurrency guards:
  removing them for speed risks stale merges or dropped fields.
