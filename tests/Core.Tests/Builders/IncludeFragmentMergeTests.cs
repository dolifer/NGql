using System;
using System.Collections.Generic;
using FluentAssertions;
using NGql.Core.Builders;
using Xunit;

namespace NGql.Core.Tests.Builders;

/// <summary>
/// Covers <see cref="QueryBuilder.Include"/> merging fragment-bearing queries: named fragments
/// (with collision handling), inline fragments, spreads, and directives. Every merged construct is
/// deep-cloned, so mutating the source after the merge must never change the merged target.
/// </summary>
public class IncludeFragmentMergeTests
{
    [Fact]
    public void Include_NamedFragmentWithSpread_MergesSpreadAndDefinition()
    {
        // Arrange
        var source = QueryBuilder.CreateDefaultBuilder("Source")
            .AddFragment("UserSummary", "User", f => f.AddField("id").AddField("name"))
            .AddField("user", u => u.SpreadFragment("UserSummary"));

        var target = QueryBuilder.CreateDefaultBuilder("Target")
            .AddField("admins", a => a.AddField("id"));

        // Act
        target.Include(source);
        var rendered = target.ToString();

        // Assert — spread at the use site AND the definition after the operation block.
        rendered.Should().Contain("...UserSummary");
        rendered.Should().Contain("fragment UserSummary on User");
        rendered.IndexOf("}", StringComparison.Ordinal)
            .Should().BeLessThan(rendered.IndexOf("fragment UserSummary", StringComparison.Ordinal),
                "the definition renders after the operation's closing brace");
    }

    [Fact]
    public void Include_InlineFragmentOnField_KeepsInlineFragment()
    {
        // Arrange
        var source = QueryBuilder.CreateDefaultBuilder("Source")
            .AddField("nodes", n => n
                .AddField("id")
                .OnType("Repository", r => r.AddField("name")));

        var target = QueryBuilder.CreateDefaultBuilder("Target", MergingStrategy.MergeByFieldPath)
            .AddField("admins", a => a.AddField("id"));

        // Act
        target.Include(source);

        // Assert
        target.ToString().Should().Contain("... on Repository");
    }

    [Fact]
    public void Include_InlineFragmentOnMergedField_MergesBothSelectionSets()
    {
        // Arrange — both queries select the same field; the incoming one adds an inline fragment.
        var source = QueryBuilder.CreateDefaultBuilder("Source", MergingStrategy.MergeByFieldPath)
            .AddField("nodes", n => n.OnType("Repository", r => r.AddField("name")));

        var target = QueryBuilder.CreateDefaultBuilder("Target", MergingStrategy.MergeByFieldPath)
            .AddField("nodes", n => n.AddField("id"));

        // Act
        target.Include(source);
        var rendered = target.ToString();

        // Assert — the existing plain child and the incoming inline fragment coexist.
        rendered.Should().Contain("id");
        rendered.Should().Contain("... on Repository");
        rendered.Should().Contain("name");
    }

    [Fact]
    public void Include_SameNamedFragmentSameType_DedupesToSingleDefinition()
    {
        // Arrange — both sides declare UserSummary on User.
        var source = QueryBuilder.CreateDefaultBuilder("Source")
            .AddFragment("UserSummary", "User", f => f.AddField("email"))
            .AddField("users", u => u.SpreadFragment("UserSummary"));

        var target = QueryBuilder.CreateDefaultBuilder("Target")
            .AddFragment("UserSummary", "User", f => f.AddField("id"))
            .AddField("admins", a => a.SpreadFragment("UserSummary"));

        // Act
        target.Include(source);
        var rendered = target.ToString();

        // Assert — one definition emitted, carrying merged fields from both sides.
        CountOccurrences(rendered, "fragment UserSummary on User").Should().Be(1);
        target.Definition.NamedFragments["UserSummary"].Fields.Keys
            .Should().BeEquivalentTo("id", "email");
    }

    [Fact]
    public void Include_SameNamedFragmentDifferentType_Throws()
    {
        // Arrange — same name, conflicting onType.
        var source = QueryBuilder.CreateDefaultBuilder("Source")
            .AddFragment("Summary", "Repository", f => f.AddField("name"))
            .AddField("repos", r => r.SpreadFragment("Summary"));

        var target = QueryBuilder.CreateDefaultBuilder("Target")
            .AddFragment("Summary", "User", f => f.AddField("id"))
            .AddField("users", u => u.SpreadFragment("Summary"));

        // Act
        var act = () => target.Include(source);

        // Assert — reuses GetOrAddNamedFragment's collision semantics.
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Summary*User*Repository*");
    }

    [Fact]
    public void Include_TwoDifferentNamedFragments_MergesBothDefinitions()
    {
        // Arrange
        var source = QueryBuilder.CreateDefaultBuilder("Source")
            .AddFragment("RepoCard", "Repository", f => f.AddField("name"))
            .AddField("repos", r => r.SpreadFragment("RepoCard"));

        var target = QueryBuilder.CreateDefaultBuilder("Target")
            .AddFragment("UserCard", "User", f => f.AddField("id"))
            .AddField("users", u => u.SpreadFragment("UserCard"));

        // Act
        target.Include(source);
        var rendered = target.ToString();

        // Assert
        rendered.Should().Contain("fragment RepoCard on Repository");
        rendered.Should().Contain("fragment UserCard on User");
    }

    [Fact]
    public void Include_TransitiveNamedFragments_CarryReferencedDefinitions()
    {
        // Arrange — Card spreads Audit; both definitions must carry across Include.
        var source = QueryBuilder.CreateDefaultBuilder("Source")
            .AddFragment("Audit", "User", f => f.AddField("updatedAt"))
            .AddFragment("Card", "User", f => f.AddField("id").SpreadFragment("Audit"))
            .AddField("users", u => u.SpreadFragment("Card"));

        var target = QueryBuilder.CreateDefaultBuilder("Target")
            .AddField("admins", a => a.AddField("id"));

        // Act
        target.Include(source);
        var rendered = target.ToString();

        // Assert
        rendered.Should().Contain("fragment Card on User");
        rendered.Should().Contain("fragment Audit on User");
        rendered.Should().Contain("...Audit", "the spread nested inside Card carries over");
    }

    [Fact]
    public void Include_MergedFieldWithDirective_KeepsDirective()
    {
        // Arrange — the incoming `user` field carries an @include directive.
        var source = QueryBuilder.CreateDefaultBuilder("Source")
            .AddField("user", u => u.AddField("id").Include("showDetails"));

        var target = QueryBuilder.CreateDefaultBuilder("Target")
            .AddField("admins", a => a.AddField("id"));

        // Act
        target.Include(source);

        // Assert
        target.ToString().Should().Contain("@include(if:$showDetails)");
    }

    [Fact]
    public void Include_DirectiveOnMergedSameField_CarriesToExistingField()
    {
        // Arrange — the same `user` field exists on both sides; the incoming one carries a directive.
        var source = QueryBuilder.CreateDefaultBuilder("Source", MergingStrategy.MergeByFieldPath)
            .AddField("user", u => u.AddField("id").Skip("hideUser"));

        var target = QueryBuilder.CreateDefaultBuilder("Target", MergingStrategy.MergeByFieldPath)
            .AddField("user", u => u.AddField("name"));

        // Act
        target.Include(source);

        // Assert — the directive attached to the incoming user field survives into the merged field.
        target.ToString().Should().Contain("@skip(if:$hideUser)");
    }

    [Fact]
    public void Include_DeepClonesNamedFragments_SourceMutationDoesNotAffectTarget()
    {
        // Arrange
        var source = QueryBuilder.CreateDefaultBuilder("Source")
            .AddFragment("Card", "User", f => f.AddField("id"))
            .AddField("users", u => u.SpreadFragment("Card"));

        var target = QueryBuilder.CreateDefaultBuilder("Target")
            .AddField("admins", a => a.AddField("id"));

        target.Include(source);
        var mergedBefore = target.ToString();

        // Act — mutate the source fragment after the merge.
        source.AddFragment("Card", "User", f => f.AddField("secretField"));

        // Assert — the merged target is unchanged (deep-clone isolation).
        target.ToString().Should().Be(mergedBefore);
        target.ToString().Should().NotContain("secretField");
    }

    [Fact]
    public void Include_DeepClonesInlineFragments_SourceMutationDoesNotAffectTarget()
    {
        // Arrange
        var source = QueryBuilder.CreateDefaultBuilder("Source", MergingStrategy.MergeByFieldPath)
            .AddField("nodes", n => n.OnType("Repository", r => r.AddField("name")));

        var target = QueryBuilder.CreateDefaultBuilder("Target", MergingStrategy.MergeByFieldPath)
            .AddField("admins", a => a.AddField("id"));

        target.Include(source);
        var mergedBefore = target.ToString();

        // Act — extend the source inline fragment after the merge.
        source.AddField("nodes", n => n.OnType("Repository", r => r.AddField("leakedField")));

        // Assert
        target.ToString().Should().Be(mergedBefore);
        target.ToString().Should().NotContain("leakedField");
    }

    [Fact]
    public void Include_MergedQuery_RendersValidGraphQL_SpreadsReferenceDeclaredFragments()
    {
        // Arrange — a round-trip proof: every spread has a matching declared fragment.
        var source = QueryBuilder.CreateDefaultBuilder("Source")
            .AddFragment("RepoCard", "Repository", f => f.AddField("name").AddField("stargazerCount"))
            .AddField("search", s => s.OnType("Repository", r => r.SpreadFragment("RepoCard")));

        var target = QueryBuilder.CreateDefaultBuilder("Target")
            .AddField("viewer", v => v.AddField("login"));

        // Act
        target.Include(source);
        var rendered = target.ToString();

        // Assert — the spread and its definition are both present, and nesting is preserved.
        rendered.Should().Contain("...RepoCard");
        rendered.Should().Contain("... on Repository");
        rendered.Should().Contain("fragment RepoCard on Repository");
        rendered.Should().Contain("stargazerCount");
        rendered.Should().Contain("login");
    }

    private static int CountOccurrences(string haystack, string needle)
    {
        int count = 0, idx = 0;
        while ((idx = haystack.IndexOf(needle, idx, StringComparison.Ordinal)) >= 0)
        {
            count++;
            idx += needle.Length;
        }
        return count;
    }
}
