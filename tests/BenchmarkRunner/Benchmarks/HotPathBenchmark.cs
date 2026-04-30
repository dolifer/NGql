using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Jobs;
using NGql.Core;
using NGql.Core.Builders;

namespace Benchmarks.Benchmarks;

[Config(typeof(Config))]
[MemoryDiagnoser]
public class HotPathBenchmark
{
    private sealed class Config : ManualConfig
    {
#pragma warning disable S1144
        public Config()
        {
            AddJob(Job.ShortRun);
            AddDiagnoser(MemoryDiagnoser.Default);
        }
#pragma warning restore S1144
    }

    private QueryBuilder _smallTreeBuilder = null!;
    private QueryBuilder _largeIncludeBuilder = null!;
    private QueryBuilder[] _fragments = null!;

    [Params(10, 100, 1000)]
    public int IncludeCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _smallTreeBuilder = BuildSmallTree();
        _fragments = new QueryBuilder[IncludeCount];
        for (int i = 0; i < IncludeCount; i++)
        {
            _fragments[i] = QueryBuilder.CreateDefaultBuilder($"Fragment{i}")
                .AddField($"users.edges.node.field{i}");
        }
        _largeIncludeBuilder = BuildLargeIncludeTree();
    }

    private static QueryBuilder BuildSmallTree() =>
        QueryBuilder.CreateDefaultBuilder("Q")
            .AddField("users.edges.node.id")
            .AddField("users.edges.node.profile.name")
            .AddField("users.edges.node.profile.email")
            .AddField("users.edges.cursor")
            .AddField("users.pageInfo.endCursor")
            .AddField("users.pageInfo.hasNextPage");

    private QueryBuilder BuildLargeIncludeTree()
    {
        var root = QueryBuilder.CreateDefaultBuilder("Root", MergingStrategy.MergeByFieldPath);
        for (int i = 0; i < IncludeCount; i++)
        {
            root.Include(_fragments[i]);
        }
        return root;
    }

    // ──────────────────────────────────────────────────────────────────────
    // AddField traversal: dotted-path lookup cost as the tree grows.
    // ──────────────────────────────────────────────────────────────────────

    [Benchmark]
    public QueryBuilder AddField_ManyDottedPaths()
    {
        var builder = QueryBuilder.CreateDefaultBuilder("Bench");
        for (int i = 0; i < IncludeCount; i++)
        {
            builder.AddField($"users.edges.node.field{i}");
        }
        return builder;
    }

    [Benchmark]
    public QueryBuilder AddField_RepeatedExistingPath()
    {
        // Re-adds same path many times — exercises the existing-field traversal repeatedly.
        var builder = QueryBuilder.CreateDefaultBuilder("Bench")
            .AddField("users.edges.node.profile.name");
        for (int i = 0; i < IncludeCount; i++)
        {
            builder.AddField("users.edges.node.profile.email");
        }
        return builder;
    }

    // ──────────────────────────────────────────────────────────────────────
    // Include cost — DFS path index population on every merged builder.
    // ──────────────────────────────────────────────────────────────────────

    [Benchmark]
    public QueryBuilder Include_ManyFragments()
    {
        var root = QueryBuilder.CreateDefaultBuilder("Root", MergingStrategy.MergeByFieldPath);
        for (int i = 0; i < _fragments.Length; i++)
        {
            root.Include(_fragments[i]);
        }
        return root;
    }

    // ──────────────────────────────────────────────────────────────────────
    // GetPathTo — the DFS-from-root call tested in isolation.
    // ──────────────────────────────────────────────────────────────────────

    [Benchmark]
    public string[] GetPathTo_ShallowNode()
    {
        return _smallTreeBuilder.GetPathTo("Q", "edges");
    }

    [Benchmark]
    public string[] GetPathTo_DeepNode()
    {
        return _smallTreeBuilder.GetPathTo("Q", "edges.node.profile.name");
    }

    [Benchmark]
    public string[] GetPathTo_RootOnly()
    {
        return _smallTreeBuilder.GetPathTo("Q");
    }

    [Benchmark]
    public string[] GetPathTo_AfterLargeMerge_DeepNode()
    {
        // Cached call: BenchmarkDotNet's looped invocations all hit the path-index cache after iter #1.
        return _largeIncludeBuilder.GetPathTo("Root", "edges.node.field0");
    }

    [Benchmark]
    public string[] GetPathTo_FreshTree_FirstCall_DeepNode()
    {
        // Build a fresh tree each invocation so the first GetPathTo call pays full DFS cost.
        var b = QueryBuilder.CreateDefaultBuilder("Q")
            .AddField("users.edges.node.id")
            .AddField("users.edges.node.profile.name")
            .AddField("users.edges.node.profile.email")
            .AddField("users.edges.cursor")
            .AddField("users.pageInfo.endCursor");
        return b.GetPathTo("Q", "edges.node.profile.email");
    }

    /// <summary>
    /// Isolates the BuildPathToNode DFS cost. The IterationSetup attribute rebuilds the tree before each
    /// iteration so the path-index cache is cold; the iteration itself runs many GetPathTo calls so the
    /// per-call cost dominates the per-iteration tree build.
    /// </summary>
    private QueryBuilder _coldTree = null!;

    [IterationSetup(Target = nameof(GetPathTo_VariedPaths_ColdCache))]
    public void SetupColdTree()
    {
        _coldTree = QueryBuilder.CreateDefaultBuilder("Q")
            .AddField("users.edges.node.id")
            .AddField("users.edges.node.profile.name")
            .AddField("users.edges.node.profile.email")
            .AddField("users.edges.cursor")
            .AddField("users.pageInfo.endCursor")
            .AddField("users.pageInfo.hasNextPage");
    }

    [Benchmark(OperationsPerInvoke = 10)]
    public int GetPathTo_VariedPaths_ColdCache()
    {
        var paths = _variedPaths;
        var builder = _coldTree;
        var totalLen = 0;
        for (int i = 0; i < paths.Length; i++)
        {
            var result = builder.GetPathTo("Q", paths[i]);
            totalLen += result.Length;
        }
        return totalLen;
    }

    private static readonly string[] _variedPaths = new[]
    {
        "edges",
        "edges.node",
        "edges.node.id",
        "edges.node.profile",
        "edges.node.profile.name",
        "edges.node.profile.email",
        "edges.cursor",
        "pageInfo",
        "pageInfo.endCursor",
        "pageInfo.hasNextPage",
    };

    // ──────────────────────────────────────────────────────────────────────
    // Consumer-shape benchmark: mimics GetQueryAndVariables — Include many trait builders
    // into a root, GetPathTo for each, then PreserveAtPath multiple times per trait.
    // ──────────────────────────────────────────────────────────────────────

    private QueryBuilder[] _traitBuilders = null!;

    [GlobalSetup(Target = nameof(ConsumerShape_BatchRequest))]
    public void SetupConsumerShape()
    {
        // Each "trait" has a typical shape: TraitName:businessObjects.someEntity.edges.node.[fields...]
        const int traitCount = 60; // matches real-world usage
        _traitBuilders = new QueryBuilder[traitCount];
        for (int i = 0; i < traitCount; i++)
        {
            var traitName = $"Trait{i}";
            _traitBuilders[i] = QueryBuilder.CreateDefaultBuilder(traitName, MergingStrategy.MergeByFieldPath)
                .AddField($"{traitName}:businessObjects.entity.edges.node.playerId")
                .AddField($"{traitName}:businessObjects.entity.edges.node.field{i}A")
                .AddField($"{traitName}:businessObjects.entity.edges.node.field{i}B");
        }
    }

    [Benchmark]
    public string ConsumerShape_BatchRequest()
    {
        var root = QueryBuilder.CreateDefaultBuilder("BatchQuery", MergingStrategy.MergeByFieldPath);
        for (int i = 0; i < _traitBuilders.Length; i++)
        {
            root.Include(_traitBuilders[i]);
            // Mirror the GqlExtensions code path: GetPathTo("edges") for each included trait.
            _ = root.GetPathTo($"Trait{i}", "edges");
        }

        // Mirror PreservationBuilder usage from FlattenPreservationExtensions: a few PreserveAtPath
        // calls per trait against the same nodePath ("edges.node") — cache hits dominate after the first.
        var preserve = PreservationBuilder.Create(root);
        for (int i = 0; i < _traitBuilders.Length; i++)
        {
            preserve.PreserveAtPath("playerId", "edges.node");
            preserve.PreserveAtPath($"field{i}A", "edges.node");
        }

        return preserve.Build().ToString();
    }
}
