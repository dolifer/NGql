using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using NGql.Core.Builders;
using NGql.Core.Tests.Extensions;
using Xunit;
using static NGql.Core.Builders.QueryBuilder;

namespace NGql.Core.Tests.Issues;

/// <summary>
/// Regression tests for the (Name, deep fingerprint) sub-bucketing added to
/// <c>FieldMergeIndex</c>/<c>QueryMerger.FindMergeTarget</c> to make merge-candidate lookup
/// O(1)-average in the number of same-named-but-incompatible candidates. The central hazard: a
/// root field's fingerprint can change AFTER it was bucketed (via an in-place merge, or an
/// out-of-band mutation), and the index must never leave it undiscoverable under its new,
/// currently-correct fingerprint.
/// </summary>
public class FingerprintBucketingRegressionTests
{
    private static QueryBuilder Fragment(string name, string filter) =>
        CreateDefaultBuilder(name, MergingStrategy.MergeByFieldPath)
            .AddField("businessObjects.entity.edges.node.playerId", new Dictionary<string, object?> { ["filter"] = filter });

    [Fact]
    public void FieldReMergedWithIdenticalShape_AfterFingerprintMemoized_StillFoundByLaterInclude()
    {
        // Arrange — force a fingerprint memo onto "businessObjects" (via a divergent sibling),
        // then re-Include the EXACT same shape as F0 through QueryMerger's own in-place-merge path
        // (MergeFieldsInPlace), which nulls the memo unconditionally on every successful merge
        // into an already argument-bearing subtree (see
        // FieldDefinitionExtensions.InvalidateMergeMemoAfterChildrenMerge's "already true" branch)
        // even when the recomputed value turns out identical. This exercises QueryMerger's
        // ReindexFingerprint call on every single successful merge, not just ones where the value
        // happens to change — proving the re-keying call is safe (and a no-op) in the common case
        // where CanMergeFields's own equal-arguments contract means the fingerprint value could
        // never actually differ post-merge.
        var root = CreateDefaultBuilder("Root", MergingStrategy.MergeByFieldPath);

        root.Include(Fragment("F0", "A"));
        root.Include(Fragment("Fx", "ZZZ")); // forces a fingerprint memo onto "businessObjects"
        root.Include(Fragment("Refine", "A")); // merges into F0's root; memo nulled and re-keyed

        // Act — a further fragment matching the same shape must still be found post-merge.
        root.Include(Fragment("PostMatch", "A"));

        // Assert — F0/Refine/PostMatch all merge into "businessObjects"; Fx stays separate.
        root.DefinitionsCount.Should().Be(2);
        var rendered = root.ToString();
        rendered.Should().NotContain("businessObjects_2");
    }

    [Fact]
    public void InterleavedOutOfBandAddFieldAndPreserve_BetweenIncludes_KeepsBucketingCorrect()
    {
        // Arrange — mix plain AddField (out-of-band root-value mutation) and Preserve (builds an
        // entirely separate QueryDefinition/FieldMergeIndex) between Include() calls that rely on
        // fingerprint bucketing. Neither out-of-band operation should desync the root builder's
        // own index.
        var root = CreateDefaultBuilder("Root", MergingStrategy.MergeByFieldPath);

        root.Include(Fragment("F0", "A"));
        root.Include(Fragment("Fx", "ZZZ")); // forces the fingerprint memo onto "businessObjects"

        // Out-of-band: a plain AddField call that replaces the root dictionary's stored
        // FieldDefinition VALUE for a brand-new, unrelated field (does not touch "businessObjects").
        root.AddField("unrelated", new Dictionary<string, object?> { ["x"] = 1 });

        // Out-of-band: builds a fresh QueryDefinition/FieldMergeIndex entirely — must not affect
        // root's own index.
        _ = PreservationBuilder.Create(root).Preserve("unrelated").Build();

        // Out-of-band mutation that DOES touch "businessObjects" via its live reference — mutates
        // "playerId"'s filter from A to B underneath the existing "businessObjects" root.
        root.AddField("businessObjects.entity.edges.node.playerId", new Dictionary<string, object?> { ["filter"] = "B" });

        // Act — a fragment matching the POST-mutation shape must merge into the same root; the
        // original PRE-mutation shape must no longer match it.
        root.Include(Fragment("PostMatch", "B"));
        root.Include(Fragment("PreMatch", "A"));

        // Assert — F0(A)/Fx(ZZZ) start as 2 roots ("businessObjects", "businessObjects_1"); the
        // out-of-band "unrelated" AddField adds a 3rd root-level field; the out-of-band mutation
        // moves "businessObjects"'s filter from A to B, so PostMatch(B) merges into it (still 3
        // total) and PreMatch(A) no longer matches anything live, becoming a 4th root.
        root.DefinitionsCount.Should().Be(4);
        var rendered = root.ToString();
        rendered.Should().Contain("unrelated");
        rendered.Should().NotContain("businessObjects_3");
    }

    [Fact]
    public async Task DivergentThenConvergentBatch_MergesConvergentAndKeepsDivergentSeparate()
    {
        // Arrange — a batch of N divergent (all-distinct-filter) fragments, immediately followed
        // by a batch that all converge onto ONE of the earlier divergent filters. Every fragment
        // in the convergent batch must land on the SAME earlier root; none may spuriously create
        // a new one, and the still-divergent roots must remain untouched.
        var root = CreateDefaultBuilder("Root", MergingStrategy.MergeByFieldPath);

        // Divergent batch: 10 fragments, each a distinct filter -> 10 separate roots.
        for (var i = 0; i < 10; i++)
        {
            root.Include(Fragment($"Divergent{i}", $"segment{i}"));
        }

        // Convergent batch: 10 more fragments all matching "segment3"'s shape exactly -> all merge
        // into the single existing "businessObjects" root that "segment3" occupies.
        for (var i = 0; i < 10; i++)
        {
            root.Include(Fragment($"Convergent{i}", "segment3"));
        }

        // Assert — still exactly 10 root-level definitions (the original divergent batch); none
        // of the convergent batch created a new root.
        root.DefinitionsCount.Should().Be(10);
        await root.Verify();
    }

    [Fact]
    public void ConvergentThenDivergentBatch_MergesConvergentThenSeparatesDivergent()
    {
        // Arrange — inverse ordering: a convergent batch first (all merge into one root), then a
        // divergent batch (each must correctly split off as its own root) — proves the fingerprint
        // sub-bucket for the shared shape does not "leak" and wrongly absorb later, genuinely
        // different fragments once it has accumulated several merges.
        var root = CreateDefaultBuilder("Root", MergingStrategy.MergeByFieldPath);

        for (var i = 0; i < 8; i++)
        {
            root.Include(Fragment($"Convergent{i}", "shared"));
        }

        for (var i = 0; i < 8; i++)
        {
            root.Include(Fragment($"Divergent{i}", $"unique{i}"));
        }

        // Assert — 1 merged root for the convergent batch + 8 separate roots for the divergent
        // batch = 9 total.
        root.DefinitionsCount.Should().Be(9);
        var rendered = root.ToString();
        rendered.Should().Contain("playerId(filter:\"shared\")");
        for (var i = 0; i < 8; i++)
        {
            rendered.Should().Contain($"playerId(filter:\"unique{i}\")");
        }
    }
}
