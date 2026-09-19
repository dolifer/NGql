using System;
using System.Reflection;
using BenchmarkDotNet.Attributes;
using NGql.Core.Abstractions;
using NGql.Core.Builders;

namespace Benchmarks.Benchmarks;

[MemoryDiagnoser]
public class AliasCollisionBenchmark
{
    private delegate string GenerateKey(string baseKey, ReadOnlySpan<FieldDefinition> fields);

    private GenerateKey _generate = null!;
    private FieldDefinition[] _fields = null!;

    [Params(10, 1000)]
    public int Count { get; set; }

    [GlobalSetup]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Major Code Smell", "S3011",
        Justification = "Benchmark-only delegate binding avoids changing production API or timing reflection invocation.")]
    public void Setup()
    {
        _generate = typeof(QueryBuilder).Assembly.GetType("NGql.Core.Features.KeyGenerator")!
            .GetMethod("GenerateUniqueKey", BindingFlags.Static | BindingFlags.NonPublic,
                null, new[] { typeof(string), typeof(ReadOnlySpan<FieldDefinition>) }, null)!
            .CreateDelegate<GenerateKey>();
        _fields = new FieldDefinition[Count];
        _fields[0] = new FieldDefinition("item");
        for (var i = 1; i < Count; i++) _fields[i] = new FieldDefinition("field", alias: $"item_{i}");
    }

    [Benchmark]
    public string Generate() => _generate("item", _fields);
}
