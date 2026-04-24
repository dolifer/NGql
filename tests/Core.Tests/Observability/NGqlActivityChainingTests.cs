using FluentAssertions;
using NGql.Core.Observability;
using Xunit;

namespace NGql.Core.Tests.Observability;

public class NGqlActivityChainingTests
{
    [Fact]
    public void WithQueryTags_ReturnsActivityForChaining()
    {
        // Arrange & Act
        var action = () =>
        {
            using var activity = NGqlActivity.StartQuery("operation")
                .WithQueryTags("queryName", 5)
                .AddEvent("tagged");
            _ = activity.IsRecording;
        };

        // Assert - Verify no exception on chaining
        action.Should().NotThrow();
    }

    [Fact]
    public void WithFieldTags_ReturnsActivityForChaining()
    {
        // Arrange & Act
        var action = () =>
        {
            using var activity = NGqlActivity.StartField("fieldOp")
                .WithFieldTags("path.to.field", true, false)
                .AddEvent("processed");
            _ = activity.IsRecording;
        };

        // Assert
        action.Should().NotThrow();
    }

    [Fact]
    public void WithPoolingTags_ReturnsActivityForChaining()
    {
        // Arrange & Act
        var action = () =>
        {
            using var activity = NGqlActivity.StartPooling("builderPool", "acquire")
                .WithPoolingTags("builderPool", "hit", 10)
                .AddEvent("pooled");
            _ = activity.IsRecording;
        };

        // Assert
        action.Should().NotThrow();
    }

    [Theory]
    [InlineData("key1", "value1")]
    [InlineData("key2", null)]
    [InlineData("exception.type", "ArgumentException")]
    public void WithTag_ReturnsActivityForChaining(string key, object? value)
    {
        // Arrange & Act
        var action = () =>
        {
            using var activity = NGqlActivity.StartQuery("test")
                .WithTag(key, value)
                .WithTag("another", "tag")
                .AddEvent("tagged");
            _ = activity.IsRecording;
        };

        // Assert
        action.Should().NotThrow();
    }

    [Fact]
    public void MethodChaining_AllMethods_WorkTogether()
    {
        // Arrange & Act & Assert
        var action = () =>
        {
            using var activity = NGqlActivity.StartQuery("complexOp")
                .WithQueryTags("myQuery", 3)
                .AddEvent("start")
                .WithTag("userId", "123")
                .WithTag("requestId", System.Guid.NewGuid().ToString())
                .AddEvent("mid-process")
                .WithStatus(System.Diagnostics.ActivityStatusCode.Ok)
                .AddEvent("complete");
        };

        action.Should().NotThrow();
    }

    [Fact]
    public void MultipleWithCalls_BuildsTagsCorrectly()
    {
        // Arrange & Act
        var action = () =>
        {
            using var activity = NGqlActivity.StartField("multi")
                .WithTag("tag1", "value1")
                .WithTag("tag2", "value2")
                .WithTag("tag3", "value3")
                .WithFieldTags("nested.path", true, false)
                .AddEvent("multi-tagged");
            _ = activity.IsRecording;
        };

        // Assert
        action.Should().NotThrow();
    }

    [Fact]
    public void StartQuery_WithQueryTags_ChainingWorks()
    {
        // Arrange & Act & Assert
        using var activity = NGqlActivity.StartQuery("chainTest")
            .WithQueryTags("test", 1)
            .WithTag("custom", "data");

        _ = activity.IsRecording;
    }

    [Fact]
    public void StartField_WithFieldTags_ChainingWorks()
    {
        // Arrange & Act & Assert
        using var activity = NGqlActivity.StartField("fieldChainTest")
            .WithFieldTags("path", false, false)
            .WithTag("field.depth", 3);

        _ = activity.IsRecording;
    }

    [Fact]
    public void StartPooling_WithPoolingTags_ChainingWorks()
    {
        // Arrange & Act & Assert
        using var activity = NGqlActivity.StartPooling("pool", "get")
            .WithPoolingTags("stringPool", "miss", 0)
            .WithTag("pool.type", "lock-free");

        _ = activity.IsRecording;
    }
}
