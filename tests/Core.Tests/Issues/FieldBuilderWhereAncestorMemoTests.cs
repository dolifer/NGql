using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using NGql.Core.Builders;
using NGql.Core.Tests.Extensions;
using Xunit;
using static NGql.Core.Builders.QueryBuilder;

namespace NGql.Core.Tests.Issues;

public class FieldBuilderWhereAncestorMemoTests
{
    private static Dictionary<string, object?> Currency(string code) => new() { ["cur"] = code };

    [Fact]
    public void Where_OnCapturedNestedBuilder_AfterActionReturns_MergesGenuinePostMutationCandidate()
    {
        // Arrange — captures the innermost FieldBuilder from a nested Action<FieldBuilder> scope
        // and calls Where() on it AFTER the enclosing AddField calls have already returned. A
        // divergent Include first forces FindMergeTarget to memoize a deep fingerprint on the
        // "metrics" ancestor chain; only then does the out-of-band Where() mutate "deposits"'s
        // argument three levels... no, one level below "metrics" — but critically, AFTER
        // AddFieldCore's own cascading ClearMergeMemo calls already ran and returned.
        var target = CreateDefaultBuilder("T", MergingStrategy.MergeByFieldPath);
        target.AddField("metrics.deposits", Currency("USD"));

        FieldBuilder? deep = null;
        target.AddField("metrics", m => m.AddField("deposits", d => { deep = d; }));

        target.Include(CreateDefaultBuilder("F0", MergingStrategy.MergeByFieldPath)
            .AddField("metrics.deposits", Currency("USD"))); // forces the fingerprint memo onto "metrics"

        // Act — mutate "deposits"'s argument out-of-band via the captured, since-detached builder.
        deep!.Where("cur", "EUR");
        target.Include(CreateDefaultBuilder("F2", MergingStrategy.MergeByFieldPath)
            .AddField("metrics.deposits", Currency("EUR"))); // genuine match against the post-mutation shape

        // Assert — a stale ancestor fingerprint memo would falsely split F2 into a second
        // "metrics_1" root instead of merging into the single mutated "metrics" root.
        target.DefinitionsCount.Should().Be(1);
        var rendered = target.ToString();
        rendered.Should().NotContain("metrics_1");
        rendered.Should().Contain("cur:\"EUR\"");
    }

    [Fact]
    public async Task Where_OnCapturedNestedBuilder_AfterActionReturns_Snapshot()
    {
        // Arrange — the exact reported repro, captured as a Verify() snapshot.
        var target = CreateDefaultBuilder("T", MergingStrategy.MergeByFieldPath);
        target.AddField("metrics.deposits", Currency("USD"));

        FieldBuilder? deep = null;
        target.AddField("metrics", m => m.AddField("deposits", d => { deep = d; }));
        target.Include(CreateDefaultBuilder("F0", MergingStrategy.MergeByFieldPath)
            .AddField("metrics.deposits", Currency("USD")));

        // Act
        deep!.Where("cur", "EUR");
        target.Include(CreateDefaultBuilder("F2", MergingStrategy.MergeByFieldPath)
            .AddField("metrics.deposits", Currency("EUR")));

        // Assert
        await target.Verify();
    }

    [Fact]
    public void Where_OnCapturedDoublyNestedBuilder_ClearsEntireAncestorChain()
    {
        // Arrange — three levels of nesting (a.b.c), capturing the innermost builder for "c" so
        // BOTH ancestors ("a" and "b") must have their memoized fingerprints cleared, not just the
        // immediate parent.
        var target = CreateDefaultBuilder("T", MergingStrategy.MergeByFieldPath);
        target.AddField("a.b.c", Currency("USD"));

        FieldBuilder? deep = null;
        target.AddField("a", a => a.AddField("b", b => b.AddField("c", c => { deep = c; })));

        target.Include(CreateDefaultBuilder("F0", MergingStrategy.MergeByFieldPath)
            .AddField("a.b.c", Currency("USD"))); // forces fingerprint memos onto "a" and "b"

        // Act
        deep!.Where("cur", "EUR");
        target.Include(CreateDefaultBuilder("F2", MergingStrategy.MergeByFieldPath)
            .AddField("a.b.c", Currency("EUR")));

        // Assert
        target.DefinitionsCount.Should().Be(1);
        target.ToString().Should().NotContain("a_1");
    }
}
