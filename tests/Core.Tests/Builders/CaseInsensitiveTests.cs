using FluentAssertions;
using NGql.Core.Builders;
using Xunit;

namespace NGql.Core.Tests.Builders;

public class CaseInsensitiveTests
{
    [Fact]
    public void Fields_Should_Be_Case_Insensitive()
    {
        var query = QueryBuilder.CreateDefaultBuilder("Test")
            .AddField("user")
            .AddField("USER") // Same field, different case
            .AddField("User"); // Same field, different case

        // Should only have one field due to case-insensitive comparison
        query.Definition.Fields.Should().HaveCount(1);
        query.Definition.Fields.Should().ContainKey("user");
    }

    [Fact]
    public void Arguments_Should_Be_Case_Insensitive()
    {
        // Test that case-insensitive comparison works by using separate calls
        var query = QueryBuilder.CreateDefaultBuilder("Test")
            .AddField("user", new System.Collections.Generic.Dictionary<string, object?> { { "id", "123" } })
            .AddField("user", new System.Collections.Generic.Dictionary<string, object?> { { "ID", "456" } }); // Same key, different case

        var userField = query.Definition.Fields["user"];
        userField.Arguments.Should().HaveCount(1);
        userField.Arguments.Should().ContainKey("id"); // Should be accessible with lowercase
        userField.Arguments.Should().ContainKey("ID");  // Should be accessible with uppercase
        userField.Arguments["id"].Should().Be("456"); // Last value wins due to merging
    }
}
