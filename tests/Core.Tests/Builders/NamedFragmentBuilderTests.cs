using System;
using FluentAssertions;
using NGql.Core.Builders;
using Xunit;

namespace NGql.Core.Tests.Builders;

public class NamedFragmentBuilderTests
{
    [Fact]
    public void AddFragment_Single_Fragment_Stores_Definition_With_Fields()
    {
        var qb = QueryBuilder.CreateDefaultBuilder("GetUser")
            .AddFragment("UserSummary", "User", f => f
                .AddField("id")
                .AddField("name")
                .AddField("avatarUrl"));

        qb.Definition.NamedFragments.Should().ContainKey("UserSummary");
        var fragment = qb.Definition.NamedFragments["UserSummary"];
        fragment.OnType.Should().Be("User");
        fragment.Fields.Keys.Should().BeEquivalentTo("id", "name", "avatarUrl");
    }

    [Fact]
    public void AddFragment_Same_Name_Same_Type_Merges_Fields()
    {
        var qb = QueryBuilder.CreateDefaultBuilder("GetUser")
            .AddFragment("UserSummary", "User", f => f.AddField("id").AddField("name"))
            .AddFragment("UserSummary", "User", f => f.AddField("avatarUrl"));

        qb.Definition.NamedFragments.Should().HaveCount(1);
        qb.Definition.NamedFragments["UserSummary"].Fields.Keys
            .Should().BeEquivalentTo("id", "name", "avatarUrl");
    }

    [Fact]
    public void AddFragment_Same_Name_Different_Type_Throws()
    {
        var qb = QueryBuilder.CreateDefaultBuilder("GetUser")
            .AddFragment("UserSummary", "User", f => f.AddField("id"));

        var act = () => qb.AddFragment("UserSummary", "Admin", f => f.AddField("permissions"));

        act.Should().Throw<InvalidOperationException>()
           .WithMessage("*UserSummary*User*Admin*");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AddFragment_Invalid_Name_Throws(string? name)
    {
        var qb = QueryBuilder.CreateDefaultBuilder("GetUser");

        var act = () => qb.AddFragment(name!, "User", f => f.AddField("id"));

        act.Should().Throw<ArgumentException>().WithParameterName("name");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AddFragment_Invalid_OnType_Throws(string? onType)
    {
        var qb = QueryBuilder.CreateDefaultBuilder("GetUser");

        var act = () => qb.AddFragment("UserSummary", onType!, f => f.AddField("id"));

        act.Should().Throw<ArgumentException>().WithParameterName("onType");
    }

    [Fact]
    public void AddFragment_Null_Build_Throws()
    {
        var qb = QueryBuilder.CreateDefaultBuilder("GetUser");

        var act = () => qb.AddFragment("UserSummary", "User", null!);

        act.Should().Throw<ArgumentNullException>().WithParameterName("build");
    }

    [Fact]
    public void AddFragment_Supports_Nested_Inline_Fragments()
    {
        var qb = QueryBuilder.CreateDefaultBuilder("Search")
            .AddFragment("SearchResult", "Node", f => f
                .OnType("Repository", r => r.AddField("name").AddField("stargazerCount"))
                .OnType("Issue", i => i.AddField("title")));

        var fragment = qb.Definition.NamedFragments["SearchResult"];
        fragment.InlineFragments.Should().ContainKeys("Repository", "Issue");
        fragment.InlineFragments["Repository"].Fields.Keys.Should().BeEquivalentTo("name", "stargazerCount");
        fragment.InlineFragments["Issue"].Fields.Keys.Should().BeEquivalentTo("title");
    }

    [Fact]
    public void SpreadFragment_Adds_Reference_To_Field()
    {
        var qb = QueryBuilder.CreateDefaultBuilder("GetUsers")
            .AddField("users", u => u.SpreadFragment("UserSummary"));

        var users = qb.Definition.Fields["users"];
        users.SpreadFragments.Should().ContainSingle().Which.Should().Be("UserSummary");
    }

    [Fact]
    public void SpreadFragment_Multiple_Different_Names_Preserves_Order()
    {
        var qb = QueryBuilder.CreateDefaultBuilder("GetUser")
            .AddField("user", u => u
                .SpreadFragment("UserSummary")
                .SpreadFragment("AuditFields")
                .SpreadFragment("Permissions"));

        var user = qb.Definition.Fields["user"];
        user.SpreadFragments.Should().ContainInOrder("UserSummary", "AuditFields", "Permissions");
    }

    [Fact]
    public void SpreadFragment_Same_Name_Twice_Is_Idempotent()
    {
        var qb = QueryBuilder.CreateDefaultBuilder("GetUser")
            .AddField("user", u => u
                .SpreadFragment("UserSummary")
                .SpreadFragment("UserSummary"));

        qb.Definition.Fields["user"].SpreadFragments.Should().ContainSingle();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void SpreadFragment_Invalid_Name_Throws(string? name)
    {
        var qb = QueryBuilder.CreateDefaultBuilder("GetUser");

        var act = () => qb.AddField("user", u => u.SpreadFragment(name!));

        act.Should().Throw<ArgumentException>().WithParameterName("name");
    }

    [Fact]
    public void Fragment_And_Spread_Coexist_With_Plain_Fields_And_Inline_Fragments()
    {
        var qb = QueryBuilder.CreateDefaultBuilder("Search")
            .AddFragment("RepoCard", "Repository", f => f
                .AddField("name")
                .AddField("stargazerCount"))
            .AddField("nodes", n => n
                .OnType("Repository", r => r.SpreadFragment("RepoCard"))
                .OnType("Issue", i => i.AddField("title"))
                .AddField("__typename"));

        qb.Definition.NamedFragments.Should().ContainKey("RepoCard");
        var nodes = qb.Definition.Fields["nodes"];
        nodes.Fields.Keys.Should().Contain("__typename");
        nodes.InlineFragments.Should().ContainKeys("Repository", "Issue");
    }

    [Fact]
    public void SpreadFragment_Inside_InlineFragment_Is_Recorded_On_The_InlineFragment()
    {
        var qb = QueryBuilder.CreateDefaultBuilder("Search")
            .AddField("nodes", n => n
                .OnType("Repository", r => r.SpreadFragment("RepoCard")));

        var nodes = qb.Definition.Fields["nodes"];
        var repoFragment = nodes.InlineFragments["Repository"];
        repoFragment.SpreadFragments.Should().ContainSingle().Which.Should().Be("RepoCard");
    }

    [Fact]
    public void NamedFragment_Body_Can_Spread_Another_NamedFragment()
    {
        var qb = QueryBuilder.CreateDefaultBuilder("GetUser")
            .AddFragment("AuditFields", "User", f => f
                .AddField("createdAt")
                .AddField("updatedAt"))
            .AddFragment("UserCard", "User", f => f
                .AddField("id")
                .AddField("name")
                .SpreadFragment("AuditFields"));

        var userCard = qb.Definition.NamedFragments["UserCard"];
        userCard.Fields.Keys.Should().BeEquivalentTo("id", "name");
        userCard.SpreadFragments.Should().ContainSingle().Which.Should().Be("AuditFields");
    }
}
