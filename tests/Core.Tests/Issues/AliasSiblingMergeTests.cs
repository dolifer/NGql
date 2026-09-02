using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using NGql.Core.Builders;
using NGql.Core.Tests.Extensions;
using Xunit;
using static NGql.Core.Builders.QueryBuilder;

namespace NGql.Core.Tests.Issues;

public class AliasSiblingMergeTests
{
    private static QueryBuilder Fragment(string name, string fieldPath, MergingStrategy strategy) =>
        CreateDefaultBuilder(name, strategy).AddField(fieldPath);

    [Theory]
    [InlineData(MergingStrategy.MergeByDefault)]
    [InlineData(MergingStrategy.MergeByFieldPath)]
    public void ReIncludeSameNameDifferentAlias_IsNoOpMerge(MergingStrategy strategy)
    {
        // Arrange — two prior Includes establish "aliasA:id" and "aliasB:id" as siblings sharing
        // a Name ("id") but differing by alias. Re-Including "aliasB:id" a THIRD time must be a
        // pure no-op: neither sibling is dropped, duplicated, or overwritten.
        var root = CreateDefaultBuilder("R", strategy);
        root.Include(Fragment("A", "user.aliasA:id", strategy));
        root.Include(Fragment("B", "user.aliasB:id", strategy));

        // Act
        root.Include(Fragment("C", "user.aliasB:id", strategy));

        // Assert
        root.ToString().Should().Be(
            "query R{\n    user{\n        aliasA:id\n        aliasB:id\n    }\n}");
    }

    [Theory]
    [InlineData(MergingStrategy.MergeByDefault)]
    [InlineData(MergingStrategy.MergeByFieldPath)]
    public void ReIncludeEachSameNameDifferentAlias_IsNoOpMergeForBoth(MergingStrategy strategy)
    {
        // Arrange/Act — re-Including EITHER previously-seen alias (not just the last one) must
        // land on its own sibling, never the other one.
        var root = CreateDefaultBuilder("R", strategy);
        root.Include(Fragment("A", "user.aliasA:id", strategy));
        root.Include(Fragment("B", "user.aliasB:id", strategy));
        root.Include(Fragment("C", "user.aliasB:id", strategy));
        root.Include(Fragment("D", "user.aliasA:id", strategy));

        // Assert
        root.ToString().Should().Be(
            "query R{\n    user{\n        aliasA:id\n        aliasB:id\n    }\n}");
    }

    [Theory]
    [InlineData(MergingStrategy.MergeByDefault)]
    [InlineData(MergingStrategy.MergeByFieldPath)]
    public void ThreePlusSameNameSiblings_ReIncludeEach_StayDistinct(MergingStrategy strategy)
    {
        // Arrange — three siblings share Name "id" with three distinct aliases.
        var root = CreateDefaultBuilder("R", strategy);
        root.Include(Fragment("A", "user.a1:id", strategy));
        root.Include(Fragment("B", "user.a2:id", strategy));
        root.Include(Fragment("C", "user.a3:id", strategy));

        // Act — re-Include each of the three, in a different order than they were first added.
        root.Include(Fragment("D", "user.a2:id", strategy));
        root.Include(Fragment("E", "user.a1:id", strategy));
        root.Include(Fragment("F", "user.a3:id", strategy));

        // Assert — still exactly three siblings, none merged into another or lost.
        root.ToString().Should().Be(
            "query R{\n    user{\n        a1:id\n        a2:id\n        a3:id\n    }\n}");
    }

    [Theory]
    [InlineData(MergingStrategy.MergeByDefault)]
    [InlineData(MergingStrategy.MergeByFieldPath)]
    public void ReIncludeWithNewChildFields_MergesIntoCorrectSibling(MergingStrategy strategy)
    {
        // Arrange — two same-name/different-alias siblings under "user", each with one child.
        var root = CreateDefaultBuilder("R", strategy);
        root.Include(CreateDefaultBuilder("A", strategy).AddField("user", a => a.AddField("aliasA:id")));
        root.Include(CreateDefaultBuilder("B", strategy).AddField("user", b => b.AddField("aliasB:id")));

        // Act — re-include "aliasB" but with an ADDITIONAL new child field alongside it. The new
        // field must be attached as a sibling of "aliasB:id", never as a sibling of "aliasA:id"
        // (some strategies auto-uniquify the new field's alias to avoid a response-key collision
        // with "aliasB", so the exact rendered alias is not asserted here — only correct grouping
        // and that nothing was lost).
        root.Include(CreateDefaultBuilder("C", strategy).AddField("user", c =>
        {
            c.AddField("aliasB:id");
            c.AddField("aliasB:name");
        }));

        // Assert — "aliasA:id" untouched, "aliasB:id" still present, and the new "name" field
        // exists exactly once (attached under the "aliasB" branch, wherever it renders).
        var rendered = root.ToString();
        rendered.Should().Contain("aliasA:id");
        rendered.Should().Contain("aliasB:id");
        CountOccurrences(rendered, "name").Should().Be(1);
    }

    [Theory]
    [InlineData(MergingStrategy.MergeByDefault)]
    [InlineData(MergingStrategy.MergeByFieldPath)]
    [InlineData(MergingStrategy.NeverMerge)]
    public void UnaliasedFieldCoexistsWithAliasedSameNameSibling(MergingStrategy strategy)
    {
        // Arrange/Act — "id" (no alias) and "aliasA:id" (aliased) share a Name but are distinct
        // GraphQL response keys ("id" vs "aliasA") and must both survive an Include-based merge.
        var root = CreateDefaultBuilder("R", strategy);
        root.Include(Fragment("A", "user.id", strategy));
        root.Include(Fragment("B", "user.aliasA:id", strategy));

        // Assert
        var rendered = root.ToString();
        rendered.Should().Contain("aliasA:id");
        // The unaliased "id" must render as bare "id", not be dropped or collapsed into "aliasA:id".
        rendered.Should().MatchRegex(@"(?<!alias\w:)\bid\b");
    }

    [Theory]
    [InlineData(MergingStrategy.MergeByDefault)]
    [InlineData(MergingStrategy.MergeByFieldPath)]
    public void CrossingIndexThreshold_SameNameDifferentAliasSiblings_StayDistinct(MergingStrategy strategy)
    {
        // Arrange — push the shared parent's FieldChildren well past its lazy-index threshold
        // (16 children) with distinct-name filler siblings, interleaved with the same-name/
        // different-alias pair under test, so the fix is exercised on the INDEXED lookup/replace
        // path, not just the small-collection linear-scan path.
        var root = CreateDefaultBuilder("R", strategy);
        for (var i = 0; i < 20; i++)
        {
            root.Include(Fragment($"F{i}", $"user.field{i}", strategy));
        }
        root.Include(Fragment("A", "user.aliasA:id", strategy));
        root.Include(Fragment("B", "user.aliasB:id", strategy));

        // Act — re-Include "aliasB" now that the collection is indexed.
        root.Include(Fragment("C", "user.aliasB:id", strategy));

        // Assert — exactly one "aliasA:id" and one "aliasB:id", none dropped or duplicated.
        var rendered = root.ToString();
        CountOccurrences(rendered, "aliasA:id").Should().Be(1);
        CountOccurrences(rendered, "aliasB:id").Should().Be(1);
    }

    private static int CountOccurrences(string haystack, string needle)
    {
        var count = 0;
        var index = 0;
        while ((index = haystack.IndexOf(needle, index, System.StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += needle.Length;
        }
        return count;
    }

    [Fact]
    public async Task ReIncludeSameNameDifferentAlias_MergeByDefault_Snapshot()
    {
        // Arrange — the exact reported repro, captured as a Verify() snapshot.
        var root = CreateDefaultBuilder("R", MergingStrategy.MergeByDefault);
        root.Include(Fragment("A", "user.aliasA:id", MergingStrategy.MergeByDefault));
        root.Include(Fragment("B", "user.aliasB:id", MergingStrategy.MergeByDefault));

        // Act
        root.Include(Fragment("C", "user.aliasB:id", MergingStrategy.MergeByDefault));
        root.Include(Fragment("D", "user.aliasA:id", MergingStrategy.MergeByDefault));

        // Assert
        await root.Verify();
    }
}
