using System.Collections.Generic;
using BenchmarkDotNet.Attributes;
using NGql.Core.Abstractions;

namespace Benchmarks.Benchmarks;

[MemoryDiagnoser]
public class ArgumentInsertionBenchmark
{
    private Dictionary<string, object> _arguments = null!;

    [Params(10, 1000)]
    public int Count { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _arguments = new Dictionary<string, object>(Count);
        for (var i = 0; i < Count; i++) _arguments.Add($"argument{i:D4}", i);
    }

    [Benchmark]
    public QueryBlock Individual()
    {
        var block = new QueryBlock("Q");
        foreach (var argument in _arguments) block.AddArgument(argument.Key, argument.Value);
        return block;
    }

    [Benchmark]
    public QueryBlock Batch()
    {
        var block = new QueryBlock("Q");
        block.AddArgument(_arguments);
        return block;
    }
}
