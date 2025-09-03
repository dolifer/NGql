using System.Threading.Tasks;
using FluentAssertions;
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
}
