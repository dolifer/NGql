using System;
using System.Linq;
using BenchmarkDotNet.Attributes;

namespace Benchmarks.Benchmarks;

[MemoryDiagnoser]
public class SplitAndRangeBenchmark
{
    private readonly string[] parts = "parent.child.grandchild".Split('.', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

    [Benchmark]
    public string[] ToArray()
        => parts.Skip(1).ToArray();

    [Benchmark]
    public string[] Range()
        => parts[1..];
}