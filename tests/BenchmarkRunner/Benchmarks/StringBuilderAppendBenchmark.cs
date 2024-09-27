using System.Text;
using BenchmarkDotNet.Attributes;

namespace Benchmarks.Benchmarks;

[MemoryDiagnoser]
public class StringBuilderAppendBenchmark
{
    private readonly string padding = new(' ', 4);

    [Benchmark]
    public string AppendConcat()
        => new StringBuilder().Append(padding + '}').ToString();

    [Benchmark]
    public string AppendFluentString()
        => new StringBuilder().Append(padding).Append('}').ToString();
}