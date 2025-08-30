using System.Collections.Generic;
using FluentAssertions;
using NGql.Core.Builders;
using Xunit;

namespace NGql.Core.Tests.Builders;

public class QueryBuilderTests
{
    [Fact]
    public void QueryBuilder_AddField_With_Type_Only_Should_Work()
    {
        // Arrange & Act
        var query = QueryBuilder.CreateDefaultBuilder("TestQuery")
            .AddField("user", "User")
            .ToString();

        // Assert
        query.Should().Contain("user");
    }

    [Fact]
    public void QueryBuilder_AddField_With_Type_And_Metadata_Should_Work()
    {
        // Arrange
        var metadata = new Dictionary<string, object?> { { "description", "User field" } };

        // Act
        var query = QueryBuilder.CreateDefaultBuilder("TestQuery")
            .AddField("user", "User", metadata)
            .ToString();

        // Assert
        query.Should().Contain("user");
    }

    [Fact]
    public void QueryBuilder_AddField_With_Arguments_Metadata_And_Action_Should_Work()
    {
        // Arrange
        var arguments = new Dictionary<string, object?> { { "id", "123" } };
        var metadata = new Dictionary<string, object?> { { "description", "User field" } };

        // Act
        var query = QueryBuilder.CreateDefaultBuilder("TestQuery")
            .AddField("user", arguments, metadata, builder =>
            {
                builder.AddField("name")
                       .AddField("email");
            })
            .ToString();

        // Assert
        query.Should().Contain("user(id:\"123\")");
        query.Should().Contain("name");
        query.Should().Contain("email");
    }

    [Fact]
    public void QueryBuilder_AddField_With_Type_Metadata_And_Action_Should_Work()
    {
        // Arrange
        var metadata = new Dictionary<string, object?> { { "description", "User field" } };

        // Act
        var query = QueryBuilder.CreateDefaultBuilder("TestQuery")
            .AddField("user", "User", metadata, builder =>
            {
                builder.AddField("profile", profileBuilder =>
                {
                    profileBuilder.AddField("name")
                                 .AddField("bio");
                });
            })
            .ToString();

        // Assert
        query.Should().Contain("user");
        query.Should().Contain("profile");
        query.Should().Contain("name");
        query.Should().Contain("bio");
    }

    [Fact]
    public void QueryBuilder_AddField_With_SubFields_Should_Not_Overwrite_Explicit_Type()
    {
        // Arrange & Act
        var queryBuilder = QueryBuilder.CreateDefaultBuilder("TestQuery")
            .AddField("String user", subFields: ["nested", "field"]);

        // Assert - Get the underlying field to verify type preservation
        var userField = queryBuilder.Definition.Fields["user"];
        userField.Type.Should().Be("String"); // Should NOT be overwritten to "object"
    }

    [Fact]
    public void QueryBuilder_SubFields_Added_Later_Should_Not_Change_Explicit_Type()
    {
        // Arrange & Act
        var queryBuilder = QueryBuilder.CreateDefaultBuilder("TestQuery")
            .AddField("CustomType user")
            .AddField("user.profile.name")
            .AddField("user.profile.email");

        // Assert - Get the field using the proper public API
        var userField = queryBuilder.Definition.Fields["user"];
        userField.Type.Should().Be("CustomType"); // Should NOT drift to "object"
        userField.Fields.Should().ContainKey("profile");
    }
}
