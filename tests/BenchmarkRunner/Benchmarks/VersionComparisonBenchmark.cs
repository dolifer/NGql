using System.Collections.Generic;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Jobs;
using NGql.Core;
using NGql.Core.Builders;

namespace Benchmarks.Benchmarks;

/// <summary>
/// Benchmark suite shared by BenchmarkRunner (local ProjectRef) and BenchmarkRunner.Published (NGql.Core NuGet).
/// Run both, then use BenchmarkMerger to get a side-by-side comparison.
///
/// Local:     dotnet run --project tests/BenchmarkRunner -c Release -- --filter "*VersionComparison*SimpleQuery*" --artifacts artifacts/benchmarks/local
/// Published: dotnet run --project tests/BenchmarkRunner.Published -c Release -- --filter "*VersionComparison*SimpleQuery*" --artifacts artifacts/benchmarks/published
/// Merge:     dotnet run --project tests/BenchmarkMerger -- artifacts/benchmarks/local artifacts/benchmarks/published
/// </summary>
[Config(typeof(Config))]
[MemoryDiagnoser]
public class VersionComparisonBenchmark
{
    private sealed class Config : ManualConfig
    {
#pragma warning disable S1144
        public Config()
        {
            AddJob(Job.ShortRun);
            AddColumn(StatisticColumn.P95);
            AddDiagnoser(MemoryDiagnoser.Default);
        }
#pragma warning restore S1144
    }

    [Benchmark]
    public string SimpleQuery()
    {
        var query = QueryBuilder
            .CreateDefaultBuilder("GetUsers")
            .AddField("users.name")
            .AddField("users.email");
        return query.ToString();
    }
    
    [Benchmark]
    public string TypeDriftScenario()
    {
        // Test the type drift bug fix
        var query = QueryBuilder
            .CreateDefaultBuilder("TypeTest")
            .AddField("String user.name")
            .AddField("user.profile.bio");
        return query.ToString();
    }
    
    [Benchmark]
    public string CaseInsensitiveFields()
    {
        // Test case-insensitive improvements
        var query = QueryBuilder
            .CreateDefaultBuilder("CaseTest")
            .AddField("user.name")
            .AddField("USER.email")  // Different case
            .AddField("User.id");    // Different case
        return query.ToString();
    }
    
    [Benchmark]
    public string ComplexQueryWithMerging()
    {
        var fragment1 = QueryBuilder
            .CreateDefaultBuilder("Fragment1")
            .AddField("user.profile.name")
            .AddField("user.profile.avatar");
    
        var fragment2 = QueryBuilder
            .CreateDefaultBuilder("Fragment2")
            .AddField("user.posts.title")
            .AddField("user.posts.publishedAt");
    
        var combined = QueryBuilder
            .CreateDefaultBuilder("Combined")
            .Include(fragment1)
            .Include(fragment2);
    
        return combined.ToString();
    }
    
    [Benchmark]
    public string ArrayTypePreservation()
    {
        // Test array type marker preservation
        var query = QueryBuilder
            .CreateDefaultBuilder("ArrayTest")
            .AddField("[] tags")
            .AddField("Post[] posts")
            .AddField("posts.title");
        return query.ToString();
    }
    
    [Benchmark]
    [Arguments(10)]
    [Arguments(100)]
    public void BulkQueryBuilding(int iterations)
    {
        for (int i = 0; i < iterations; i++)
        {
            var query = QueryBuilder
                .CreateDefaultBuilder($"Query{i}")
                .AddField("users.name")
                .AddField("users.email")
                .AddField("users.profile.bio");
            _ = query.ToString();
        }
    }
    
    [Benchmark]
    public string DeepNestedFields()
    {
        var query = QueryBuilder
            .CreateDefaultBuilder("DeepNest")
            .AddField("user.profile.settings.privacy.notifications.email.frequency")
            .AddField("user.profile.settings.privacy.notifications.push.enabled")
            .AddField("user.profile.settings.theme.colors.primary");
        return query.ToString();
    }
    
    [Benchmark]
    [Arguments(50)]
    [Arguments(200)]
    public void SpanPathOptimization(int fieldCount)
    {
        var builder = QueryBuilder.CreateDefaultBuilder("SpanTest");
        for (int i = 0; i < fieldCount; i++)
        {
            builder.AddField($"field{i}.nested.value");
        }
        _ = builder.ToString();
    }
    
    [Benchmark]
    public void ArgumentsPoolStress()
    {
        var queries = new List<QueryBuilder>();
        for (int i = 0; i < 30; i++)
        {
            queries.Add(QueryBuilder
                .CreateDefaultBuilder($"Pool{i}")
                .AddField("user.name"));
        }
        queries.ForEach(q => q.ToString());
    }
    
    [Benchmark]
    public string TypeDriftEdgeCases()
    {
        return QueryBuilder
            .CreateDefaultBuilder("EdgeCase")
            .AddField("String user.name")      // Explicit type
            .AddField("user.name")             // Implicit type
            .AddField("Int user.age")          // Different type
            .AddField("user.age")              // Should preserve Int
            .ToString();
    }
    
    [Benchmark]
    [Arguments(10)]
    [Arguments(30)]
    public string NestedClassicQuery(int depth)
    {
        // Classic (Query/QueryBlock) API with deep sub-query nesting — exercises the
        // QueryTextBuilder.Build recursion and the string-field insert path.
        var root = new Query("root");
        var current = root;
        for (int i = 0; i < depth; i++)
        {
            var child = new Query("level" + i);
            child.Select("alpha", "beta", "gamma");
            current.Select(child);
            current = child;
        }
        return root.ToString();
    }

    [Benchmark]
    [Arguments(100)]
    [Arguments(500)]
    public string MassiveFieldCount(int fieldCount)
    {
        var builder = QueryBuilder.CreateDefaultBuilder("Massive");
        for (int i = 0; i < fieldCount; i++)
        {
            builder.AddField($"field{i}");
        }
        return builder.ToString();
    }

    [Benchmark]
    [Arguments(10)]
    [Arguments(50)]
    public string DictionaryArgumentRendering(int iterations)
    {
        // Exercises the dictionary/nested-object argument render path (reflection + boxing
        // removal). Each iteration renders a field whose argument is a multi-entry dictionary
        // with a nested dictionary, then re-renders to hit the render path repeatedly.
        string result = string.Empty;
        for (int i = 0; i < iterations; i++)
        {
            var query = QueryBuilder
                .CreateDefaultBuilder("DictArgs")
                .AddField("users", new Dictionary<string, object?>
                {
                    ["filter"] = new Dictionary<string, object?>
                    {
                        ["status"] = "active",
                        ["role"] = "admin",
                        ["tier"] = "gold",
                    },
                    ["limit"] = 25,
                    ["offset"] = 0,
                    ["sort"] = "name",
                });
            result = query.ToString();
        }
        return result;
    }

    [Benchmark]
    [Arguments(10)]
    [Arguments(50)]
    public string PreserveFromExpressionRepeated(int iterations)
    {
        // Exercises PreserveFromExpression<T> repeatedly (per-request pattern): navigation-property
        // reflection caching and the deferred parameter-type-map allocation. Each call re-walks the
        // same type, so a per-type reflection cache turns N reflected lookups into one.
        var full = QueryBuilder
            .CreateDefaultBuilder("Profile")
            .AddField("user.profile.name")
            .AddField("user.profile.bio")
            .AddField("user.profile.age");

        string result = string.Empty;
        for (int i = 0; i < iterations; i++)
        {
            var filtered = PreservationBuilder
                .Create(full)
                .PreserveFromExpression<BenchUser>(u => u.profile.name != null && u.profile.age > 18)
                .Build();
            result = filtered.ToString();
        }
        return result;
    }

    private static QueryBuilder BuildModerateQuery()
        => QueryBuilder
            .CreateDefaultBuilder("RenderSink")
            .AddField("user.profile.name")
            .AddField("user.profile.email")
            .AddField("user.profile.avatar")
            .AddField("user.posts.title")
            .AddField("user.posts.publishedAt")
            .AddField("user.settings.privacy.notifications");

    [Benchmark(Baseline = true)]
    [Arguments(10)]
    [Arguments(50)]
    public int ToStringPerCall(int iterations)
    {
        // Baseline: each render allocates a fresh string via ToString().
        var query = BuildModerateQuery();
        int total = 0;
        for (int i = 0; i < iterations; i++)
        {
            total += query.ToString().Length;
        }
        return total;
    }

#if !NGQL_PUBLISHED
    [Benchmark]
    [Arguments(10)]
    [Arguments(50)]
    public int AppendToReusedBuilder(int iterations)
    {
        // Render into one reused StringBuilder — no per-call intermediate string allocation.
        var query = BuildModerateQuery();
        var sb = new System.Text.StringBuilder();
        int total = 0;
        for (int i = 0; i < iterations; i++)
        {
            sb.Clear();
            query.AppendTo(sb);
            total += sb.Length;
        }
        return total;
    }
#endif

    [Benchmark]
    [Arguments(10)]
    [Arguments(50)]
    public int Utf8ViaToStringGetBytes(int iterations)
    {
        // Status-quo two-allocation path: ToString() allocates a string, then GetBytes allocates a
        // byte[] per iteration. Baseline for the WriteUtf8 comparison.
        var query = BuildModerateQuery();
        int total = 0;
        for (int i = 0; i < iterations; i++)
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes(query.ToString());
            total += bytes.Length;
        }
        return total;
    }

#if !NGQL_PUBLISHED
    [Benchmark]
    [Arguments(10)]
    [Arguments(50)]
    public int Utf8ViaWriteUtf8(int iterations)
    {
        // Transcode straight to UTF-8 into one reused ArrayBufferWriter — no per-call intermediate
        // string and no per-call byte[] allocation.
        var query = BuildModerateQuery();
        var writer = new System.Buffers.ArrayBufferWriter<byte>();
        int total = 0;
        for (int i = 0; i < iterations; i++)
        {
            writer.Clear();
            query.WriteUtf8(writer);
            total += writer.WrittenCount;
        }
        return total;
    }
#endif

#if !NGQL_PUBLISHED
    [Benchmark]
    [Arguments(10)]
    [Arguments(50)]
    public string DirectivesRendering(int iterations)
    {
        // Exercises the new directive render path: @include/@skip on an object field plus a
        // generic @format on a leaf. Guarded for NGQL_PUBLISHED because Include/Skip/Directive
        // do not exist in NGql.Core 2.0.0 (the published-runner reference).
        string result = string.Empty;
        for (int i = 0; i < iterations; i++)
        {
            var query = QueryBuilder
                .CreateDefaultBuilder("Directives")
                .AddField("user", u => u
                    .Include("$show")
                    .Skip("$hide")
                    .AddField("name", n => n.Directive("format", new Dictionary<string, object?> { ["as"] = "ISO8601" }))
                    .AddField("email"));
            result = query.ToString();
        }
        return result;
    }
#endif

    public sealed class BenchProfile
    {
        public string name { get; set; } = string.Empty;
        public string bio { get; set; } = string.Empty;
        public int age { get; set; }
    }

    public sealed class BenchUser
    {
        public BenchProfile profile { get; set; } = new();
    }
}
