using System.Linq;
using FluentAssertions;
using NGql.Core;
using NGql.Core.Builders;
using Xunit;

namespace NGql.Core.Tests.Issues;

/// <summary>
/// Regression: <c>QueryBuilder.Include(source)</c> must not mutate the source builder.
///
/// Internally the merge stores incoming field references directly under the target's root
/// dictionary on the first Include for a given path; if a second Include then merges into
/// that path, the in-place merge propagates new fields back into the original source builder
/// because both share the same <see cref="NGql.Core.Abstractions.FieldDefinition"/> instances.
///
/// Production callers iterate every source builder afterwards (e.g. to build flatten lookups);
/// any leakage produces wrong-path lookups and silently dropped subtrees in the final query.
/// </summary>
public class IncludeMutatesSourceTests
{
    [Fact]
    public void Include_TwoSourcesShareRootPath_FirstSourceUnchangedAfterSecondInclude()
    {
        var first = QueryBuilder.CreateDefaultBuilder("First", MergingStrategy.MergeByFieldPath)
            .AddField("businessObjects.playerProfile.edges.node.Session:session.LastLogin:lastLogin.Date:date");

        var second = QueryBuilder.CreateDefaultBuilder("Second", MergingStrategy.MergeByFieldPath)
            .AddField("businessObjects.playerProfile.edges.node.Deposit:deposit.FirstDepositAmount:firstDepositAmount");

        var firstBefore = first.ToString();

        var root = QueryBuilder.CreateDefaultBuilder("Root", MergingStrategy.MergeByFieldPath);
        root.Include(first);
        root.Include(second);

        var firstAfter = first.ToString();
        firstAfter.Should().Be(firstBefore, "Include() must not mutate the source builder");
    }

    [Fact]
    public void Include_ManySources_NoneIsMutated()
    {
        // Build N independent sources that all share the same root path so the merge target
        // ends up funneled into the same root dictionary entry.
        var sources = Enumerable.Range(0, 5)
            .Select(i => QueryBuilder.CreateDefaultBuilder($"Source{i}", MergingStrategy.MergeByFieldPath)
                .AddField($"shared.root.field{i}"))
            .ToArray();

        var snapshots = sources.Select(s => s.ToString()).ToArray();

        var root = QueryBuilder.CreateDefaultBuilder("Root", MergingStrategy.MergeByFieldPath);
        foreach (var s in sources)
            root.Include(s);

        for (int i = 0; i < sources.Length; i++)
        {
            sources[i].ToString().Should().Be(snapshots[i],
                $"source #{i} must remain unchanged after Include() chain");
        }
    }

    [Fact]
    public void Include_SourceWithNestedArguments_NotMutatedByArgumentMerge()
    {
        // Arguments dictionaries are mutable reference types. If the merge shares the inner
        // dictionary instance, a later argument-merge into the target could leak back into
        // the source's argument tree.
        var nested = new System.Collections.Generic.Dictionary<string, object?> { ["eq"] = 5 };
        var args = new System.Collections.Generic.Dictionary<string, object?> { ["where"] = nested };

        var source = QueryBuilder.CreateDefaultBuilder("Source", MergingStrategy.MergeByFieldPath);
        source.AddField("users", args, new[] { "id", "name" });

        var sourceSnapshot = source.ToString();

        // Include into root, then add a sibling that triggers an argument-level merge.
        var root = QueryBuilder.CreateDefaultBuilder("Root", MergingStrategy.MergeByFieldPath).Include(source);
        var sibling = QueryBuilder.CreateDefaultBuilder("Sibling", MergingStrategy.MergeByFieldPath);
        var siblingNested = new System.Collections.Generic.Dictionary<string, object?> { ["gt"] = 0 };
        var siblingArgs = new System.Collections.Generic.Dictionary<string, object?> { ["where"] = siblingNested };
        sibling.AddField("users", siblingArgs, new[] { "id", "name" });
        root.Include(sibling);

        source.ToString().Should().Be(sourceSnapshot, "argument-merge must not mutate the source");
    }

    [Fact]
    public void Include_SourceUsedTwice_StaysIndependent()
    {
        // Including the same source into two different roots should also keep it untouched.
        var source = QueryBuilder.CreateDefaultBuilder("Source", MergingStrategy.MergeByFieldPath)
            .AddField("a.b.c");

        var snapshot = source.ToString();

        var rootA = QueryBuilder.CreateDefaultBuilder("RootA", MergingStrategy.MergeByFieldPath).Include(source);
        var rootB = QueryBuilder.CreateDefaultBuilder("RootB", MergingStrategy.MergeByFieldPath).Include(source);

        // Mutate rootA via a second Include that shares the path
        var sibling = QueryBuilder.CreateDefaultBuilder("Sibling", MergingStrategy.MergeByFieldPath)
            .AddField("a.b.d");
        rootA.Include(sibling);

        source.ToString().Should().Be(snapshot, "source must not be affected by mutations on either root");
        rootB.ToString().Should().NotContain("d", "rootB must not see fields from rootA's second Include");
    }
}
