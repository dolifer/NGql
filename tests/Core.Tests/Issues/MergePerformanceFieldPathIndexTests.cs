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
        // Arrange — same field name, same top-level arguments (so the fingerprint bucket the
        // arguments alone produce would allow a merge), but incompatible nested child shapes.
        // The fingerprint deliberately only covers a field's own _arguments, never its subtree,
        // so this must still be rejected — by the untouched, subtree-aware half of
        // CanMergeFields, not by the fingerprint.
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
}
