using System.Collections.Generic;
using BenchmarkDotNet.Attributes;
using NGql.Core.Abstractions;
using NGql.Core.Builders;

namespace Benchmarks.Benchmarks;

[MemoryDiagnoser]
public class EmptyArgumentBenchmark
{
    private readonly Dictionary<string, object?> _list = new()
    {
        ["values"] = new List<object>()
    };
    private readonly Dictionary<string, object?> _dictionary = new()
    {
        ["filter"] = new Dictionary<string, object>()
    };

    [Benchmark]
    public QueryBuilder EmptyList() => QueryBuilder.CreateDefaultBuilder("Q").AddField("item", _list);

    [Benchmark]
    public QueryBuilder EmptyDictionary() => QueryBuilder.CreateDefaultBuilder("Q").AddField("item", _dictionary);

    [Benchmark]
    public QueryBlock BlockEmptyList()
    {
        var block = new QueryBlock("Q");
        block.AddArgument("values", _list["values"]!);
        return block;
    }

    [Benchmark]
    public QueryBlock BlockEmptyDictionary()
    {
        var block = new QueryBlock("Q");
        block.AddArgument("filter", _dictionary["filter"]!);
        return block;
    }
}
