using System;
using System.Collections.Generic;
using System.Reflection;
using BenchmarkDotNet.Attributes;
using NGql.Core.Builders;

namespace Benchmarks.Benchmarks;

[MemoryDiagnoser]
public class AliasCollisionBenchmark
{
    private Func<string, IEnumerable<string>, string> _generate = null!;
    private string[] _keys = null!;

    [Params(10, 1000)]
    public int Count { get; set; }

    [GlobalSetup]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Major Code Smell", "S3011",
        Justification = "Benchmark-only delegate binding avoids changing production API or timing reflection invocation.")]
    public void Setup()
    {
        _generate = typeof(QueryBuilder).Assembly.GetType("NGql.Core.Features.KeyGenerator")!
            .GetMethod("GenerateUniqueKey", BindingFlags.Static | BindingFlags.NonPublic,
                null, new[] { typeof(string), typeof(IEnumerable<string>) }, null)!
            .CreateDelegate<Func<string, IEnumerable<string>, string>>();
        _keys = new string[Count];
        _keys[0] = "item";
        for (var i = 1; i < Count; i++) _keys[i] = $"item_{i}";
    }

    [Benchmark]
    public string Generate() => _generate("item", _keys);
}
