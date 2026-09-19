using System.Buffers;
using System.Collections.Generic;
using BenchmarkDotNet.Attributes;
using NGql.Core;
using NGql.Core.Builders;

namespace Benchmarks.Benchmarks;

[MemoryDiagnoser]
public class AllocationHotspotBenchmark
{
    private QueryBuilder _small = null!;
    private QueryBuilder _large = null!;
    private readonly ArrayBufferWriter<byte> _output = new(32768);
    private readonly Dictionary<string, object?> _arguments = new()
    {
        ["filter"] = new Filter()
    };

    public sealed class Filter
    {
        public string Name { get; set; } = "Alice";
        public int Limit { get; set; } = 20;
        public bool Active { get; set; } = true;
        public Variable Cursor { get; set; } = new("$cursor", "String");
    }

    [GlobalSetup]
    public void Setup()
    {
        _small = QueryBuilder.CreateDefaultBuilder("Q").AddField("users.id");
        _large = QueryBuilder.CreateDefaultBuilder("Q").AddField("item",
            new Dictionary<string, object?> { ["text"] = new string('x', 10000) });
        _ = ObjectArguments();
        SmallUtf8();
        LargeUtf8();
    }

    [Benchmark]
    public int SmallUtf8()
    {
        _output.Clear();
        _small.WriteUtf8(_output);
        return _output.WrittenCount;
    }

    [Benchmark]
    public int LargeUtf8()
    {
        _output.Clear();
        _large.WriteUtf8(_output);
        return _output.WrittenCount;
    }

    [Benchmark]
    public QueryBuilder ObjectArguments() => QueryBuilder.CreateDefaultBuilder("Q")
        .AddField("users", _arguments);
}
