using System.IO;
using BenchmarkDotNet.Attributes;
using NGql.Core.Builders;

namespace Benchmarks.Benchmarks;

[MemoryDiagnoser]
public class RootSelectionBenchmark
{
    private QueryBuilder _query = null!;

    [Params(1, 10, 1000)]
    public int Count { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _query = QueryBuilder.CreateDefaultBuilder("Q");
        for (var i = 0; i < Count; i++) _query.AddField($"field{i}");
    }

    [Benchmark]
    public void Write() => _query.WriteTo(TextWriter.Null);

    [Benchmark]
    public string Render() => _query.ToString();
}
