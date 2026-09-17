using System.Buffers;
using System.Collections.Generic;
using BenchmarkDotNet.Attributes;
using NGql.Core;
using NGql.Core.Builders;

namespace Benchmarks.Benchmarks;

[MemoryDiagnoser]
public class GuardrailBenchmark
{
    private QueryBuilder _query = null!;
    private readonly QueryBuilder[] _fragments = new QueryBuilder[100];
    private readonly string[] _paths = new string[100];
    private readonly string[] _selected = new string[10];
    private readonly ArrayBufferWriter<byte> _output = new(32768);

    [GlobalSetup]
    public void Setup()
    {
        for (var i = 0; i < _paths.Length; i++)
        {
            _paths[i] = $"users.edges.node.field{i}";
            _fragments[i] = QueryBuilder.CreateDefaultBuilder("F", MergingStrategy.MergeByFieldPath)
                .AddField("users.id", new Dictionary<string, object?> { ["filter"] = i });
        }
        for (var i = 0; i < _selected.Length; i++) _selected[i] = _paths[i * 10];
        _query = BuildPaths();
        _ = CachedPath();
        _ = Utf8();
    }

    [Benchmark]
    public QueryBuilder BuildPaths()
    {
        var query = QueryBuilder.CreateDefaultBuilder("Q");
        foreach (var path in _paths) query.AddField(path);
        return query;
    }

    [Benchmark]
    public QueryBuilder MergeFragments()
    {
        var query = QueryBuilder.CreateDefaultBuilder("Q", MergingStrategy.MergeByFieldPath);
        foreach (var fragment in _fragments) query.Include(fragment);
        return query;
    }

    [Benchmark]
    public string[] CachedPath() => _query.GetPathTo("Q", "edges.node.field99");

    [Benchmark]
    public QueryBuilder Preserve() => PreservationBuilder.Create(_query).Preserve(_selected).Build();

    [Benchmark]
    public int Utf8()
    {
        _output.Clear();
        _query.WriteUtf8(_output);
        return _output.WrittenCount;
    }
}
