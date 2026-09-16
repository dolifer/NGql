using System;
using System.Collections.Generic;
using System.IO;
using BenchmarkDotNet.Attributes;
using NGql.Core.Builders;

namespace Benchmarks.Benchmarks;

[MemoryDiagnoser]
public class RenderingBenchmark
{
    private QueryBuilder _arguments = null!;
    private QueryBuilder _deep = null!;
    private QueryBuilder _oversized = null!;

    [GlobalSetup]
    public void Setup()
    {
        _arguments = QueryBuilder.CreateDefaultBuilder("Arguments").AddField("item",
            new Dictionary<string, object?>
            {
                ["float"] = 1.25f,
                ["double"] = double.MaxValue,
                ["decimal"] = decimal.MaxValue,
                ["date"] = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc),
                ["offset"] = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.FromHours(2))
            });
        _deep = QueryBuilder.CreateDefaultBuilder("Deep")
            .AddField("a.b.c.d.e.f.g.h.i.j.k.l.m.n.o.p");
        _oversized = QueryBuilder.CreateDefaultBuilder("Large").AddField("item",
            new Dictionary<string, object?> { ["text"] = new string('x', 300_000) });
    }

    [Benchmark]
    public string ScalarArguments() => _arguments.ToString();

    [Benchmark]
    public string SingleChildChain() => _deep.ToString();

    [Benchmark]
    public void OversizedRender() => _oversized.WriteTo(TextWriter.Null);
}
