using System;
using System.Collections.Generic;
using FluentAssertions;
using NGql.Core.Builders;
using Xunit;

namespace NGql.Core.Tests.Builders;

/// <summary>
/// Tests for inline-fragment support introduced in NGql 2.1 (`FieldBuilder.OnType`).
/// Inline fragments narrow a field's selection set to a concrete GraphQL type, rendering as
/// `... on TypeName { … }`. Used when a field returns a union or interface and the caller
/// needs type-specific fields.
/// </summary>
public class InlineFragmentTests
{
    [Fact]
    public void OnType_SingleFragment_RendersInlineFragmentSyntax()
    {
        // Arrange & Act
        var query = QueryBuilder.CreateDefaultBuilder("Q")
            .AddField("nodes", n => n
                .OnType("Repository", r => r.AddField("name").AddField("stargazerCount")))
            .ToString();

        // Assert
        query.Should().Be(
            "query Q{\n" +
            "    nodes{\n" +
            "        ... on Repository{\n" +
            "            name\n" +
            "            stargazerCount\n" +
            "        }\n" +
            "    }\n" +
            "}");
    }

    [Fact]
    public void OnType_MultipleFragments_RendersAlphabeticallyByTypeName()
    {
        // Arrange & Act — added in non-alphabetical order; expect alphabetical render.
        var query = QueryBuilder.CreateDefaultBuilder("Q")
            .AddField("nodes", n => n
                .OnType("Repository", r => r.AddField("name"))
                .OnType("Issue", i => i.AddField("title"))
                .OnType("PullRequest", p => p.AddField("number")))
            .ToString();

        // Assert — Issue < PullRequest < Repository (ordinal)
        var issuePos       = query.IndexOf("... on Issue", StringComparison.Ordinal);
        var pullRequestPos = query.IndexOf("... on PullRequest", StringComparison.Ordinal);
        var repositoryPos  = query.IndexOf("... on Repository", StringComparison.Ordinal);
        issuePos.Should().BeLessThan(pullRequestPos);
        pullRequestPos.Should().BeLessThan(repositoryPos);
    }

    [Fact]
    public void OnType_TwoCallsForSameType_MergeIntoOneFragment()
    {
        // Arrange & Act — two OnType calls for the same type should produce one merged fragment.
        var query = QueryBuilder.CreateDefaultBuilder("Q")
            .AddField("nodes", n => n
                .OnType("Repository", r => r.AddField("name"))
                .OnType("Repository", r => r.AddField("url")))
            .ToString();

        // Assert — exactly one `... on Repository` block in the output
        var fragmentCount = CountOccurrences(query, "... on Repository");
        fragmentCount.Should().Be(1);
        query.Should().Contain("name");
        query.Should().Contain("url");
    }

    [Fact]
    public void OnType_NestedFragment_RendersRecursively()
    {
        // Arrange & Act — fragment inside a fragment
        var query = QueryBuilder.CreateDefaultBuilder("Q")
            .AddField("nodes", n => n
                .OnType("PullRequest", p => p
                    .AddField("title")
                    .OnType("Mergeable", m => m.AddField("conflict"))))
            .ToString();

        // Assert
        query.Should().Contain("... on PullRequest");
        query.Should().Contain("... on Mergeable");
        query.Should().Contain("conflict");
        // Nested fragment should sit *inside* the outer fragment's braces
        var outerStart = query.IndexOf("... on PullRequest", StringComparison.Ordinal);
        var innerStart = query.IndexOf("... on Mergeable", StringComparison.Ordinal);
        innerStart.Should().BeGreaterThan(outerStart);
    }

    [Fact]
    public void OnType_FieldsInsideFragmentSortAlphabetically()
    {
        // Arrange & Act
        var query = QueryBuilder.CreateDefaultBuilder("Q")
            .AddField("nodes", n => n
                .OnType("Repository", r => r
                    .AddField("zulu")
                    .AddField("alpha")
                    .AddField("mike")))
            .ToString();

        // Assert — fields sort alphabetically inside the fragment, same as elsewhere.
        var alphaPos = query.IndexOf("alpha", StringComparison.Ordinal);
        var mikePos  = query.IndexOf("mike", StringComparison.Ordinal);
        var zuluPos  = query.IndexOf("zulu", StringComparison.Ordinal);
        alphaPos.Should().BeLessThan(mikePos);
        mikePos.Should().BeLessThan(zuluPos);
    }

    [Fact]
    public void OnType_FragmentsRenderAfterPlainFields()
    {
        // Arrange & Act — `nodes` has both a plain `id` field AND a fragment.
        // Convention: plain fields first, then fragments.
        var query = QueryBuilder.CreateDefaultBuilder("Q")
            .AddField("nodes", n => n
                .AddField("id")
                .OnType("Repository", r => r.AddField("name")))
            .ToString();

        // Assert
        var idPos       = query.IndexOf("id", StringComparison.Ordinal);
        var fragmentPos = query.IndexOf("... on Repository", StringComparison.Ordinal);
        idPos.Should().BeLessThan(fragmentPos);
    }

    [Fact]
    public void OnType_ParentFieldWithArguments_StillWorks()
    {
        // Arrange & Act — fragment parent has its own arguments
        var query = QueryBuilder.CreateDefaultBuilder("Q")
            .AddField("search",
                new Dictionary<string, object?>
                {
                    ["query"] = "stars:>1",
                    ["first"] = 10,
                },
                b => b.AddField("nodes", n => n
                    .OnType("Repository", r => r.AddField("name"))))
            .ToString();

        // Assert
        query.Should().Contain("search(first:10, query:\"stars:>1\")");
        query.Should().Contain("... on Repository");
    }

    [Fact]
    public void OnType_FieldsInsideFragmentCanCarryArguments()
    {
        // Arrange & Act — a field inside a fragment can still take args
        var query = QueryBuilder.CreateDefaultBuilder("Q")
            .AddField("nodes", n => n
                .OnType("Repository", r => r
                    .AddField("commits", new Dictionary<string, object?> { ["last"] = 5 })))
            .ToString();

        // Assert
        query.Should().Contain("... on Repository");
        query.Should().Contain("commits(last:5)");
    }

    [Fact]
    public void OnType_FragmentOnlyParent_NoChildFields_RendersBlock()
    {
        // Arrange & Act — `nodes` has zero plain fields, only fragments.
        // The renderer must still produce a `nodes{ … }` block (not bare `nodes`).
        var query = QueryBuilder.CreateDefaultBuilder("Q")
            .AddField("nodes", n => n
                .OnType("Repository", r => r.AddField("name")))
            .ToString();

        // Assert — `nodes{` opens a block, no orphan bare `nodes\n`.
        query.Should().Contain("nodes{");
        query.Should().NotMatch("*nodes\n*");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void OnType_NullOrWhitespaceTypeName_Throws(string? typeName)
    {
        // Arrange
        var builder = QueryBuilder.CreateDefaultBuilder("Q");

        // Act
        Action act = () => builder.AddField("nodes", n => n.OnType(typeName!, _ => { }));

        // Assert
        act.Should().Throw<ArgumentException>()
           .WithMessage("*Inline fragment type name cannot be null or whitespace*");
    }

    [Fact]
    public void OnType_NullAction_Throws()
    {
        // Arrange
        var builder = QueryBuilder.CreateDefaultBuilder("Q");

        // Act
        Action act = () => builder.AddField("nodes", n => n.OnType("Repository", null!));

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void FieldDefinition_ExposesInlineFragments_ReadOnly()
    {
        // Arrange — exercise the public API surface (IReadOnlyDictionary view).
        var qb = QueryBuilder.CreateDefaultBuilder("Q")
            .AddField("nodes", n => n
                .OnType("Repository", r => r.AddField("name"))
                .OnType("Issue", i => i.AddField("title")));

        // Act — walk the public API down to the fragments collection.
        var nodes = qb.Definition.Fields["nodes"];

        // Assert — both fragments visible, accessible by type name.
        nodes.HasInlineFragments.Should().BeTrue();
        nodes.InlineFragments.Should().HaveCount(2);
        nodes.InlineFragments.Should().ContainKey("Repository");
        nodes.InlineFragments.Should().ContainKey("Issue");
        nodes.InlineFragments["Repository"].Fields.Should().ContainKey("name");
        nodes.InlineFragments["Issue"].Fields.Should().ContainKey("title");
    }

    [Fact]
    public void FieldDefinition_HasInlineFragments_FalseWhenNoneAdded()
    {
        // Arrange & Act
        var qb = QueryBuilder.CreateDefaultBuilder("Q")
            .AddField("user", u => u.AddField("name"));

        // Assert — plain field with no fragments
        qb.Definition.Fields["user"].HasInlineFragments.Should().BeFalse();
        qb.Definition.Fields["user"].InlineFragments.Should().BeEmpty();
    }

    [Fact]
    public void InlineFragmentDefinition_EqualsByTypeName()
    {
        // Two fragments with the same type name compare equal — useful for de-dup logic.
        var a = new NGql.Core.Abstractions.InlineFragmentDefinition("Repository");
        var b = new NGql.Core.Abstractions.InlineFragmentDefinition("Repository");
        var c = new NGql.Core.Abstractions.InlineFragmentDefinition("Issue");

        a.Should().Be(b);
        a.GetHashCode().Should().Be(b.GetHashCode());
        a.Should().NotBe(c);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    public void InlineFragmentDefinition_WhitespaceTypeName_ThrowsAtConstructor(string typeName)
    {
        // OnType validates upstream (its own ArgumentException), but the constructor must
        // also reject whitespace so direct callers and serialization round-trips are safe.
        Action act = () => new NGql.Core.Abstractions.InlineFragmentDefinition(typeName);
        act.Should().Throw<ArgumentException>()
           .WithMessage("*Inline fragment type name cannot be null or whitespace*");
    }

    [Fact]
    public void FieldDefinition_InlineFragments_VisibleOnNestedFragment()
    {
        // Nested fragment scenario: outer .OnType("PullRequest", p => p.OnType("Mergeable", …))
        // must surface the inner fragment via the public `InlineFragments` getter on the outer.
        var qb = QueryBuilder.CreateDefaultBuilder("Q")
            .AddField("nodes", n => n
                .OnType("PullRequest", p => p
                    .AddField("title")
                    .OnType("Mergeable", m => m.AddField("conflict"))));

        var pullRequestFragment = qb.Definition.Fields["nodes"].InlineFragments["PullRequest"];

        pullRequestFragment.Fields.Should().ContainKey("title");
        pullRequestFragment.InlineFragments.Should().ContainKey("Mergeable");
        pullRequestFragment.InlineFragments["Mergeable"].Fields.Should().ContainKey("conflict");
    }

    [Fact]
    public void InlineFragmentDefinition_FreshInstance_HasEmptyFieldsAndFragments()
    {
        // The static empty-dict fallbacks for Fields/InlineFragments are exercised when a
        // fragment exists but has no content (e.g. a serialization round-trip producing an
        // empty selection set, or before the builder lambda runs).
        var fragment = new NGql.Core.Abstractions.InlineFragmentDefinition("Repository");

        fragment.Fields.Should().BeEmpty();
        fragment.InlineFragments.Should().BeEmpty();
    }

    private static int CountOccurrences(string haystack, string needle)
    {
        int count = 0;
        int idx = 0;
        while ((idx = haystack.IndexOf(needle, idx, StringComparison.Ordinal)) != -1)
        {
            count++;
            idx += needle.Length;
        }
        return count;
    }
}
