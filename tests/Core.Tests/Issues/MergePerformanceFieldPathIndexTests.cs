using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using NGql.Core.Builders;
using NGql.Core.Tests.Extensions;
using Xunit;
using static NGql.Core.Builders.QueryBuilder;

namespace NGql.Core.Tests.Issues;

public class MergePerformanceFieldPathIndexTests
{
    private static QueryBuilder DivergentFragment(int index) =>
        CreateDefaultBuilder($"Fragment{index}", MergingStrategy.MergeByFieldPath)
            .AddField("businessObjects.entity.edges.node.playerId", new Dictionary<string, object?> { ["filter"] = $"segment{index}" });

    private static QueryBuilder IdenticalFragment(int index) =>
        CreateDefaultBuilder($"Fragment{index}", MergingStrategy.MergeByFieldPath)
            .AddField("businessObjects.entity.edges.node.playerId", new Dictionary<string, object?> { ["filter"] = "sharedFilter" });

    [Fact]
    public async Task Include_DivergentFilterFragments_ProducesSeparateAliasedRoots()
    {
        // Arrange
        var root = CreateDefaultBuilder("Root", MergingStrategy.MergeByFieldPath);

        // Act
        for (var i = 0; i < 5; i++)
        {
            root.Include(DivergentFragment(i));
        }

        // Assert
        await root.Verify();
    }

    [Fact]
    public void Include_IdenticalFilterFragments_MergeIntoOneRoot()
    {
        // Arrange
        var root = CreateDefaultBuilder("Root", MergingStrategy.MergeByFieldPath);

        // Act
        for (var i = 0; i < 25; i++)
        {
            root.Include(IdenticalFragment(i));
        }

        // Assert
        root.DefinitionsCount.Should().Be(1);
        var rendered = root.ToString();
        rendered.Should().Contain("playerId(filter:\"sharedFilter\")");
        rendered.Should().NotContain("businessObjects_1");
    }

    [Fact]
    public async Task Include_MixedSharedAndDivergentFilterFragments_MergesSharedAndSeparatesDivergent()
    {
        // Arrange
        var root = CreateDefaultBuilder("Root", MergingStrategy.MergeByFieldPath);

        // Act — five fragments share one filter (should merge into a single root), five more
        // each carry a distinct filter (should stay separate aliased roots), and a final fragment
        // repeats the shared filter to prove a late merge still lands on the original root even
        // after several divergent fragments were inserted in between.
        for (var i = 0; i < 5; i++)
        {
            root.Include(CreateDefaultBuilder($"Shared{i}", MergingStrategy.MergeByFieldPath)
                .AddField("businessObjects.entity.edges.node.playerId", new Dictionary<string, object?> { ["filter"] = "shared" }));
        }
        for (var i = 0; i < 5; i++)
        {
            root.Include(CreateDefaultBuilder($"Unique{i}", MergingStrategy.MergeByFieldPath)
                .AddField("businessObjects.entity.edges.node.playerId", new Dictionary<string, object?> { ["filter"] = $"unique{i}" }));
        }
        root.Include(CreateDefaultBuilder("SharedAgain", MergingStrategy.MergeByFieldPath)
            .AddField("businessObjects.entity.edges.node.playerId", new Dictionary<string, object?> { ["filter"] = "shared" }));

        // Assert
        await root.Verify();
    }

    [Fact]
    public async Task Include_DivergentFilterFragments_NeverMergeStrategyStaysSeparate()
    {
        // Arrange
        var root = CreateDefaultBuilder("Root", MergingStrategy.NeverMerge);

        // Act
        for (var i = 0; i < 5; i++)
        {
            root.Include(DivergentFragment(i));
        }

        // Assert
        await root.Verify();
    }

    [Fact]
    public async Task Include_IdenticalFilterFragments_NeverMergeStrategyStaysSeparate()
    {
        // Arrange
        var root = CreateDefaultBuilder("Root", MergingStrategy.NeverMerge);

        // Act
        for (var i = 0; i < 5; i++)
        {
            root.Include(IdenticalFragment(i));
        }

        // Assert
        await root.Verify();
    }

    [Fact]
    public void Include_FiltersDifferingOnlyByCase_StaySeparateRoots()
    {
        // Arrange — AreValuesEqual compares strings via value.Equals(value2), i.e. ordinal, so
        // "Segment" and "segment" must NOT be treated as the same fingerprint bucket collapsing
        // into a merge; this also proves the fingerprint hashes strings ordinally (not
        // OrdinalIgnoreCase) to match that comparison exactly.
        var root = CreateDefaultBuilder("Root", MergingStrategy.MergeByFieldPath);

        // Act
        root.Include(CreateDefaultBuilder("Upper", MergingStrategy.MergeByFieldPath)
            .AddField("businessObjects.entity.edges.node.playerId", new Dictionary<string, object?> { ["filter"] = "Segment" }));
        root.Include(CreateDefaultBuilder("Lower", MergingStrategy.MergeByFieldPath)
            .AddField("businessObjects.entity.edges.node.playerId", new Dictionary<string, object?> { ["filter"] = "segment" }));

        // Assert
        root.DefinitionsCount.Should().Be(2);
        var rendered = root.ToString();
        rendered.Should().Contain("playerId(filter:\"Segment\")");
        rendered.Should().Contain("playerId(filter:\"segment\")");
    }

    [Fact]
    public void Include_EqualNestedDictionaryFilters_MergeIntoOneRoot()
    {
        // Arrange — nested IDictionary<string,object?> argument values fall back to the
        // conservative sentinel hash (not a real structural hash), so two arguments that ARE
        // structurally equal must still land in the same bucket and merge — proves the sentinel
        // fallback never wrongly SPLITS a genuine match.
        static QueryBuilder Fragment(string name) =>
            CreateDefaultBuilder(name, MergingStrategy.MergeByFieldPath)
                .AddField("businessObjects.entity.edges.node.playerId", new Dictionary<string, object?>
                {
                    ["filter"] = new Dictionary<string, object?> { ["segment"] = "a", ["min"] = 1 },
                });

        var root = CreateDefaultBuilder("Root", MergingStrategy.MergeByFieldPath);

        // Act
        for (var i = 0; i < 5; i++)
        {
            root.Include(Fragment($"Fragment{i}"));
        }

        // Assert
        root.DefinitionsCount.Should().Be(1);
        root.ToString().Should().NotContain("businessObjects_1");
    }

    [Fact]
    public async Task Include_DivergentNestedDictionaryFilters_ProducesSeparateAliasedRoots()
    {
        // Arrange — nested dictionary values that are NOT structurally equal must still be
        // correctly rejected by the full CanMergeFields check even though the sentinel hash
        // groups them into the same bucket as any other nested-dictionary-valued field.
        var root = CreateDefaultBuilder("Root", MergingStrategy.MergeByFieldPath);

        // Act
        for (var i = 0; i < 3; i++)
        {
            root.Include(CreateDefaultBuilder($"Fragment{i}", MergingStrategy.MergeByFieldPath)
                .AddField("businessObjects.entity.edges.node.playerId", new Dictionary<string, object?>
                {
                    ["filter"] = new Dictionary<string, object?> { ["segment"] = $"segment{i}" },
                }));
        }

        // Assert
        await root.Verify();
    }

    [Fact]
    public void Include_SameArgumentsDifferentChildStructure_ProducesSeparateAliasedRoots()
    {
        // Arrange — same field name, same top-level arguments, but incompatible nested child
        // shapes: First's "alpha" child is argument-free, Second's "beta" child carries an
        // argument the deep fingerprint must see. Since "beta" is argument-carrying and absent
        // from First, the deep fingerprints diverge (First has no contribution from an
        // argument-carrying child at that name; Second does) — so this is now correctly rejected
        // by the fingerprint pre-filter itself, without even reaching CanMergeFields. Behavior
        // (the render output) must match pre-change exactly regardless of which layer rejects it.
        var root = CreateDefaultBuilder("Root", MergingStrategy.MergeByFieldPath);

        // Act
        root.Include(CreateDefaultBuilder("First", MergingStrategy.MergeByFieldPath)
            .AddField("businessObjects", new Dictionary<string, object?> { ["filter"] = "shared" })
            .AddField("businessObjects.alpha"));
        root.Include(CreateDefaultBuilder("Second", MergingStrategy.MergeByFieldPath)
            .AddField("businessObjects", new Dictionary<string, object?> { ["filter"] = "shared" })
            .AddField("businessObjects.beta", new Dictionary<string, object?> { ["extra"] = "value" }));

        // Assert
        root.DefinitionsCount.Should().Be(2);
        var rendered = root.ToString();
        rendered.Should().Contain("alpha");
        rendered.Should().Contain("beta");
    }

    [Fact]
    public void Include_ZeroArgumentFields_MergeIntoOneRoot()
    {
        // Arrange — fields with no arguments at all must still merge with each other (the
        // fingerprint for "no arguments" is a fixed, single value shared by every argument-less
        // field, so they all land in one bucket and CanMergeFields confirms the merge).
        var root = CreateDefaultBuilder("Root", MergingStrategy.MergeByFieldPath);

        // Act
        for (var i = 0; i < 5; i++)
        {
            root.Include(CreateDefaultBuilder($"Fragment{i}", MergingStrategy.MergeByFieldPath)
                .AddField("businessObjects.entity.edges.node.playerId"));
        }

        // Assert
        root.DefinitionsCount.Should().Be(1);
        root.ToString().Should().NotContain("businessObjects_1");
    }

    // ── Deep (subtree) fingerprint regression tests ─────────────────────────
    // The production shape's discriminating filter sits several levels below the root field
    // that FindMergeTarget compares, so the merge index buckets on a fingerprint that covers the
    // whole merge-relevant subtree, not just the root field's own (always-empty) arguments.

    [Fact]
    public async Task Include_DeepDivergentFilters_ProducesSeparateAliasedRoots()
    {
        // Arrange — the exact production shape: root field "businessObjects" carries zero
        // arguments; the discriminating filter lives four levels down on the "playerId" leaf.
        var root = CreateDefaultBuilder("Root", MergingStrategy.MergeByFieldPath);

        // Act
        for (var i = 0; i < 5; i++)
        {
            root.Include(DivergentFragment(i));
        }

        // Assert
        root.DefinitionsCount.Should().Be(5);
        await root.Verify();
    }

    [Fact]
    public void Include_DeepIdenticalFilters_MergeIntoOneRoot()
    {
        // Arrange — proves the deep fingerprint does not cause a false split at depth: fragments
        // whose only leaf argument is identical must still land in the same bucket and merge.
        var root = CreateDefaultBuilder("Root", MergingStrategy.MergeByFieldPath);

        // Act
        for (var i = 0; i < 25; i++)
        {
            root.Include(IdenticalFragment(i));
        }

        // Assert
        root.DefinitionsCount.Should().Be(1);
        root.ToString().Should().NotContain("businessObjects_1");
    }

    [Fact]
    public void Include_ExtraArgumentFreeDeepChild_StillMerges()
    {
        // Arrange — incoming carries an extra child subtree ("other.deep.leaf") that has NO
        // arguments anywhere in it. An argument-free subtree must contribute nothing to the deep
        // fingerprint, or two otherwise-identical fields would be falsely split just because one
        // has extra argument-free structure the other lacks. This is the case most likely to
        // catch a wrongly-shallow fingerprint derivation (one that skips based on a child's own
        // arguments instead of its whole subtree, or one that fails to ignore the child at all).
        var root = CreateDefaultBuilder("Root", MergingStrategy.MergeByFieldPath)
            .AddField("bo", new Dictionary<string, object?> { ["r"] = "R" }, b => b
                .AddField("entity", new Dictionary<string, object?> { ["e"] = "E" }, e => e
                    .AddField("node", n => n.AddField("id"))));

        var incoming = CreateDefaultBuilder("Extra", MergingStrategy.MergeByFieldPath)
            .AddField("bo", new Dictionary<string, object?> { ["r"] = "R" }, b => b
                .AddField("entity", new Dictionary<string, object?> { ["e"] = "E" }, e => e
                    .AddField("other.deep.leaf")));

        // Act
        root.Include(incoming);

        // Assert
        root.DefinitionsCount.Should().Be(1);
        var rendered = root.ToString();
        rendered.Should().NotContain("bo_1");
        rendered.Should().Contain("id");
        rendered.Should().Contain("leaf");
    }

    [Fact]
    public void Include_ExtraChildWithDeepNestedArgument_ProducesSeparateAliasedRoots()
    {
        // Arrange — mirror of the previous test, except the extra child's leaf now carries an
        // argument three levels down ("other.deep.leaf(g:\"y\")"). A single argument anywhere in
        // a subtree makes that whole ancestor chain significant: the deep fingerprint must fold
        // in the full name-path down to the argument-bearing node, not just the leaf's own
        // arguments in isolation, or two structurally different fields would falsely collide and
        // merge. This is the case most likely to catch a wrongly-truncated path derivation.
        var root = CreateDefaultBuilder("Root", MergingStrategy.MergeByFieldPath)
            .AddField("bo", new Dictionary<string, object?> { ["r"] = "R" }, b => b
                .AddField("entity", new Dictionary<string, object?> { ["e"] = "E" }, e => e
                    .AddField("node", n => n.AddField("id"))));

        var incoming = CreateDefaultBuilder("Extra", MergingStrategy.MergeByFieldPath)
            .AddField("bo", new Dictionary<string, object?> { ["r"] = "R" }, b => b
                .AddField("entity", new Dictionary<string, object?> { ["e"] = "E" }, e => e
                    .AddField("other.deep.leaf", new Dictionary<string, object?> { ["g"] = "y" })));

        // Act
        root.Include(incoming);

        // Assert
        root.DefinitionsCount.Should().Be(2);
        var rendered = root.ToString();
        rendered.Should().Contain("bo_1");
    }

    [Fact]
    public void Include_IdenticalArgumentsAtMultipleLevels_DifferentLeaves_MergeIntoOneRoot()
    {
        // Arrange — arguments at SEVERAL depths do not, by themselves, prevent merging: as long
        // as every argument-bearing node along the way matches exactly, divergent argument-free
        // leaves still combine under the shared path. Guards against a fingerprint that treats
        // "has arguments somewhere" as automatically disqualifying rather than comparing the
        // actual argument-bearing structure.
        var root = CreateDefaultBuilder("Root", MergingStrategy.MergeByFieldPath)
            .AddField("bo", new Dictionary<string, object?> { ["r"] = "R" }, b => b
                .AddField("entity", new Dictionary<string, object?> { ["e"] = "E" }, e => e
                    .AddField("node.id")));

        var incoming = CreateDefaultBuilder("Second", MergingStrategy.MergeByFieldPath)
            .AddField("bo", new Dictionary<string, object?> { ["r"] = "R" }, b => b
                .AddField("entity", new Dictionary<string, object?> { ["e"] = "E" }, e => e
                    .AddField("node.name")));

        // Act
        root.Include(incoming);

        // Assert
        root.DefinitionsCount.Should().Be(1);
        var rendered = root.ToString();
        rendered.Should().NotContain("bo_1");
        rendered.Should().Contain("id");
        rendered.Should().Contain("name");
    }

    [Fact]
    public void Include_ExistingArgBearingChild_IncomingDifferentChild_ProducesSeparateAliasedRoots()
    {
        // Arrange — existing has an argument-bearing child ("posts(limit:10)") the incoming side
        // lacks entirely; incoming instead has an unrelated argument-free child ("tags"). The
        // deep fingerprint must see "posts" as significant (it carries arguments) and "tags" as
        // insignificant (it does not), so the two fingerprints diverge and the fields correctly
        // stay separate — matching CanMergeFields's own (untouched) verdict, now reached via the
        // fingerprint layer instead of a full scan. Mirrors
        // Include_ExistingHasArgsNotInIncoming_FailsCanMergeAndAliasesChild in
        // QueryBuilderMergingStrategyTests.
        var root = CreateDefaultBuilder("Root", MergingStrategy.MergeByFieldPath)
            .AddField("users.posts", new Dictionary<string, object?> { ["limit"] = 10 }, new[] { "title" });

        var incoming = CreateDefaultBuilder("Child", MergingStrategy.MergeByFieldPath)
            .AddField("users.tags", new[] { "name" });

        // Act
        root.Include(incoming);

        // Assert
        root.DefinitionsCount.Should().Be(2);
        var rendered = root.ToString();
        rendered.Should().Contain("users_1");
    }

    [Fact]
    public async Task Include_MixedBatchWithVaryingDepthDivergence_MergesSharedAndSeparatesDivergent()
    {
        // Arrange — a single batch interleaving: identical fragments (should all merge), a
        // fragment diverging at the ROOT-adjacent level, one diverging MID-tree, and one
        // diverging only at the LEAF — every divergence depth must still correctly separate.
        var root = CreateDefaultBuilder("Root", MergingStrategy.MergeByFieldPath);

        QueryBuilder Shared(string name) =>
            CreateDefaultBuilder(name, MergingStrategy.MergeByFieldPath)
                .AddField("bo", new Dictionary<string, object?> { ["r"] = "R" }, b => b
                    .AddField("entity", new Dictionary<string, object?> { ["e"] = "E" }, e => e
                        .AddField("node.playerId")));

        // Act
        root.Include(Shared("Shared1"));
        root.Include(Shared("Shared2"));

        root.Include(CreateDefaultBuilder("RootDivergent", MergingStrategy.MergeByFieldPath)
            .AddField("bo", new Dictionary<string, object?> { ["r"] = "DIFFERENT" }, b => b
                .AddField("entity", new Dictionary<string, object?> { ["e"] = "E" }, e => e
                    .AddField("node.playerId"))));

        root.Include(CreateDefaultBuilder("MidDivergent", MergingStrategy.MergeByFieldPath)
            .AddField("bo", new Dictionary<string, object?> { ["r"] = "R" }, b => b
                .AddField("entity", new Dictionary<string, object?> { ["e"] = "DIFFERENT" }, e => e
                    .AddField("node.playerId"))));

        root.Include(CreateDefaultBuilder("LeafDivergent", MergingStrategy.MergeByFieldPath)
            .AddField("bo", new Dictionary<string, object?> { ["r"] = "R" }, b => b
                .AddField("entity", new Dictionary<string, object?> { ["e"] = "E" }, e => e
                    .AddField("node.playerId", new Dictionary<string, object?> { ["filter"] = "x" }))));

        root.Include(Shared("Shared3"));

        // Assert — 2 shared fragments + late-arriving Shared3 merge into 1 root; root/mid/leaf
        // divergent fragments each stay separate: 4 total root-level definitions.
        root.DefinitionsCount.Should().Be(4);
        await root.Verify();
    }

    [Fact]
    public void Include_OutOfBandAddFieldBetweenIncludes_KeepsWorking()
    {
        // Arrange — interleaving plain AddField/Preserve calls between Include() calls must not
        // desync the deep fingerprint cache: a shared-filter fragment still merges, and a
        // differing-filter fragment still stays separate, even with an out-of-band AddField on
        // the root builder in between.
        var root = CreateDefaultBuilder("Root", MergingStrategy.MergeByFieldPath);

        // Act
        root.Include(CreateDefaultBuilder("First", MergingStrategy.MergeByFieldPath)
            .AddField("shared", new Dictionary<string, object?> { ["f"] = "x" }));

        // Out-of-band mutation: a plain AddField call on the same root builder, not routed
        // through QueryMerger's in-place-merge invalidation path.
        root.AddField("unrelated");

        root.Include(CreateDefaultBuilder("Second", MergingStrategy.MergeByFieldPath)
            .AddField("shared", new Dictionary<string, object?> { ["f"] = "x" })); // same filter -> merge

        root.Include(CreateDefaultBuilder("Third", MergingStrategy.MergeByFieldPath)
            .AddField("shared", new Dictionary<string, object?> { ["f"] = "DIFFERENT" })); // different -> separate

        // Assert
        var rendered = root.ToString();
        rendered.Should().Contain("shared(f:\"x\")");
        rendered.Should().Contain("shared_1:shared(f:\"DIFFERENT\")");
        rendered.Should().Contain("unrelated");
    }
}
