# NGql.Core 2.2.1 against 2.2.0

Measured 2026-09-24 with BenchmarkDotNet 0.15.7, .NET 9.0.9, Apple M4, macOS 27.0. The baseline is
the `2.2.0` tag. The 2.2.0 build ran first and the 2.2.1 build a few hours later on the same host;
the runs were not interleaved, so treat single-digit time changes as approximate. Bytes are managed
allocation per operation, not retained heap, and are exact. "Intervals" compares the two 99.9%
confidence intervals: "separate" means they do not overlap. Microbenchmarks on one machine do not
predict service throughput.

## Release benchmark

`VersionComparisonBenchmark`, in-process job (three warmups, five iterations), measured at
`8d52c0e`. The vectorized scans in `8e1d19a` landed afterwards; in a short job they took 200
dotted-path builds from 39.3 to 32.5 µs and the simple query from 347 to 324 ns, with unchanged
allocation.

| Workload | 2.2.0 time | 2.2.1 time | Change | Intervals | 2.2.0 bytes | 2.2.1 bytes | Change |
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

## Paths the release benchmark does not isolate

`PostReleaseBenchmark`, same job and host. It uses only the public API, so the same file runs
against a `2.2.0` checkout for the baseline.

| Workload | 2.2.0 time | 2.2.1 time | Change | 2.2.0 bytes | 2.2.1 bytes | Change |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| RootsWithLambda (1,000 roots) | 1.535 ms | 0.132 ms | −91% | 701,591 | 549,576 | −21.7% |
| RootsWithArguments (1,000 roots) | 2.092 ms | 0.249 ms | −88% | 1,534,400 | 534,426 | −65.2% |
| ReAddWithArguments | 1,707 ns | 975 ns | −43% | 6,272 | 2,680 | −57.3% |
| MergeByFieldPathTwoFragments | 590 ns | 521 ns | −12% | 2,880 | 2,448 | −15.0% |
| DivergentFilterMerge (800 fragments) | 398.3 µs | 373.4 µs | −6% | 961,418 | 926,762 | −3.6% |
| NeverMergeIncludes (200 fragments) | 46.3 µs | 46.5 µs | ±0% (overlap) | 176,184 | 160,072 | −9.1% |
| PreservePaths | 287 ns | 266 ns | −7% | 1,456 | 1,296 | −11.0% |
| ClassicSelectList | 237 ns | 176 ns | −26% | 944 | 328 | −65.3% |

The first two rows show the root-lookup fix: the old case-sensitive scan made each new root O(n),
so adding many roots was quadratic. `RootsWithArguments` also no longer builds a throwaway
`SortedDictionary` per call.

## Reproduce

```sh
dotnet run --project tests/BenchmarkRunner -c Release -f net9.0 -- --filter '*VersionComparisonBenchmark*' '*PostReleaseBenchmark*' --warmupCount 3 --iterationCount 5 --inProcess --artifacts benchmark-results/2.2.1
```

For the baseline, run the same command in a `2.2.0` checkout with
`tests/BenchmarkRunner/Benchmarks/PostReleaseBenchmark.cs` copied in. Write results outside
`artifacts/`: `make clean` and `make ci` delete that folder.
