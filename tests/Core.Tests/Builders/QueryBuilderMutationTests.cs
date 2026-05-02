using System.Collections.Generic;
using FluentAssertions;
using NGql.Core.Builders;
using Xunit;

namespace NGql.Core.Tests.Builders;

public class QueryBuilderMutationTests
{
    [Fact]
    public void CreateMutationBuilder_Renders_MutationPrefix()
    {
        // Arrange & Act
        var mutation = QueryBuilder.CreateMutationBuilder("Logout")
            .AddField("logout")
            .ToString();

        // Assert
        mutation.Should().StartWith("mutation Logout");
        mutation.Should().NotContain("query");
    }

    [Fact]
    public void CreateDefaultBuilder_StillRenders_QueryPrefix()
    {
        // Arrange & Act
        var query = QueryBuilder.CreateDefaultBuilder("GetUser")
            .AddField("user.name")
            .ToString();

        // Assert
        query.Should().StartWith("query GetUser");
        query.Should().NotContain("mutation");
    }

    [Fact]
    public void MutationBuilder_With_Variables_And_Args_Renders_Identically_To_Classic_Mutation()
    {
        // Arrange — same operation built two ways: via the new builder and via the classic API.
        var nameVar  = new Variable("$name",  "String!");
        var emailVar = new Variable("$email", "String!");

        // New path
        var built = QueryBuilder.CreateMutationBuilder("CreateUser")
            .AddField("createUser",
                new Dictionary<string, object?> { ["name"] = nameVar, ["email"] = emailVar },
                new[] { "createdAt", "id" })
            .ToString();

        // Classic path (still supported, used here as the oracle)
        var oracle = new Mutation("CreateUser", nameVar, emailVar)
            .Select(new Query("createUser")
                .Where("name", nameVar)
                .Where("email", emailVar)
                .Select("id", "createdAt"))
            .ToString();

        // Assert
        built.Should().Be(oracle);
    }

    [Fact]
    public void MutationBuilder_With_MergingStrategy_Preserves_Strategy()
    {
        // Arrange
        var fragmentA = QueryBuilder.CreateMutationBuilder("FragA")
            .AddField("createUser.id");
        var fragmentB = QueryBuilder.CreateMutationBuilder("FragB")
            .AddField("createUser.email");

        // Act
        var merged = QueryBuilder
            .CreateMutationBuilder("Combined", MergingStrategy.MergeByFieldPath)
            .Include(fragmentA)
            .Include(fragmentB)
            .ToString();

        // Assert — the merging behavior is shared with queries, so this just confirms the
        // mutation builder honors the strategy and emits the mutation prefix.
        merged.Should().StartWith("mutation Combined");
        merged.Should().Contain("createUser");
        merged.Should().Contain("email");
        merged.Should().Contain("id");
    }

    [Fact]
    public void MutationBuilder_FluentSurface_MatchesQueryBuilder()
    {
        // The fluent surface must be identical — same AddField overloads, same Include,
        // same WithMetadata. This test exercises the most-used overloads to catch any
        // accidental divergence.
        var mutation = QueryBuilder.CreateMutationBuilder("TouchEverything")
            .AddField("simpleField")
            .AddField("withArgs", new Dictionary<string, object?> { ["x"] = 1 })
            .AddField("withSubFields", new[] { "a", "b" })
            .AddField("withBoth",
                new Dictionary<string, object?> { ["x"] = 1 },
                new[] { "a" })
            .WithMetadata(new Dictionary<string, object> { ["tag"] = "test" });

        // Assert
        var rendered = mutation.ToString();
        rendered.Should().StartWith("mutation TouchEverything");
        rendered.Should().Contain("simpleField");
        rendered.Should().Contain("withArgs(x:1)");
        rendered.Should().Contain("withSubFields");
        rendered.Should().Contain("withBoth(x:1)");
    }
}
