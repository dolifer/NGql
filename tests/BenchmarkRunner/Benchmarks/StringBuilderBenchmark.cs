using System.Text;
using BenchmarkDotNet.Attributes;

namespace Benchmarks.Benchmarks;

[MemoryDiagnoser]
public class StringBuilderBenchmark
{
    [Benchmark]
    public string AppendChar()
        => new StringBuilder().Append('(').ToString();

    [Benchmark]
    public string AppendString()
        => new StringBuilder().Append("(").ToString();
}