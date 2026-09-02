using System.Collections.Generic;
using System.Threading.Tasks;
using NGql.Core.Tests.Extensions;
using Xunit;
using static NGql.Core.Builders.QueryBuilder;

namespace NGql.Core.Tests.Issues;

public class NestedAliasConflictTest
{
    [Fact]
    public async Task NestedSegmentAlias_SameAlias_DifferentTargets_ShouldCreateSeparateFields()
    {
        // Arrange - Same alias "Metrics" at nested segment targeting different fields
        var queryA = CreateDefaultBuilder("QueryA", MergingStrategy.MergeByFieldPath)
            .AddField("user.profile.Metrics:realtime_metrics.count")
            .AddField("user.profile.Metrics:realtime_metrics.timestamp");

        var queryB = CreateDefaultBuilder("QueryB", MergingStrategy.MergeByFieldPath)
            .AddField("user.profile.Metrics:once_a_day_metrics.total")
            .AddField("user.profile.Metrics:once_a_day_metrics.date");

        var rootQuery = CreateDefaultBuilder("TestQuery", MergingStrategy.MergeByFieldPath);

        // Act
        rootQuery.Include(queryA).Include(queryB);

        // Assert
        await rootQuery.Verify("NestedSegmentAlias_Conflict");
    }

    [Fact]
    public async Task MergeByDefault_SameNameDifferentAliasSiblings_NewAliasAppendsSeparateField()
    {
        // Arrange — two Includes establish two siblings that share a Name ("id") but differ by
        // alias ("aliasA"/"aliasB") under the same parent. FieldChildren's index remembers only
        // the LAST slot inserted for a given Name, so a subsequent lookup for a THIRD, distinct
        // alias must fall through Helpers.FindExistingField's exhaustive-scan fallback (an index
        // hit on Name alone would otherwise wrongly report "not found" via the wrong candidate, or
        // worse, silently reuse the wrong slot) rather than incorrectly merging into either
        // existing sibling.
        var root = CreateDefaultBuilder("Root", MergingStrategy.MergeByDefault)
            .AddField("user.aliasA:id");

        root.Include(CreateDefaultBuilder("B", MergingStrategy.MergeByDefault)
            .AddField("user.aliasB:id"));

        // Act — re-Include with a THIRD, never-before-seen alias for the same Name.
        root.Include(CreateDefaultBuilder("C", MergingStrategy.MergeByDefault)
            .AddField("user.aliasC:id"));

        // Assert — three distinct sibling fields, none dropped or accidentally merged together.
        await root.Verify();
    }

    [Fact]
    public async Task MergeByDefault_ReIncludeFirstOfThreeSameNameSiblings_MergesIntoExistingViaFallbackScan()
    {
        // Arrange — three siblings share a Name ("id") with three distinct aliases. FieldChildren's
        // index remembers only the LAST-appended slot for "id" (aliasC), so re-Including the FIRST
        // alias (aliasA) is guaranteed to miss the index fast path in Helpers.FindExistingField and
        // require the exhaustive linear-scan fallback to locate it — proving that fallback doesn't
        // just run, but genuinely FINDS an earlier same-named sibling instead of always missing
        // through to the Path-based fallback.
        var root = CreateDefaultBuilder("Root", MergingStrategy.MergeByDefault)
            .AddField("user.aliasA:id");
        root.Include(CreateDefaultBuilder("B", MergingStrategy.MergeByDefault).AddField("user.aliasB:id"));
        root.Include(CreateDefaultBuilder("C", MergingStrategy.MergeByDefault).AddField("user.aliasC:id"));

        // Act — re-Include the FIRST-established alias with a new argument; it must merge into the
        // existing "aliasA:id" sibling (found via the linear scan), not append a fourth field.
        root.Include(CreateDefaultBuilder("D", MergingStrategy.MergeByDefault)
            .AddField("user.aliasA:id", new Dictionary<string, object?> { ["extra"] = "value" }));

        // Assert — still exactly three siblings; aliasA now carries the merged argument.
        await root.Verify();
    }
}
