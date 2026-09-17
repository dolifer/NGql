using System.Collections.Generic;
using BenchmarkDotNet.Attributes;
using NGql.Core.Abstractions;
using NGql.Core.Builders;

namespace Benchmarks.Benchmarks;

[MemoryDiagnoser]
public class BatchOperationsBenchmark
{
    private QueryBlock _variables = null!;
    private QueryBuilder _source = null!;
    private QueryBuilder _integers = null!;
    private string[] _paths = null!;

    [Params(10, 1000)]
    public int Count { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _variables = new QueryBlock("Variables");
        _variables.AddField("id");
        _source = QueryBuilder.CreateDefaultBuilder("Source");
        _paths = new string[Count];
        var values = new object[Count];
        for (var i = 0; i < Count; i++)
        {
            _variables.AddVariable($"$v{i}", "Int");
            _paths[i] = $"users.edges.node.field{i}";
            values[i] = -i;
        }
        _integers = QueryBuilder.CreateDefaultBuilder("Integers").AddField("item",
            new Dictionary<string, object?> { ["values"] = values });
    }

    [Benchmark]
    public string RootVariables() => _variables.ToString();

    [Benchmark]
    public PreservationBuilder PreservePaths() => PreservationBuilder.Create(_source).Preserve(_paths);

    [Benchmark]
    public string SignedIntegers() => _integers.ToString();
}
