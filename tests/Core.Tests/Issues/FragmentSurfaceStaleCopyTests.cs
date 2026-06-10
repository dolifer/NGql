using FluentAssertions;
using NGql.Core.Builders;
using Xunit;

namespace NGql.Core.Tests.Issues;

/// <summary>
/// Regression: <c>FieldBuilder.Where/WithAlias/WithType</c> replace the builder's field with a
/// <c>with</c> record copy. The fragment-surface reflect-back in <c>QueryBuilder.AddFragment</c>
/// and <c>FieldBuilder.OnType</c> originally read the pre-copy surface, so spread lists or
/// nested fragment maps first created after such a call were written to the copy and discarded.
/// </summary>
public class FragmentSurfaceStaleCopyTests
{
    [Fact]
    public void AddFragment_SpreadAfterWithAlias_KeepsSpread()
    {
        // Arrange & Act — WithAlias forces a `with` copy before SpreadFragment runs
        var query = QueryBuilder.CreateDefaultBuilder("Q")
            .AddFragment("Audit", "User", f => f.AddField("updatedAt"))
            .AddFragment("Card", "User", f => f
                .AddField("id")
                .WithAlias("x")
                .SpreadFragment("Audit"))
            .AddField("users", u => u.SpreadFragment("Card"));

        // Assert
        query.ToString().Should().Contain("...Audit",
            "spreads recorded after a with-copying builder call must reach the fragment");
    }

    [Fact]
    public void OnType_SpreadAfterWhere_KeepsSpread()
    {
        // Arrange & Act — Where forces a `with` copy on the inline-fragment surface
        var query = QueryBuilder.CreateDefaultBuilder("Q")
            .AddFragment("Card", "User", f => f.AddField("id"))
            .AddField("nodes", n => n.OnType("User", r => r
                .Where("first", 10)
                .SpreadFragment("Card")));

        // Assert
        query.ToString().Should().Contain("...Card",
            "spreads recorded after a with-copying builder call must reach the inline fragment");
    }

    [Fact]
    public void AddFragment_NestedOnTypeAfterWithType_KeepsNestedFragment()
    {
        // Arrange & Act — nested inline-fragment map created after a with-copy
        var query = QueryBuilder.CreateDefaultBuilder("Q")
            .AddFragment("Card", "Node", f => f
                .AddField("id")
                .WithType("Node")
                .OnType("User", u => u.AddField("email")))
            .AddField("nodes", n => n.SpreadFragment("Card"));

        // Assert
        query.ToString().Should().Contain("... on User",
            "nested fragments recorded after a with-copying builder call must reach the fragment");
    }
}
