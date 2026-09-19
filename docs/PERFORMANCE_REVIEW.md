# NGql.Core performance — final state

Branch `perf/reduce-render-allocations` at `91bf481`, compared with the released
**NGql.Core 2.1.0** package and with the branch baseline `9502ece`. Public method
signatures, rendered output and the legacy reflection-based `Include` path are
unchanged. The pass-by-pass history, rejected experiments and rerun tables are
in git history up to `91bf481`.

## How to read the numbers

- BenchmarkDotNet 0.15.7, .NET 9.0.9, Apple M4, macOS 27.0, Release, in-process
  ShortRun. "Bytes" are managed bytes allocated per benchmark operation, not
  retained heap or RSS.
- **Allocation figures were measured on the final build** (`91bf481`, assembly
  SHA256 `9ba32de6…`) on 2026-09-19 and are deterministic.
- **Timing figures come from earlier quiet, ordered (A/B/B/A) runs** and are
  labeled with the build they were taken on. The final-build timing rerun ran
  under host load (load average 13; the same assembly differed by up to 25%
  between runs), so it is not used for timing claims.
- These are microbenchmarks on one machine. They do not establish application
  throughput or process memory.

## Against NGql.Core 2.1.0

Same linked benchmark source for both. Every shared case allocates less than the
release; none allocates more.

| Workload | 2.1.0 bytes | Final bytes | Change |
| --- | ---: | ---: | ---: |
| Simple query | 1,597 | 1,413 | −11.5% |
| Type drift scenario | 2,130 | 1,782 | −16.3% |
| Type drift edge cases | 1,915 | 1,403 | −26.7% |
| Case-insensitive fields | 1,813 | 1,597 | −11.9% |
| Array type preservation | 1,843 | 1,434 | −22.2% |
| Complex query with merging | 6,359 | 4,721 | −25.8% |
| Deep nested fields | 5,857 | 5,581 | −4.7% |
| Arguments pool stress | 40,366 | 37,724 | −6.5% |
| Bulk building, 10 queries | 22,559 | 20,398 | −9.6% |
| Bulk building, 100 queries | 225,597 | 204,001 | −9.6% |
| Dotted paths, 50 fields | 58,593 | 57,262 | −2.3% |
| Dotted paths, 200 fields | 236,984 | 232,059 | −2.1% |
| Flat selection, 100 fields | 26,747 | 26,573 | −0.7% |
| Flat selection, 500 fields | 129,454 | 129,280 | −0.1% |
| Classic nesting, depth 10 | 54,456 | 13,363 | −75.5% |
| Classic nesting, depth 30 | 685,333 | 56,556 | −91.7% |
| Dictionary arguments ×10 | 41,759 | 37,202 | −10.9% |
| Dictionary arguments ×50 | 208,804 | 185,999 | −10.9% |
| Expression preservation ×10 | 37,489 | 30,720 | −18.1% |
| Expression preservation ×50 | 180,849 | 147,528 | −18.4% |
| ToString ×10 | 11,295 | 9,155 | −18.9% |
| ToString ×50 | 42,015 | 32,195 | −23.4% |
| ToString + UTF-8 ×10 | 14,336 | 12,196 | −14.9% |
| ToString + UTF-8 ×50 | 57,221 | 47,391 | −17.2% |

Timing against 2.1.0 (ordered 2.1.0 / local / local / 2.1.0 runs, ten
iterations, intervals below ±18 ns and not overlapping, taken at `feb2e75`):

| Workload | 2.1.0 | Local | Change |
| --- | ---: | ---: | ---: |
| Complex query with merging | 1,462 / 1,456 ns | 1,217 / 1,203 ns | about 17% faster |
| Simple query | 365 / 360 ns | 321 / 319 ns | about 12% faster |
| Classic nesting, depth 10 (at `815ea8b`) | 6,538 ns | 3,019 ns | −54% |
| Classic nesting, depth 30 (at `815ea8b`) | 38,468 ns | 9,694 ns | −75% |

Retained managed heap per held complex-merge workload (10,000 held, compacting
full GC, three identical repeats, final build):

| Held graph | 2.1.0 | Final |
| --- | ---: | ---: |
| Merged builder only | 3,008 B | 2,352 B |
| Both fragments and merged builder | 5,752 B | 4,368 B |

## Features and their final metrics

"Before" is the value measured on the commit that preceded the feature;
"final" is the final build unless a build is named.

### Rendering

| Feature | Metric | Before | Final |
| --- | --- | ---: | ---: |
| Scalars, decimals and dates format into a stack buffer (invariant culture) | Five scalar arguments, bytes | 928 | 536 |
| Oversized builders are rejected before `Clear` | 300,000-character argument, bytes | 1,234,128 | 617,561 |
| Single-child selections render without a sort buffer | Sixteen-level chain, time (at `dd27676`) | 971.5 ns | 357.4 ns |
| Sorting uses cached comparisons, no adapter objects | Write 10 sorted root fields, bytes | 64 | 0 |
| Root arguments sort pooled entries instead of a dictionary | Render 1,000 arguments, bytes | 92,497 | 36,024 |
| | Render 1,000 variables, bytes | 126,610 | 70,249 |
| | Render 10 arguments / variables, bytes | 1,408 / 1,672 | 472 / 848 |
| A lone argument or variable renders without an enumerator | Render 1 argument / variable, bytes | 552 / 480 | 80 / 88 |
| | Render 1 argument, time (at `fa3d8f5`) | 63.0 ns | 44.0 ns |
| | Warm single-entry write to a sink, bytes | — | 0 |
| Single root selection renders from a reference span | One-field write, time (at `815ea8b`) | 44.5 ns | 32.8 ns |
| Single-chunk UTF-8 output skips the encoder | Small UTF-8 query, bytes | 48 | 0 |
| | Large UTF-8 query, bytes | 176 | 128 |

### Arguments and variables

| Feature | Metric | Before | Final |
| --- | --- | ---: | ---: |
| New argument keys skip the key-casing scan | Insert 1,000 arguments individually, bytes | 240,108 | 56,275 |
| | Same, time (at `49c7bc0`) | 4,173 µs | 387 µs |
| | Insert 10 individually / as a batch, bytes | 1,710 / 2,447 | 832 / 1,872 |
| | Insert a batch of 1,000, bytes | 201,564 | 145,545 |
| Root-variable lookup is logarithmic | 1,000 root variables, bytes | 294,245 | 54,040 |
| | Same, time (at `b837e1f`) | 4,435 µs | 374 µs |
| List normalization reserves capacity | 10 / 1,000 items, bytes | 624 / 16,896 | 432 / 8,352 |
| Property metadata is shared across extraction, normalization, comparison and rendering | Object-argument construction, bytes | 2,488 | 2,216 |
| Empty containers skip cycle tracking | `QueryBlock` empty list / dictionary, bytes | 528 / 608 | 360 / 440 |
| Alias suffix candidates are tested as spans (.NET 9+) | Alias with 10 / 1,000 occupied names, bytes | 496 / 113,264 | 104 / 73,272 |

### Construction and field layout

| Feature | Metric | Before | Final |
| --- | --- | ---: | ---: |
| `FieldDefinition` is 96 B: one-byte memo caches, optional state (fragments, spreads, directives, metadata) behind one reference, computed effective name | Instance size, bytes | 144 | 96 (as in 2.1.0) |
| Paths that expose no builder call the factory directly (plain, argument-bearing and sub-field overloads) | Complex merging, bytes | 7,072 | 4,721 |
| | 200 dotted paths, bytes | 276,936 | 232,059 |
| | 500 flat fields, bytes | 153,400 | 129,280 |
| Merge clones reuse name, alias and path strings | Merge 100 fragments, bytes | 144,824 | 130,393 |
| The path index is created on first `GetPathTo` | Simple query, bytes | 1,760 | 1,413 |
| Optional-state guardrails (cold builds) | Directives / inline fragments / named fragments / metadata, bytes | 4,328 / 4,728 / 2,976 / 2,464 | 4,200 / 4,360 / 2,752 / 2,304 |

### Merging

| Feature | Metric | Before | Final |
| --- | --- | ---: | ---: |
| Merge-index invalidation is scoped to the queries that observe a field, not process-wide | Merge after an unrelated mutation, 1,000 candidates (at `815ea8b`) | 81,608 ns, 268,728 B | 290 ns, 296 B |
| | Warm merge, 1,000 candidates, bytes | 272 | 272 |
| Captured builders keep the index correct: `Where`, `IncludeIf`/`SkipIf` and argument-bearing `AddField` on a builder captured from a nested action, a dotted path or after a sub-field overload | Regression cases confirmed to produce two definitions instead of one | 6 failing | 0 failing |

### Preservation

| Feature | Metric | Before | Final |
| --- | --- | ---: | ---: |
| Ancestor pruning probes dotted prefixes instead of scanning all paths | 1,000 unrelated paths, bytes | 97,188 | 73,192 |
| | Same, time (at `b837e1f`) | 501 µs | 40 µs |
| .NET 8 hash prefilter avoids prefix strings | 512 deep paths after a short path, extra bytes (.NET 8) | 114,576 | under 16,384 |

### Caches, pools and retention

| Feature | Metric | Before | Final |
| --- | --- | ---: | ---: |
| Builder pool budget applies to the sum of pooled builders per thread (262,144 characters) | Characters retained after four 200,000-character builders | 800,000 | 200,000 |
| Over budget, the pool keeps the largest builders that fit, largest on top | Builder retained after an 80,000 + 200,000 nested render | first returned | the 200,000 one, either order |
| Medium renders still reuse their builder | 1,024 to 131,072-character argument, bytes | 128 | 128 |
| Custom type names: two lock-free generations of 2,048, names in use are promoted | Names retained after 20,000 unique lookups | 20,000 | at most about 4,096 |
| | Churn of 6,000 names, 1 / 4 threads (at `34fece7`) | 2.564 / 5.223 ms | 2.266 / 4.440 ms |
| | Name kept in use across 20,000 others | evicted | same instance |
| | Cached custom-type field creation (at `34fece7`) | 158.1 ns, 896 B | 158.0 ns, 896 B |
| Reflection metadata caches hold types weakly | Collectible types rooted by the three caches | 3 | 0 |
| Pooled hash sets above 256 slots are not returned | Backing slots of the next rental after growing past 2,000 | over 2,000 | at most 256 |

### Robustness fixes made along the way

- Signed integers render with the invariant ASCII minus sign under any culture.
- `WriteUtf8` accepts buffer writers that return spans shorter than requested.
- Optional field state is replaced with compare-and-swap, so a `Metadata` read
  racing a directive, spread or fragment addition loses neither.
- Names longer than 256 characters are never cached; alias formatting bounds
  stack use at 512 characters.

## Known costs

- **Feature-bearing fields pay a 48 B holder.** Cold builds of fields with
  metadata are about 12% slower and with inline fragments about 6% slower than
  before the 96 B layout (ordered runs at `feb2e75`), plus 1–3% for atomic
  replacement. Their allocation is still below the pre-change values above. A
  schema where most fields carry metadata benefits less.
- **Scalar-argument rendering measured about 2.7% slower** (roughly 16 ns,
  unchanged 536 B) after the memo-cache compaction in four ordered runs. The
  renderer reads none of the changed fields; the cause is not established.
- **Invariant integer formatting is slower:** 1,000 signed integers 10.7 → 14.7 µs
  with unchanged 12,032 B. This buys culture-independent output.
- **`QueryBlock` gained one reference field** for the single-argument fast path:
  empty-container blocks allocate 8 B more than their best intermediate value
  (360 / 440 B against 352 / 432 B), still below the 528 / 608 B baseline.
- **Builders handed to user code keep an eager merge tracker** (40 B). It is what
  lets a captured builder invalidate a merge index later; only builders that
  never reach user code skip it.
- **Type unloading costs a `ConditionalWeakTable` lookup** for reflected object
  arguments; its measured difference was within noise (491.8 → 506.0 ns,
  overlapping intervals).
- **Large reentrant render bursts can reallocate.** When nested builders exceed
  the 262,144-character budget together, the smaller one is rebuilt per render.
- **A dotted `AddField` with an action resolves its ancestor chain** with one
  child lookup per path segment (a tree search only for aliased segments that do
  not resolve by name). This is not benchmarked separately.

## Validation

**2,313 unit tests and 92 integration tests pass on each of .NET 8, 9 and 10, with
100% line, branch and method coverage** across NGql.Core, the `ngql` tool, the demo
server and Shared (4,377 lines, 2,886 branches, 888 methods; `make coverage`; the tool
adds 73 tests per framework). Reaching it removed unreachable guards, the unused
`LockFreeArgumentsPool`, and key-generator and pool overloads that only tests called;
allocation figures above were re-checked afterwards and are unchanged.
Rendered output for the complex-merge workload is byte-identical to 2.1.0
(SHA256 `2F440BAE…3AAB6`). Regression coverage added on this branch includes
randomized differential tests against the former argument-ordering algorithm,
warmed zero-allocation assertions for sorted and single-entry rendering, cache
and pool retention tests with forced collection, concurrent memoization and
optional-state races, and the captured-builder merge-index cases.

## Reproduce

```sh
dotnet test tests/Core.Tests/Core.Tests.csproj -c Release -p:NuGetAudit=false
dotnet test tests/Core.IntegrationTests/Core.IntegrationTests.csproj -c Release -p:NuGetAudit=false

# Release comparison: preserve each NGql.Core.dll under complex-merge-opt/<name>/ first
dotnet run --project artifacts/benchmarks/complex-merge-opt/bdn -c Release -p:ProfileVersion=published -- --filter '*SharedWorkloadBenchmark*' --job short --inProcess --warmupCount 3 --iterationCount 5 --iterationTime 200 --artifacts artifacts/benchmarks/complex-merge-opt/bdn-final-published
dotnet run --project artifacts/benchmarks/complex-merge-opt/bdn -c Release -p:ProfileVersion=final -- --filter '*' --job short --inProcess --warmupCount 3 --iterationCount 5 --iterationTime 200 --artifacts artifacts/benchmarks/complex-merge-opt/bdn-final
python3 artifacts/benchmarks/complex-merge-opt/compare.py bdn-final-published bdn-final
dotnet run --project artifacts/benchmarks/complex-merge-opt/heap -c Release -p:ProfileVersion=final -- --count 10000 --repeats 3 --mode both

# Feature benchmarks
dotnet run --project tests/BenchmarkRunner -c Release -f net9.0 -p:NuGetAudit=false -- --filter '*ArgumentInsertionBenchmark*' '*BlockArgumentRenderingBenchmark*' '*AliasCollisionBenchmark*' '*EmptyArgumentBenchmark*' '*ListArgumentBenchmark*' '*RootSelectionBenchmark*' '*MediumRenderBenchmark*' '*AllocationHotspotBenchmark*' '*BatchOperationsBenchmark*' '*TypeCacheBenchmark*' '*TypeCacheChurnBenchmark*' --job short --inProcess
```

The `-p:NuGetAudit=false` in these commands is no longer needed: it worked around advisory
GHSA-23fw-v26w-5fgq in `Microsoft.Build.Tasks.Git` 8.0.0, fixed by moving
`Microsoft.SourceLink.GitHub` to 10.0.401.
Raw reports for the final figures are in the git-ignored
`artifacts/benchmarks/complex-merge-opt/` (`bdn-final`, `bdn-final-published`,
`heap-final.txt`) and `artifacts/benchmarks/final-91bf481/local`.

Open work is listed in [PERFORMANCE_TASKS.md](PERFORMANCE_TASKS.md).
