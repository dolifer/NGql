using BenchmarkDotNet.Attributes;
using NGql.Core.Abstractions;

namespace Benchmarks.Benchmarks;

[MemoryDiagnoser]
public class BlockArgumentRenderingBenchmark
{
    private QueryBlock _arguments = null!;
    private QueryBlock _variables = null!;

    [Params(1, 10, 1000)]
    public int Count { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _arguments = new QueryBlock("Q");
        _variables = new QueryBlock("Q");
        _arguments.AddField("id");
        _variables.AddField("id");
        for (var i = Count - 1; i >= 0; i--)
        {
            _arguments.AddArgument($"argument{i:D4}", i);
            _variables.AddVariable($"$variable{i:D4}", "Int");
        }
    }

    [Benchmark]
    public string Arguments() => _arguments.ToString();

    [Benchmark]
    public string Variables() => _variables.ToString();
}
