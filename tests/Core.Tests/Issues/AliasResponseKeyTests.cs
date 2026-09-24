using System;
using FluentAssertions;
using NGql.Core.Builders;
using Xunit;

namespace NGql.Core.Tests.Issues;

/// <summary>
/// A field's response key is its alias, or its name when it has none. Adding a path whose last
/// segment has a different response key used to find the existing field by name and write the
/// alias onto it, so <c>name</c> followed by <c>aaa:name</c> rendered only <c>aaa:name</c>, and two
/// aliases of one root field kept only the first. A segment now matches an existing field only
/// when both name and alias match, and an explicit alias never rewrites an existing field.
/// </summary>
public class AliasResponseKeyTests
{
    [Theory]
    [InlineData("name", "aaa:name")]
    [InlineData("aaa:name", "name")]
    public void AddField_RootFieldAndAliasedDuplicate_RendersBoth(string first, string second)
    {
        var query = QueryBuilder.CreateDefaultBuilder("Q").AddField(first).AddField(second);

        query.ToString().Should().Be("query Q{\n    aaa:name\n    name\n}");
    }

    [Theory]
    [InlineData("user.id", "user.aliasA:id")]
    [InlineData("user.aliasA:id", "user.id")]
    public void AddField_NestedFieldAndAliasedDuplicate_RendersBoth(string first, string second)
    {
        var query = QueryBuilder.CreateDefaultBuilder("Q").AddField(first).AddField(second);

        query.ToString().Should().Be("query Q{\n    user{\n        aliasA:id\n        id\n    }\n}");
    }

    [Fact]
    public void AddField_TwoAliasesOfOneRootField_RendersBoth()
    {
        var query = QueryBuilder.CreateDefaultBuilder("Q").AddField("a:user.id").AddField("b:user.name");

        query.ToString().Should().Be("query Q{\n    a:user{\n        id\n    }\n    b:user{\n        name\n    }\n}");
    }

    [Fact]
    public void AddField_AliasedPathAfterUnaliasedRoot_DoesNotRewriteIt()
    {
        var query = QueryBuilder.CreateDefaultBuilder("Q").AddField("user.name").AddField("u:user.email");

        query.ToString().Should().Be("query Q{\n    u:user{\n        email\n    }\n    user{\n        name\n    }\n}");
    }

    [Fact]
    public void AddField_SameAliasRepeated_AccumulatesUnderOneNode()
    {
        var query = QueryBuilder.CreateDefaultBuilder("Q")
            .AddField("primaryName:user.name")
            .AddField("primaryName:user.email");

        query.ToString().Should().Be("query Q{\n    primaryName:user{\n        email\n        name\n    }\n}");
    }

    [Fact]
    public void FieldBuilder_FieldAndAliasedDuplicate_RendersBoth()
    {
        var query = QueryBuilder.CreateDefaultBuilder("Q").AddField("user", b => b.AddField("id").AddField("aliasA:id"));

        query.ToString().Should().Be("query Q{\n    user{\n        aliasA:id\n        id\n    }\n}");
    }

    [Theory]
    [InlineData("x:user.id", "x:posts.id", "query Q{\n    x:posts{\n        id\n    }\n    x:user{\n        id\n    }\n}")]
    [InlineData("user.x:name", "user.x:email", "query Q{\n    user{\n        x:email\n        x:name\n    }\n}")]
    public void AddField_OneAliasOnTwoDifferentFields_KeepsBoth(string first, string second, string expected)
    {
        // Invalid GraphQL, which the server reports; the builder only guarantees nothing is dropped.
        var query = QueryBuilder.CreateDefaultBuilder("Q").AddField(first).AddField(second);

        query.ToString().Should().Be(expected);
    }

    [Fact]
    public void AddField_SecondAliasOfRootFieldExtendedAgain_AccumulatesUnderIt()
    {
        var query = QueryBuilder.CreateDefaultBuilder("Q")
            .AddField("a:user.id")
            .AddField("b:user.id")
            .AddField("b:user.name");

        query.ToString().Should().Be(
            "query Q{\n    a:user{\n        id\n    }\n    b:user{\n        id\n        name\n    }\n}");
    }

    [Fact]
    public void AddField_SecondAliasReusedForAnotherRootField_KeepsAll()
    {
        var query = QueryBuilder.CreateDefaultBuilder("Q")
            .AddField("a:user.id")
            .AddField("b:user.id")
            .AddField("b:posts.id");

        query.Definition.Fields.Should().HaveCount(3);
        query.ToString().Should().Contain("b:posts{").And.Contain("b:user{").And.Contain("a:user{");
    }

    [Fact]
    public void AddField_PlainFieldWhoseNameAnotherFieldUsesAsAlias_KeepsBoth()
    {
        var query = QueryBuilder.CreateDefaultBuilder("Q")
            .AddField("title")
            .AddField("name:title")
            .AddField("name");

        query.ToString().Should().Be("query Q{\n    name\n    name:title\n    title\n}");
    }

    [Fact]
    public void AddField_PlainFieldAfterAliasedOneWhoseAliasKeyIsTaken_KeepsAll()
    {
        var query = QueryBuilder.CreateDefaultBuilder("Q")
            .AddField("aaa")
            .AddField("aaa:name")
            .AddField("name");

        query.ToString().Should().Be("query Q{\n    aaa\n    aaa:name\n    name\n}");
    }

    [Fact]
    public void AddField_TypedPlainFieldAfterAliasedRoot_AddsPlainField()
    {
        var query = QueryBuilder.CreateDefaultBuilder("Q").AddField("aaa:name").AddField("String name");

        query.ToString().Should().Be("query Q{\n    aaa:name\n    name\n}");
    }

    [Fact]
    public void AddField_TypedPlainFieldWhoseNameAnotherFieldUsesAsAlias_KeepsBoth()
    {
        var query = QueryBuilder.CreateDefaultBuilder("Q").AddField("title").AddField("name:title").AddField("String name");

        query.ToString().Should().Be("query Q{\n    name\n    name:title\n    title\n}");
    }

    [Fact]
    public void AddField_ArgumentsOnSecondAliasOfRootField_LandOnThatAlias()
    {
        var query = QueryBuilder.CreateDefaultBuilder("Q")
            .AddField("a:user.id")
            .AddField("b:user.id")
            .AddField("b:user", new System.Collections.Generic.Dictionary<string, object?> { ["x"] = 1 });

        query.ToString().Should().Be(
            "query Q{\n    a:user{\n        id\n    }\n    b:user(x:1){\n        id\n    }\n}");
    }

    [Fact]
    public void AddField_ArgumentsOnPlainFieldStoredUnderNumberedKey_LandOnIt()
    {
        var query = QueryBuilder.CreateDefaultBuilder("Q")
            .AddField("title")
            .AddField("name:title")
            .AddField("name")
            .AddField("name", new System.Collections.Generic.Dictionary<string, object?> { ["x"] = 1 });

        query.ToString().Should().Be("query Q{\n    name(x:1)\n    name:title\n    title\n}");
    }

    [Fact]
    public void AddField_SubFieldArrayWithNullEntry_ThrowsArgumentNull()
    {
        var act = () => QueryBuilder.CreateDefaultBuilder("Q").AddField("user", new NGql.Core.Abstractions.FieldDefinition[] { null! });

        act.Should().Throw<System.ArgumentNullException>();
    }
}
