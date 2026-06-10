using FluentAssertions;
using NGql.Core.Builders;
using Xunit;

namespace NGql.Core.Tests.Issues;

/// <summary>
/// Regression: a query produced by <c>PreservationBuilder.Build()</c> must be fully isolated
/// from its source builder.
///
/// Internally, preservation stored the source <see cref="NGql.Core.Abstractions.FieldDefinition"/>
/// reference at the end of every preserved path, so the preserved query and the source query
/// shared the leaf's whole subtree. Mutating either builder afterwards leaked into the other —
/// breaking the documented role-based filtering scenario, where a "safe" projection is handed
/// out and the full query keeps evolving.
/// </summary>
public class PreserveSharesSourceSubtreeTests
{
    [Fact]
    public void Preserve_MutatePreservedAfterBuild_SourceUnchanged()
    {
        // Arrange
        var source = QueryBuilder.CreateDefaultBuilder("Full")
            .AddField("user.name")
            .AddField("user.email");
        var sourceBefore = source.ToString();

        var preserved = PreservationBuilder.Create(source)
            .Preserve("user")
            .Build();

        // Act
        preserved.AddField("user.ssn");

        // Assert
        source.ToString().Should().Be(sourceBefore,
            "mutating the preserved query must not leak into the source builder");
    }

    [Fact]
    public void Preserve_MutateSourceAfterBuild_PreservedUnchanged()
    {
        // Arrange
        var source = QueryBuilder.CreateDefaultBuilder("Full")
            .AddField("user.profile.name")
            .AddField("user.profile.bio");

        var preserved = PreservationBuilder.Create(source)
            .Preserve("user.profile")
            .Build();
        var preservedBefore = preserved.ToString();

        // Act
        source.AddField("user.profile.secret");

        // Assert
        preserved.ToString().Should().Be(preservedBefore,
            "mutating the source after Build() must not leak into the preserved projection");
    }

    [Fact]
    public void Preserve_DeepLeafPath_MutateSourceSiblingSubtree_PreservedUnchanged()
    {
        // Arrange — preserved path ends at a nested leaf; the leaf node itself is the share point
        var source = QueryBuilder.CreateDefaultBuilder("Full")
            .AddField("account.owner.name")
            .AddField("account.balance");

        var preserved = PreservationBuilder.Create(source)
            .Preserve("account.owner")
            .Build();
        var preservedBefore = preserved.ToString();

        // Act — grow the subtree under the preserved leaf in the source
        source.AddField("account.owner.taxId");

        // Assert
        preserved.ToString().Should().Be(preservedBefore);
    }

    [Fact]
    public void Preserve_FieldArguments_NotSharedWithSource()
    {
        // Arrange — argument dictionaries are mutable; the preserved copy must own its own
        var source = QueryBuilder.CreateDefaultBuilder("Full");
        source.AddField("users", new System.Collections.Generic.Dictionary<string, object?> { ["first"] = 10 }, new[] { "id", "name" });

        var preserved = PreservationBuilder.Create(source)
            .Preserve("users")
            .Build();
        var preservedBefore = preserved.ToString();

        // Act — change the argument on the source field via the field-builder surface
        source.AddField("users", new System.Collections.Generic.Dictionary<string, object?> { ["first"] = 99 }, new[] { "id" });

        // Assert
        preserved.ToString().Should().Be(preservedBefore,
            "argument changes on the source must not show up in the preserved projection");
    }
}
