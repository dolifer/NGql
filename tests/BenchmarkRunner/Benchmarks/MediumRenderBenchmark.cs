using System.Collections.Generic;
using System.IO;
using BenchmarkDotNet.Attributes;
using NGql.Core.Builders;

namespace Benchmarks.Benchmarks;

[MemoryDiagnoser]
public class MediumRenderBenchmark
{
    private QueryBuilder _query = null!;

    [Params(1024, 32768, 131072)]
    public int Length { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _query = QueryBuilder.CreateDefaultBuilder("Q").AddField("item",
            new Dictionary<string, object?> { ["text"] = new string('x', Length) });
        Write();
    }

    [Benchmark]
    public void Write() => _query.WriteTo(TextWriter.Null);
}
