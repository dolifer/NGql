using System.Collections.Generic;
using BenchmarkDotNet.Attributes;
using NGql.Core;
using NGql.Core.Builders;

namespace Benchmarks.Benchmarks;

[MemoryDiagnoser]
public class MergeIsolationBenchmark
{
    private QueryBuilder _target = null!;
    private QueryBuilder _incoming = null!;
    private FieldBuilder _unrelated = null!;

    [Params(10, 1000)]
    public int Count { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _target = QueryBuilder.CreateDefaultBuilder("Q", MergingStrategy.MergeByFieldPath);
        for (var i = 0; i < Count; i++)
        {
            _incoming = QueryBuilder.CreateDefaultBuilder("F", MergingStrategy.MergeByFieldPath)
                .AddField("user.id", new Dictionary<string, object?> { ["filter"] = i });
            _target.Include(_incoming);
        }
        _ = QueryBuilder.CreateDefaultBuilder("Other").AddField("other", field => _unrelated = field);
    }

    [Benchmark]
    public QueryBuilder WarmMerge() => _target.Include(_incoming);

    [Benchmark]
    public QueryBuilder AfterUnrelatedMutation()
    {
        _unrelated.Where("filter", 42);
        return _target.Include(_incoming);
    }
}
