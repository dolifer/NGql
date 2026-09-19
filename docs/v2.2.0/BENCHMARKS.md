# NGql.Core 2.2.0 against 2.1.0

Measured 2026-09-19 on the `main` build (`d6cfe86`) and the NGql.Core 2.1.0 NuGet package with the same
linked source, `VersionComparisonBenchmark`. BenchmarkDotNet 0.15.7, .NET 9.0.9, Apple M4, macOS 27.0,
quiet host (load average about 2). The 2.1.0 runner ran first, then the local runner; each ran an
in-process job (three warmups, five iterations) and BenchmarkDotNet's separate-process ShortRun (three
iterations). "Separate" means the two 99.9% confidence intervals do not overlap. Bytes are managed
allocation per operation, not retained heap. Microbenchmarks on one machine do not predict service throughput.

## In-process job

| Workload | 2.1.0 time | 2.2.0 time | Change | Intervals | 2.1.0 bytes | 2.2.0 bytes | Change |
| --- | ---: | ---: | ---: | --- | ---: | ---: | ---: |
| ArgumentsPoolStress | 8,409.2 ns | 7,288.0 ns | -13.3% | separate | 40,366 | 37,724 | -6.5% |
| ArrayTypePreservation | 555.5 ns | 495.3 ns | -10.8% | separate | 1,843 | 1,434 | -22.2% |
| BulkQueryBuilding (iterations 10) | 5,719.7 ns | 5,177.1 ns | -9.5% | separate | 22,559 | 20,398 | -9.6% |
| BulkQueryBuilding (iterations 100) | 57.17 µs | 52.05 µs | -9.0% | separate | 225,597 | 204,001 | -9.6% |
| CaseInsensitiveFields | 431.9 ns | 400.8 ns | -7.2% | separate | 1,812 | 1,597 | -11.9% |
| ComplexQueryWithMerging | 1,426.3 ns | 1,218.9 ns | -14.5% | separate | 6,380 | 4,721 | -26.0% |
| DeepNestedFields | 1,487.1 ns | 1,315.8 ns | -11.5% | separate | 5,857 | 5,581 | -4.7% |
| DictionaryArgumentRendering (iterations 10) | 11.97 µs | 9,860.3 ns | -17.6% | separate | 41,759 | 37,202 | -10.9% |
| DictionaryArgumentRendering (iterations 50) | 59.64 µs | 49.20 µs | -17.5% | separate | 208,804 | 185,999 | -10.9% |
| MassiveFieldCount (fieldCount 100) | 11.12 µs | 11.36 µs | +2.1% | overlap | 26,747 | 26,573 | -0.7% |
| MassiveFieldCount (fieldCount 500) | 63.48 µs | 64.35 µs | +1.4% | overlap | 129,444 | 129,280 | -0.1% |
| NestedClassicQuery (depth 10) | 6,644.7 ns | 3,046.4 ns | -54.2% | separate | 54,456 | 13,363 | -75.5% |
| NestedClassicQuery (depth 30) | 39.26 µs | 9,855.5 ns | -74.9% | separate | 685,332 | 56,556 | -91.7% |
| PreserveFromExpressionRepeated (iterations 10) | 11.81 µs | 10.50 µs | -11.0% | separate | 37,509 | 30,720 | -18.1% |
| PreserveFromExpressionRepeated (iterations 50) | 57.24 µs | 50.99 µs | -10.9% | separate | 180,879 | 147,528 | -18.4% |
| SimpleQuery | 372.3 ns | 330.2 ns | -11.3% | separate | 1,597 | 1,413 | -11.5% |
| SpanPathOptimization (fieldCount 200) | 72.29 µs | 68.90 µs | -4.7% | separate | 236,984 | 232,059 | -2.1% |
| SpanPathOptimization (fieldCount 50) | 17.28 µs | 16.13 µs | -6.7% | separate | 58,593 | 57,262 | -2.3% |
| ToStringPerCall (iterations 10) | 3,859.7 ns | 3,208.0 ns | -16.9% | separate | 11,295 | 9,155 | -18.9% |
| ToStringPerCall (iterations 50) | 15.58 µs | 12.08 µs | -22.4% | separate | 42,015 | 32,195 | -23.4% |
| TypeDriftEdgeCases | 625.2 ns | 553.6 ns | -11.5% | separate | 1,915 | 1,403 | -26.7% |
| TypeDriftScenario | 568.5 ns | 483.4 ns | -15.0% | separate | 2,130 | 1,782 | -16.3% |
| Utf8ViaToStringGetBytes (iterations 10) | 4,212.6 ns | 3,542.4 ns | -15.9% | separate | 14,336 | 12,196 | -14.9% |
| Utf8ViaToStringGetBytes (iterations 50) | 17.13 µs | 13.72 µs | -19.9% | separate | 57,221 | 47,391 | -17.2% |

## Separate process job

| Workload | 2.1.0 time | 2.2.0 time | Change | Intervals | 2.1.0 bytes | 2.2.0 bytes | Change |
| --- | ---: | ---: | ---: | --- | ---: | ---: | ---: |
| ArgumentsPoolStress | 7,856.8 ns | 6,714.3 ns | -14.5% | overlap | 40,366 | 37,724 | -6.5% |
| ArrayTypePreservation | 551.6 ns | 457.0 ns | -17.2% | separate | 1,843 | 1,434 | -22.2% |
| BulkQueryBuilding (iterations 10) | 5,405.0 ns | 4,816.2 ns | -10.9% | separate | 22,559 | 20,398 | -9.6% |
| BulkQueryBuilding (iterations 100) | 54.45 µs | 48.44 µs | -11.0% | separate | 225,597 | 204,001 | -9.6% |
| CaseInsensitiveFields | 418.2 ns | 374.1 ns | -10.5% | overlap | 1,812 | 1,597 | -11.9% |
| ComplexQueryWithMerging | 1,373.7 ns | 1,161.0 ns | -15.5% | separate | 6,380 | 4,721 | -26.0% |
| DeepNestedFields | 1,396.1 ns | 1,242.1 ns | -11.0% | overlap | 5,857 | 5,581 | -4.7% |
| DictionaryArgumentRendering (iterations 10) | 10.98 µs | 9,000.6 ns | -18.0% | overlap | 41,759 | 37,202 | -10.9% |
| DictionaryArgumentRendering (iterations 50) | 55.01 µs | 45.63 µs | -17.1% | overlap | 208,804 | 185,999 | -10.9% |
| MassiveFieldCount (fieldCount 100) | 9,958.4 ns | 10.19 µs | +2.4% | overlap | 26,706 | 26,573 | -0.5% |
| MassiveFieldCount (fieldCount 500) | 58.13 µs | 57.97 µs | -0.3% | overlap | 129,413 | 129,280 | -0.1% |
| NestedClassicQuery (depth 10) | 6,098.5 ns | 2,885.2 ns | -52.7% | separate | 54,456 | 13,363 | -75.5% |
| NestedClassicQuery (depth 30) | 34.22 µs | 9,347.3 ns | -72.7% | separate | 685,332 | 56,556 | -91.7% |
| PreserveFromExpressionRepeated (iterations 10) | 10.79 µs | 9,428.4 ns | -12.6% | overlap | 37,489 | 30,720 | -18.1% |
| PreserveFromExpressionRepeated (iterations 50) | 49.43 µs | 45.21 µs | -8.5% | overlap | 180,849 | 147,528 | -18.4% |
| SimpleQuery | 351.4 ns | 295.2 ns | -16.0% | overlap | 1,597 | 1,413 | -11.5% |
| SpanPathOptimization (fieldCount 200) | 69.79 µs | 64.17 µs | -8.1% | overlap | 236,984 | 232,059 | -2.1% |
| SpanPathOptimization (fieldCount 50) | 16.31 µs | 14.65 µs | -10.2% | overlap | 58,593 | 57,262 | -2.3% |
| ToStringPerCall (iterations 10) | 3,727.0 ns | 2,996.1 ns | -19.6% | separate | 11,295 | 9,155 | -18.9% |
| ToStringPerCall (iterations 50) | 14.93 µs | 11.57 µs | -22.5% | separate | 42,015 | 32,195 | -23.4% |
| TypeDriftEdgeCases | 597.5 ns | 530.4 ns | -11.2% | overlap | 1,915 | 1,403 | -26.7% |
| TypeDriftScenario | 573.9 ns | 447.1 ns | -22.1% | separate | 2,130 | 1,782 | -16.3% |
| Utf8ViaToStringGetBytes (iterations 10) | 4,073.3 ns | 3,322.4 ns | -18.4% | separate | 14,336 | 12,196 | -14.9% |
| Utf8ViaToStringGetBytes (iterations 50) | 16.49 µs | 12.71 µs | -22.9% | separate | 57,221 | 47,391 | -17.2% |

## Reproduce

```sh
NGqlCoreVersion=2.1.0 dotnet run --project tests/BenchmarkRunner.Published -c Release -f net9.0 -- --filter '*VersionComparisonBenchmark*' --warmupCount 3 --iterationCount 5 --inProcess --artifacts benchmark-results/published
dotnet run --project tests/BenchmarkRunner -c Release -f net9.0 -- --filter '*VersionComparisonBenchmark*' --warmupCount 3 --iterationCount 5 --inProcess --artifacts benchmark-results/local
```

Each command produces both tables: `--inProcess` configures the command-line job, and
`VersionComparisonBenchmark` adds the separate-process ShortRun through its own config.

Write results outside `artifacts/`: `make clean` and `make ci` delete that folder.
