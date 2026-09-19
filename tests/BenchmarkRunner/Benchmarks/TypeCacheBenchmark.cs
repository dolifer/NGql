using BenchmarkDotNet.Attributes;
using NGql.Core.Builders;

namespace Benchmarks.Benchmarks;

[MemoryDiagnoser]
public class TypeCacheBenchmark
{
    [Params("String", "CustomSchemaObject")]
    public string Type { get; set; } = null!;

    [GlobalSetup]
    public void Setup() => _ = CreateField();

    [Benchmark]
    public QueryBuilder CreateField() => QueryBuilder.CreateDefaultBuilder("Q").AddField("item", Type);
}
