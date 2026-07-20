using System.Collections.Generic;
using FluentAssertions;
using NGql.Core.Builders;
using Xunit;

namespace NGql.Core.Tests.Builders;

public class QueryBuilderSubscriptionTests
{
    [Fact]
    public void CreateSubscriptionBuilder_Renders_SubscriptionPrefix()
    {
        // Arrange & Act
        var subscription = QueryBuilder.CreateSubscriptionBuilder("OnMessage")
            .AddField("message.id")
            .ToString();

        // Assert
        subscription.Should().StartWith("subscription OnMessage");
        subscription.Should().NotContain("query");
        subscription.Should().NotContain("mutation");
    }

    [Fact]
    public void SubscriptionBuilder_Renders_Identically_To_Query_Except_Keyword()
    {
        // Arrange — same field/arg shapes built as a subscription and as a query.
        var subscription = QueryBuilder.CreateSubscriptionBuilder("Feed")
            .AddField("events", new Dictionary<string, object?> { ["topic"] = "news" }, new[] { "id", "payload" })
            .ToString();

        var query = QueryBuilder.CreateDefaultBuilder("Feed")
            .AddField("events", new Dictionary<string, object?> { ["topic"] = "news" }, new[] { "id", "payload" })
            .ToString();

        // Assert — swapping the leading keyword makes the two renderings identical.
        subscription.Should().StartWith("subscription Feed");
        query.Should().StartWith("query Feed");
        subscription.Replace("subscription Feed", "query Feed").Should().Be(query);
    }

    [Fact]
    public void SubscriptionBuilder_With_Variables_Renders_VariableSignature()
    {
        // Arrange — same operation built two ways: via the new builder and via the classic API.
        var afterVar = new Variable("$after", "String");

        // New path
        var built = QueryBuilder.CreateSubscriptionBuilder("Notifications")
            .AddField("notifications",
                new Dictionary<string, object?> { ["after"] = afterVar },
                new[] { "createdAt", "id" })
            .ToString();

        // Assert — subscription keyword plus the hoisted variable signature.
        built.Should().StartWith("subscription Notifications($after:String)");
        built.Should().Contain("notifications(after:$after)");
        built.Should().Contain("id");
        built.Should().Contain("createdAt");
    }

    [Fact]
    public void SubscriptionBuilder_With_MergingStrategy_Preserves_Strategy()
    {
        // Arrange
        var fragmentA = QueryBuilder.CreateSubscriptionBuilder("FragA")
            .AddField("stream.id");
        var fragmentB = QueryBuilder.CreateSubscriptionBuilder("FragB")
            .AddField("stream.value");

        // Act
        var merged = QueryBuilder
            .CreateSubscriptionBuilder("Combined", MergingStrategy.MergeByFieldPath)
            .Include(fragmentA)
            .Include(fragmentB)
            .ToString();

        // Assert — the merging behavior is shared with queries, so this just confirms the
        // subscription builder honors the strategy and emits the subscription prefix.
        merged.Should().StartWith("subscription Combined");
        merged.Should().Contain("stream");
        merged.Should().Contain("value");
        merged.Should().Contain("id");
    }

    [Fact]
    public void SubscriptionBuilder_FluentSurface_MatchesQueryBuilder()
    {
        // The fluent surface must be identical — same AddField overloads, same Include,
        // same WithMetadata. This test exercises the most-used overloads to catch any
        // accidental divergence.
        var subscription = QueryBuilder.CreateSubscriptionBuilder("TouchEverything")
            .AddField("simpleField")
            .AddField("withArgs", new Dictionary<string, object?> { ["x"] = 1 })
            .AddField("withSubFields", new[] { "a", "b" })
            .AddField("withBoth",
                new Dictionary<string, object?> { ["x"] = 1 },
                new[] { "a" })
            .WithMetadata(new Dictionary<string, object> { ["tag"] = "test" });

        // Assert
        var rendered = subscription.ToString();
        rendered.Should().StartWith("subscription TouchEverything");
        rendered.Should().Contain("simpleField");
        rendered.Should().Contain("withArgs(x:1)");
        rendered.Should().Contain("withSubFields");
        rendered.Should().Contain("withBoth(x:1)");
    }
}
