using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using NGql.Core.Builders;

namespace Benchmarks.Benchmarks;

// Cycles through more custom type names than the cache retains while one name stays in use.
[MemoryDiagnoser]
public class TypeCacheChurnBenchmark
{
    private const int DistinctTypes = 6000;
    private string[] _types = null!;

    [Params(1, 4)]
    public int Threads { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _types = new string[DistinctTypes];
        for (var i = 0; i < _types.Length; i++) _types[i] = "ChurnType" + i;
    }

    [Benchmark]
    public void CycleTypes()
    {
        if (Threads == 1)
        {
            Cycle();
            return;
        }

        Parallel.For(0, Threads, _ => Cycle());
    }

    private void Cycle()
    {
        foreach (var type in _types)
        {
            QueryBuilder.CreateDefaultBuilder("Q").AddField("item", type).AddField("hot", "HotSchemaObject");
        }
    }
}
