using System.Collections.Generic;
using System.Linq;
using BenchmarkDotNet.Attributes;
using NGql.Core;
using NGql.Core.Builders;

namespace Benchmarks.Benchmarks;

/// <summary>
/// Paths changed after 2.2.0 that the release comparison does not isolate: root-field lookup,
/// argument re-adds, merge-index bookkeeping and string-path preservation. Public API only, so
/// the same file runs against 2.2.0 for a baseline.
/// </summary>
[MemoryDiagnoser]
public class PostReleaseBenchmark
{
    private static readonly Dictionary<string, object?> FirstArguments = new()
    {
        ["first"] = 10, ["after"] = "abc", ["orderBy"] = "name", ["status"] = "active",
    };

    private static readonly Dictionary<string, object?> SecondArguments = new() { ["first"] = 20, ["where"] = "x" };

    private string[] _roots = null!;
    private QueryBuilder[] _divergent = null!;
    private QueryBuilder[] _plain = null!;
    private QueryBuilder _left = null!;
    private QueryBuilder _right = null!;
    private QueryBuilder _full = null!;

    [GlobalSetup]
    public void Setup()
    {
        _roots = Enumerable.Range(0, 1000).Select(i => "root" + i).ToArray();
        _divergent = Enumerable.Range(0, 800)
            .Select(i => QueryBuilder.CreateDefaultBuilder("F" + i, MergingStrategy.MergeByFieldPath)
                .AddField("metrics.value", new Dictionary<string, object?> { ["filter"] = i }))
            .ToArray();
        _plain = Enumerable.Range(0, 200).Select(i => QueryBuilder.CreateDefaultBuilder("P" + i).AddField("user.id")).ToArray();
        _left = QueryBuilder.CreateDefaultBuilder("Q1").AddField("user.name").AddField("user.email");
        _right = QueryBuilder.CreateDefaultBuilder("Q2").AddField("user.name").AddField("user.age");
        _full = QueryBuilder.CreateDefaultBuilder("Full")
            .AddField("user.name").AddField("user.email").AddField("user.ssn")
            .AddField("user.profile.avatar").AddField("user.profile.bio");
    }

    [Benchmark]
    public QueryBuilder RootsWithLambda()
    {
        var query = QueryBuilder.CreateDefaultBuilder("Roots");
        foreach (var root in _roots) query.AddField(root, field => field.AddField("id"));
        return query;
    }

    [Benchmark]
    public QueryBuilder RootsWithArguments()
    {
        var query = QueryBuilder.CreateDefaultBuilder("Roots");
        foreach (var root in _roots) query.AddField(root, FirstArguments);
        return query;
    }

    [Benchmark]
    public QueryBuilder ReAddWithArguments()
        => QueryBuilder.CreateDefaultBuilder("Q")
            .AddField("users", FirstArguments).AddField("users", SecondArguments)
            .AddField("orders", FirstArguments).AddField("orders", SecondArguments);

    [Benchmark]
    public QueryBuilder MergeByFieldPathTwoFragments()
        => QueryBuilder.CreateDefaultBuilder("C", MergingStrategy.MergeByFieldPath).Include(_left).Include(_right);

    [Benchmark]
    public QueryBuilder DivergentFilterMerge()
    {
        var query = QueryBuilder.CreateDefaultBuilder("T", MergingStrategy.MergeByFieldPath);
        foreach (var fragment in _divergent) query.Include(fragment);
        return query;
    }

    [Benchmark]
    public QueryBuilder NeverMergeIncludes()
    {
        var query = QueryBuilder.CreateDefaultBuilder("T", MergingStrategy.NeverMerge);
        foreach (var fragment in _plain) query.Include(fragment);
        return query;
    }

    [Benchmark]
    public QueryBuilder PreservePaths()
        => PreservationBuilder.Create(_full).Preserve("user.name", "user.profile.avatar").Build();

    [Benchmark]
    public string ClassicSelectList()
    {
        var root = new Query("root");
        root.Select("gamma", "alpha", "beta");
        return root.ToString();
    }
}
