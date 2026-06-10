using System.Collections.Generic;
using FluentAssertions;
using NGql.Core.Builders;
using Xunit;

namespace NGql.Core.Tests.Issues;

/// <summary>
/// Regression: the preserve-isolation fix (deep-cloning preserved subtrees) must carry the
/// field's complete state. The first cut of the fix cloned children/arguments/metadata but
/// dropped inline fragments, fragment spreads, and the operation's named-fragment definitions,
/// silently emitting incomplete GraphQL. Intermediate path nodes also kept sharing their
/// argument dictionary with the source.
/// </summary>
public class PreserveFragmentStateTests
{
    [Fact]
    public void Preserve_LeafWithInlineFragment_KeepsFragment()
    {
        // Arrange
        var source = QueryBuilder.CreateDefaultBuilder("S")
            .AddField("nodes", n =>
            {
                n.AddField("id");
                n.OnType("Repository", r => r.AddField("name"));
            });

        // Act
        var preserved = PreservationBuilder.Create(source).Preserve("nodes").Build();

        // Assert
        preserved.ToString().Should().Contain("... on Repository",
            "the preserved subtree must keep the source's inline fragments");
    }

    [Fact]
    public void Preserve_LeafWithSpread_KeepsSpreadAndFragmentDefinition()
    {
        // Arrange
        var source = QueryBuilder.CreateDefaultBuilder("S")
            .AddFragment("Card", "User", f => f.AddField("id"))
            .AddField("users", u =>
            {
                u.AddField("id");
                u.SpreadFragment("Card");
            });

        // Act
        var text = PreservationBuilder.Create(source).Preserve("users").Build().ToString();

        // Assert
        text.Should().Contain("...Card", "the spread must survive preservation");
        text.Should().Contain("fragment Card on User", "the referenced definition must be copied");
    }

    [Fact]
    public void Preserve_TransitiveSpreads_CopiesAllReferencedDefinitions()
    {
        // Arrange — Card spreads Audit; preserving a Card user must carry both definitions
        var source = QueryBuilder.CreateDefaultBuilder("S")
            .AddFragment("Audit", "User", f => f.AddField("updatedAt"))
            .AddFragment("Card", "User", f =>
            {
                f.AddField("id");
                f.SpreadFragment("Audit");
            })
            .AddField("users", u => u.SpreadFragment("Card"));

        // Act
        var text = PreservationBuilder.Create(source).Preserve("users").Build().ToString();

        // Assert
        text.Should().Contain("fragment Card on User");
        text.Should().Contain("fragment Audit on User");
    }

    [Fact]
    public void Preserve_SpreadInsideInlineFragment_CopiesReferencedDefinition()
    {
        // Arrange — the spread sits inside an inline fragment, not directly on the field
        var source = QueryBuilder.CreateDefaultBuilder("S")
            .AddFragment("Card", "User", f => f.AddField("id"))
            .AddField("nodes", n => n.OnType("User", u => u.SpreadFragment("Card")));

        // Act
        var text = PreservationBuilder.Create(source).Preserve("nodes").Build().ToString();

        // Assert
        text.Should().Contain("... on User");
        text.Should().Contain("...Card");
        text.Should().Contain("fragment Card on User",
            "spreads nested in inline fragments must pull their definitions along");
    }

    [Fact]
    public void Preserve_UnreferencedNamedFragment_NotCopied()
    {
        // Arrange — GraphQL rejects operations with unused fragment definitions
        var source = QueryBuilder.CreateDefaultBuilder("S")
            .AddFragment("Card", "User", f => f.AddField("id"))
            .AddFragment("Unused", "User", f => f.AddField("name"))
            .AddField("users", u => u.SpreadFragment("Card"));

        // Act
        var text = PreservationBuilder.Create(source).Preserve("users").Build().ToString();

        // Assert
        text.Should().Contain("fragment Card on User");
        text.Should().NotContain("fragment Unused", "unreferenced definitions must not be emitted");
    }

    [Fact]
    public void Preserve_IntermediateNodeArguments_NotSharedWithSource()
    {
        // Arrange — "user" becomes an intermediate node of the preserved path
        var source = QueryBuilder.CreateDefaultBuilder("Full");
        source.AddField("user", new Dictionary<string, object?> { ["id"] = 1 }, new[] { "name", "email" });

        var preserved = PreservationBuilder.Create(source).Preserve("user.name").Build();
        var sourceBefore = source.ToString();

        // Act — mutate the intermediate node's arguments through the preserved builder
        preserved.AddField("user", b => b.Where("id", 999));

        // Assert
        source.ToString().Should().Be(sourceBefore,
            "argument mutation on the preserved query must not leak into the source");
    }
}
