# NGql.Core performance — final state

`main` at `d6cfe86`, compared with the released **NGql.Core 2.1.0** package and with the
baseline `ecb2ded` that preceded this work. Public method signatures, rendered output and the
legacy reflection-based `Include` path are unchanged. The pass-by-pass history is in git; commit hashes below refer to `main` after its 2026-09-20 history rewrite.

A later pass on top of the 2.2.0 release is described in [Since 2.2.0](#since-220).

## Since 2.2.0

Branch `perf/post-2.2-pass` (`875f64f`…`8d52c0e`), compared with the `2.2.0` tag, whose
`src/Core` is identical to `main` at `bcf3b21`. Public signatures, rendered output,
collection-sharing behavior and the argument key-collision rules are unchanged. One bug was
fixed along the way (see below).

### Release benchmark

`VersionComparisonBenchmark`, in-process job (three warmups, five iterations), .NET 9.0.9,
Apple M4, 2026-09-24. The 2.2.0 build ran first, the branch a few hours later on the same host;
the runs were not interleaved, so treat single-digit time changes as approximate. Bytes are
exact. "Intervals" compares the 99.9% confidence intervals.

| Workload | 2.2.0 time | Branch time | Change | Intervals | 2.2.0 bytes | Branch bytes | Change |
| --- | ---: | ---: | ---: | --- | ---: | ---: | ---: |
| SimpleQuery | 408.6 ns | 269.2 ns | −34.1% | separate | 1,413 | 1,034 | −26.8% |
| TypeDriftScenario | 527.6 ns | 448.2 ns | −15.0% | overlap | 1,782 | 1,505 | −15.5% |
| CaseInsensitiveFields | 435.5 ns | 329.0 ns | −24.5% | separate | 1,597 | 1,188 | −25.6% |
| ComplexQueryWithMerging | 1,221.8 ns | 1,168.3 ns | −4.4% | overlap | 4,721 | 4,362 | −7.6% |
| ArrayTypePreservation | 531.0 ns | 459.0 ns | −13.6% | separate | 1,434 | 1,157 | −19.3% |
| DeepNestedFields | 1,421.0 ns | 1,249.0 ns | −12.1% | overlap | 5,581 | 4,895 | −12.3% |
| ArgumentsPoolStress | 7,530.6 ns | 5,791.9 ns | −23.1% | separate | 37,724 | 27,884 | −26.1% |
| TypeDriftEdgeCases | 562.4 ns | 521.1 ns | −7.3% | separate | 1,403 | 1,178 | −16.1% |
| NestedClassicQuery (depth 10) | 3,075.9 ns | 2,136.3 ns | −30.5% | separate | 13,363 | 6,001 | −55.1% |
| NestedClassicQuery (depth 30) | 12,394.1 ns | 7,211.6 ns | −41.8% | separate | 56,556 | 34,796 | −38.5% |
| BulkQueryBuilding (10) | 5,172.1 ns | 4,424.5 ns | −14.5% | separate | 20,398 | 16,005 | −21.5% |
| BulkQueryBuilding (100) | 54.64 µs | 44.35 µs | −18.8% | overlap | 204,001 | 160,000 | −21.6% |
| DictionaryArgumentRendering (10) | 9,836.3 ns | 7,491.8 ns | −23.8% | separate | 37,202 | 25,364 | −31.8% |
| DictionaryArgumentRendering (50) | 49.49 µs | 37.44 µs | −24.3% | separate | 185,999 | 126,802 | −31.8% |
| DirectivesRendering (10) | 8,942.8 ns | 6,327.6 ns | −29.2% | separate | 42,004 | 27,679 | −34.1% |
| DirectivesRendering (50) | 47.03 µs | 31.87 µs | −32.2% | separate | 210,002 | 138,404 | −34.1% |
| PreserveFromExpressionRepeated (10) | 10.61 µs | 9,888.0 ns | −6.8% | separate | 30,720 | 28,252 | −8.0% |
| PreserveFromExpressionRepeated (50) | 67.77 µs | 47.69 µs | −29.6% | separate | 147,528 | 136,090 | −7.8% |
| ToStringPerCall (10) | 3,205.3 ns | 3,045.7 ns | −5.0% | separate | 9,155 | 8,458 | −7.6% |
| ToStringPerCall (50) | 12.25 µs | 11.73 µs | −4.2% | overlap | 32,195 | 31,498 | −2.2% |
| AppendToReusedBuilder (10) | 3,174.3 ns | 2,957.5 ns | −6.8% | separate | 4,659 | 3,973 | −14.7% |
| AppendToReusedBuilder (50) | 11.77 µs | 11.16 µs | −5.2% | separate | 4,659 | 3,973 | −14.7% |
| Utf8ViaToStringGetBytes (10) | 3,554.5 ns | 3,389.0 ns | −4.7% | overlap | 12,196 | 11,500 | −5.7% |
| Utf8ViaToStringGetBytes (50) | 16.14 µs | 13.34 µs | −17.3% | overlap | 47,391 | 46,694 | −1.5% |
| Utf8ViaWriteUtf8 (10) | 3,160.7 ns | 3,006.3 ns | −4.9% | separate | 4,280 | 3,584 | −16.3% |
| Utf8ViaWriteUtf8 (50) | 14.03 µs | 11.23 µs | −20.0% | overlap | 4,280 | 3,584 | −16.3% |
| SpanPathOptimization (50) | 16.37 µs | 14.25 µs | −12.9% | separate | 57,262 | 49,797 | −13.0% |
| SpanPathOptimization (200) | 69.30 µs | 60.93 µs | −12.1% | separate | 232,059 | 202,988 | −12.5% |
| MassiveFieldCount (100) | 11.43 µs | 10.63 µs | −7.0% | separate | 26,573 | 26,348 | −0.8% |
| MassiveFieldCount (500) | 68.35 µs | 61.61 µs | −9.9% | overlap | 129,280 | 129,055 | −0.2% |

Every workload allocates less than 2.2.0; none allocates more. 21 of the 30 are faster with
separate intervals; the other 9 overlap and none is slower. The `AppendToReusedBuilder` bytes are
the one-time query build: renders into a reused builder allocate nothing per call.

### Paths the release benchmark does not isolate

`PostReleaseBenchmark`, same job and host as above, 2.2.0 first.

| Workload | 2.2.0 time | Branch time | Change | 2.2.0 bytes | Branch bytes | Change |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| RootsWithLambda (1,000 roots) | 1.535 ms | 0.132 ms | −91% | 701,591 | 549,576 | −21.7% |
| RootsWithArguments (1,000 roots) | 2.092 ms | 0.249 ms | −88% | 1,534,400 | 534,426 | −65.2% |
| ReAddWithArguments | 1,707 ns | 975 ns | −43% | 6,272 | 2,680 | −57.3% |
| MergeByFieldPathTwoFragments | 590 ns | 521 ns | −12% | 2,880 | 2,448 | −15.0% |
| DivergentFilterMerge (800 fragments) | 398.3 µs | 373.4 µs | −6% | 961,418 | 926,762 | −3.6% |
| NeverMergeIncludes (200 fragments) | 46.3 µs | 46.5 µs | ±0% (overlap) | 176,184 | 160,072 | −9.1% |
| PreservePaths | 287 ns | 266 ns | −7% | 1,456 | 1,296 | −11.0% |
| ClassicSelectList | 237 ns | 176 ns | −26% | 944 | 328 | −65.3% |

The first two rows are the root-lookup fix: the old case-sensitive scan made each new root
O(n), so they were quadratic. `RootsWithArguments` also no longer builds a throwaway `SortedDictionary` per call.

### What changed

| Change | Where |
| --- | --- |
| Root lookups by span go through the dictionary's own case-insensitive comparer (alternate lookup on .NET 9+) instead of a case-sensitive linear scan | `SpanExtensions` |
| A new field's name, dictionary key and path share one string; a dotted leaf reuses the caller's string as its path | `FieldFactory`, `SpanExtensions` |
| The simple-field fast path adds or finds with one hash probe | `QueryBuilder.AddFieldFastPath` |
| Arguments are sorted once, at field creation, instead of into a throwaway `SortedDictionary` first; key collisions are still rejected up front, before any variable is extracted, with an allocation-free check up to 16 keys | `QueryBuilder` |
| Argument dictionaries are copied, rendered and scanned through struct enumerators instead of boxed interface ones | `Helpers`, `QueryTextBuilder`, `PreserveExtensions` |
| Merging arguments into an existing field copies the stored `SortedDictionary` with its linear-time copy constructor when it uses the case-insensitive comparer | `FieldDefinitionExtensions` |
| Cycle detection during variable extraction creates its set on the first nested container only | `Helpers.ExtractVariablesFromChild` |
| Builders hold the owning `QueryDefinition`, so its variable set is created only when a variable is promoted | `FieldBuilder`, `PreserveExtensions` |
| `QueryMap` keeps its first mapping inline; the dictionary appears on the second | `QueryMap` |
| Sub-field arrays no longer attach an empty metadata dictionary to every sub-field | `FieldBuilder.AddSubField` |
| Classic blocks create their argument dictionary and variable set on first use (the public getters still return the live collections), sort field lists with a pooled stable sort instead of LINQ `OrderBy`, and skip enumerating empty sub-query variable sets | `QueryBlock` |
| `IncludeIf`/`SkipIf` build their normalized arguments directly | `FieldBuilder` |
| The merge index dropped two sets that duplicated existing data (names with built buckets; every root key) and creates its suffix counters on the first collision | `FieldMergeIndex` |

### Fixed along the way

A root field added with a lambda or with arguments was matched against existing roots
case-sensitively while the root dictionary is case-insensitive, so `AddField("User", …)` then
`AddField("user", …)` replaced the first root and dropped its children. The same scan made adding
n such roots O(n²). Regression tests: `RootFieldCaseInsensitiveLookupTests`.

### Measured and not adopted

- **A faster comparer for the render sort.** An EventPipe CPU profile attributed about 68% of a
  500-field render to case-insensitive string comparison. Direct measurement disagreed: an
  ordinal sort of the same names costs 21 µs against 30 µs, and a prefix-skipping comparer gave
  no gain. The sampler misattributes on macOS arm64; confirm its findings with a benchmark
  before acting on them.
- **Caching the sorted order, replacing argument storage, dropping the `FieldChildren` lock
  object:** see [PERFORMANCE_TASKS.md](PERFORMANCE_TASKS.md#measured-and-deferred).

### Validation

2,339 unit tests, 92 integration tests and 76 tool tests pass on each of .NET 8, 9 and 10, with
100% line and branch coverage (`make coverage`). New regression tests cover the root-case bug,
argument collision rules on both dictionary sizes, shared-string identity, the lazy collections
(including live-view getters), list ordering against LINQ `OrderBy` on 200 random mixed lists,
directive argument rendering, indirect variable cycles, and the argument copy paths.

```sh
dotnet run --project tests/BenchmarkRunner -c Release -f net9.0 -- --filter '*VersionComparisonBenchmark*' '*PostReleaseBenchmark*' --warmupCount 3 --iterationCount 5 --inProcess --artifacts benchmark-results/post-2.2
```

`PostReleaseBenchmark` uses only the public API, so the same file can be copied into a `2.2.0`
checkout for the baseline.

## How to read the numbers

- BenchmarkDotNet 0.15.7, .NET 9.0.9, Apple M4, macOS 27.0, Release. "Bytes" are managed
  bytes allocated per benchmark operation, not retained heap or RSS.
- The 2.1.0 comparison below was measured on the final `main` build on 2026-09-19 on a quiet
  host (load average about 2). Full tables for both benchmark jobs:
  [v2.2.0/BENCHMARKS.md](v2.2.0/BENCHMARKS.md).
- Per-feature "before" values were measured on the commit that preceded each feature; "final"
  allocation values were measured on the final build. Per-feature timings name their build.
- These are microbenchmarks on one machine. They do not establish application throughput or
  process memory.

## Against NGql.Core 2.1.0

Same linked benchmark source for both. Every shared case allocates less than the
release; none allocates more.

| Workload | 2.1.0 bytes | Final bytes | Change |
| --- | ---: | ---: | ---: |
| Simple query | 1,597 | 1,413 | −11.5% |
| Type drift scenario | 2,130 | 1,782 | −16.3% |
| Type drift edge cases | 1,915 | 1,403 | −26.7% |
| Case-insensitive fields | 1,812 | 1,597 | −11.9% |
| Array type preservation | 1,843 | 1,434 | −22.2% |
| Complex query with merging | 6,380 | 4,721 | −26.0% |
| Deep nested fields | 5,857 | 5,581 | −4.7% |
| Arguments pool stress | 40,366 | 37,724 | −6.5% |
| Bulk building, 10 queries | 22,559 | 20,398 | −9.6% |
| Bulk building, 100 queries | 225,597 | 204,001 | −9.6% |
| Dotted paths, 50 fields | 58,593 | 57,262 | −2.3% |
| Dotted paths, 200 fields | 236,984 | 232,059 | −2.1% |
| Flat selection, 100 fields | 26,747 | 26,573 | −0.7% |
| Flat selection, 500 fields | 129,444 | 129,280 | −0.1% |
| Classic nesting, depth 10 | 54,456 | 13,363 | −75.5% |
| Classic nesting, depth 30 | 685,332 | 56,556 | −91.7% |
| Dictionary arguments ×10 | 41,759 | 37,202 | −10.9% |
| Dictionary arguments ×50 | 208,804 | 185,999 | −10.9% |
| Expression preservation ×10 | 37,509 | 30,720 | −18.1% |
| Expression preservation ×50 | 180,879 | 147,528 | −18.4% |
| ToString ×10 | 11,295 | 9,155 | −18.9% |
| ToString ×50 | 42,015 | 32,195 | −23.4% |
| ToString + UTF-8 ×10 | 14,336 | 12,196 | −14.9% |
| ToString + UTF-8 ×50 | 57,221 | 47,391 | −17.2% |

Timing against 2.1.0 on the final build (in-process job, 99.9% intervals): **22 of the 24
workloads are faster with non-overlapping intervals; the two flat-selection workloads are
unchanged** (+1.4% and +2.1%, overlapping). In the separate-process job, which has only three
iterations, 12 of the 24 intervals separate; every one of those is faster, and the
flat-selection workloads are again within noise (−0.3% and +2.4%).

| Workload | 2.1.0 | 2.2.0 | Change |
| --- | ---: | ---: | ---: |
| Simple query | 372.3 ns | 330.2 ns | −11.3% |
| Complex query with merging | 1,426.3 ns | 1,218.9 ns | −14.5% |
| Type drift scenario | 568.5 ns | 483.4 ns | −15.0% |
| Deep nested fields | 1,487.1 ns | 1,315.8 ns | −11.5% |
| Arguments pool stress | 8.41 µs | 7.29 µs | −13.3% |
| Bulk building, 100 queries | 57.17 µs | 52.05 µs | −9.0% |
| Dictionary arguments ×50 | 59.64 µs | 49.20 µs | −17.5% |
| Expression preservation ×50 | 57.24 µs | 50.99 µs | −10.9% |
| ToString ×50 | 15.58 µs | 12.08 µs | −22.4% |
| ToString + UTF-8 ×50 | 17.13 µs | 13.72 µs | −19.9% |
| Dotted paths, 200 fields | 72.29 µs | 68.90 µs | −4.7% |
| Flat selection, 500 fields | 63.48 µs | 64.35 µs | +1.4% (overlap) |
| Classic nesting, depth 10 | 6.64 µs | 3.05 µs | −54.2% |
| Classic nesting, depth 30 | 39.26 µs | 9.86 µs | −74.9% |

The two runners ran one after the other rather than interleaved, so treat single-digit
percentages as approximate.

Retained managed heap per held complex-merge workload (10,000 held, compacting
full GC, three identical repeats, measured at `c4845bf`; the layout has not changed since):

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
| Single-child selections render without a sort buffer | Sixteen-level chain, time (at `423419d`) | 971.5 ns | 357.4 ns |
| Sorting uses cached comparisons, no adapter objects | Write 10 sorted root fields, bytes | 64 | 0 |
| Root arguments sort pooled entries instead of a dictionary | Render 1,000 arguments, bytes | 92,497 | 36,024 |
| | Render 1,000 variables, bytes | 126,610 | 70,249 |
| | Render 10 arguments / variables, bytes | 1,408 / 1,672 | 472 / 848 |
| A lone argument or variable renders without an enumerator | Render 1 argument / variable, bytes | 552 / 480 | 80 / 88 |
| | Render 1 argument, time (at `c8cb8bc`) | 63.0 ns | 44.0 ns |
| | Warm single-entry write to a sink, bytes | — | 0 |
| Single root selection renders from a reference span | One-field write, time (at `f22174c`) | 44.5 ns | 32.8 ns |
| Single-chunk UTF-8 output skips the encoder | Small UTF-8 query, bytes | 48 | 0 |
| | Large UTF-8 query, bytes | 176 | 128 |

### Arguments and variables

| Feature | Metric | Before | Final |
| --- | --- | ---: | ---: |
| New argument keys skip the key-casing scan | Insert 1,000 arguments individually, bytes | 240,108 | 56,275 |
| | Same, time (at `b346794`) | 4,173 µs | 387 µs |
| | Insert 10 individually / as a batch, bytes | 1,710 / 2,447 | 832 / 1,872 |
| | Insert a batch of 1,000, bytes | 201,564 | 145,545 |
| Root-variable lookup is logarithmic | 1,000 root variables, bytes | 294,245 | 54,040 |
| | Same, time (at `9a24b12`) | 4,435 µs | 374 µs |
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
| Merge-index invalidation is scoped to the queries that observe a field, not process-wide | Merge after an unrelated mutation, 1,000 candidates (at `f22174c`) | 81,608 ns, 268,728 B | 290 ns, 296 B |
| | Warm merge, 1,000 candidates, bytes | 272 | 272 |
| Captured builders keep the index correct: `Where`, `IncludeIf`/`SkipIf` and argument-bearing `AddField` on a builder captured from a nested action, a dotted path or after a sub-field overload | Regression cases confirmed to produce two definitions instead of one | 6 failing | 0 failing |

### Preservation

| Feature | Metric | Before | Final |
| --- | --- | ---: | ---: |
| Ancestor pruning probes dotted prefixes instead of scanning all paths | 1,000 unrelated paths, bytes | 97,188 | 73,192 |
| | Same, time (at `9a24b12`) | 501 µs | 40 µs |
| .NET 8 hash prefilter avoids prefix strings | 512 deep paths after a short path, extra bytes (.NET 8) | 114,576 | under 16,384 |

### Caches, pools and retention

| Feature | Metric | Before | Final |
| --- | --- | ---: | ---: |
| Builder pool budget applies to the sum of pooled builders per thread (262,144 characters) | Characters retained after four 200,000-character builders | 800,000 | 200,000 |
| Over budget, the pool keeps the largest builders that fit, largest on top | Builder retained after an 80,000 + 200,000 nested render | first returned | the 200,000 one, either order |
| Medium renders still reuse their builder | 1,024 to 131,072-character argument, bytes | 128 | 128 |
| Custom type names: two lock-free generations of 2,048, names in use are promoted | Names retained after 20,000 unique lookups | 20,000 | at most about 4,096 |
| | Churn of 6,000 names, 1 / 4 threads (at `fa5c3fb`) | 2.564 / 5.223 ms | 2.266 / 4.440 ms |
| | Name kept in use across 20,000 others | evicted | same instance |
| | Cached custom-type field creation (at `fa5c3fb`) | 158.1 ns, 896 B | 158.0 ns, 896 B |
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
  before the 96 B layout (ordered runs at `3c0a8b6`), plus 1–3% for atomic
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
make ci     # clean build, all tests, coverage report (deletes artifacts/ first)

# Release comparison: see docs/v2.2.0/BENCHMARKS.md for the two commands and full tables.

# Feature benchmarks
dotnet run --project tests/BenchmarkRunner -c Release -f net9.0 -- --filter '*ArgumentInsertionBenchmark*' '*BlockArgumentRenderingBenchmark*' '*AliasCollisionBenchmark*' '*EmptyArgumentBenchmark*' '*ListArgumentBenchmark*' '*RootSelectionBenchmark*' '*MediumRenderBenchmark*' '*AllocationHotspotBenchmark*' '*BatchOperationsBenchmark*' '*TypeCacheBenchmark*' '*TypeCacheChurnBenchmark*' '*MergeIsolationBenchmark*' '*GuardrailBenchmark*' --job short --inProcess --artifacts benchmark-results/features
```

Write benchmark output outside `artifacts/`: `make clean` and `make ci` delete that folder.
The per-pass raw reports, the preserved intermediate assemblies and the standalone
retained-heap and paired-assembly harnesses used during this work lived there, were
git-ignored, and were removed by a `make ci` run on 2026-09-19. Their results are recorded in
this document; the retained-heap and per-pass figures cannot be regenerated without
rebuilding those harnesses.

Open work is listed in [PERFORMANCE_TASKS.md](PERFORMANCE_TASKS.md).
