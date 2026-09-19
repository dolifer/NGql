using System.Collections.Generic;
using BenchmarkDotNet.Attributes;
using NGql.Core.Abstractions;

namespace Benchmarks.Benchmarks;

[MemoryDiagnoser]
public class ListArgumentBenchmark
{
    private Dictionary<string, object?> _arguments = null!;

    [Params(10, 1000)]
    public int Count { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var values = new List<object>(Count);
        for (var i = 0; i < Count; i++) values.Add(i);
        _arguments = new Dictionary<string, object?> { ["values"] = values };
    }

    [Benchmark]
    public FieldDirective NormalizeList() => new("filter", _arguments);
}
