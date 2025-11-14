using System.Collections.Generic;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Jobs;
using NGql.Core.Builders;

namespace Benchmarks.Benchmarks;

/// <summary>
/// Compares performance between local (current) and published package versions
/// Run with: dotnet run -c Release -- --filter "*VersionComparison*"
/// </summary>
[Config(typeof(Config))]
[MemoryDiagnoser]
public class VersionComparisonBenchmark
{
    private sealed class Config : ManualConfig
    {
#pragma warning disable S1144
#pragma warning disable CS0618 // Type or member is obsolete
        public Config()
        {
            // Compare local version vs published version
            AddJob(Job.Default.WithId("Local"));

            AddJob(Job.Default
                .WithNuGet("NGql.Core", "1.5.0")
                .WithId("Published")
                .AsBaseline()
            );

            AddColumn(StatisticColumn.P95);
            AddDiagnoser(MemoryDiagnoser.Default);
            WithOption(ConfigOptions.JoinSummary, true);
            WithOption(ConfigOptions.DisableOptimizationsValidator, true);
        }
#pragma warning restore CS0618 // Type or member is obsolete
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
}
