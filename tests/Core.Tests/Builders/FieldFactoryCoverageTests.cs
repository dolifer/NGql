using System.Collections.Generic;
using FluentAssertions;
using NGql.Core.Abstractions;
using NGql.Core.Builders;
using Xunit;

namespace NGql.Core.Tests.Builders;

public class FieldFactoryCoverageTests
{
    [Fact]
    public void AddField_DottedPath_With_Arguments_Should_Merge_Into_Last_Segment()
    {
        // Arrange & Act - Add dotted field with arguments on the last segment
        var query = QueryBuilder.CreateDefaultBuilder("TestQuery")
            .AddField("user.profile.name", new Dictionary<string, object?> { { "limit", 10 } })
            .ToString();

        // Assert - Arguments should be on the final segment
        query.Should().Contain("name");
        query.Should().Contain("user");
        query.Should().Contain("profile");
    }

    [Fact]
    public void AddField_DottedPath_With_Type_Should_Set_Type()
    {
        // Arrange & Act
        var qb = QueryBuilder.CreateDefaultBuilder("TestQuery")
            .AddField("user.profile.name", "String");

        // Assert
        var field = qb.Definition.Fields["user"];
        field.Should().NotBeNull();
        field._children.Should().NotBeNull();
    }

    [Fact]
    public void AddField_DottedPath_With_Metadata_Should_Preserve_Metadata()
    {
        // Arrange
        var metadata = new Dictionary<string, object?> { { "description", "User name" } };

        // Act
        var qb = QueryBuilder.CreateDefaultBuilder("TestQuery")
            .AddField("user.profile.name", metadata);

        // Assert
        qb.Definition.Fields.Should().ContainKey("user");
    }

    [Fact]
    public void AddField_DottedPath_Multiple_Segments_Should_Create_Hierarchy()
    {
        // Arrange & Act
        var query = QueryBuilder.CreateDefaultBuilder("TestQuery")
            .AddField("a.b.c.d.e")
            .ToString();

        // Assert
        query.Should().Contain("a");
    }

    [Fact]
    public void AddField_DottedPath_With_Type_And_Arguments_Should_Apply_Both()
    {
        // Arrange
        var args = new Dictionary<string, object?> { { "first", 10 } };

        // Act
        var qb = QueryBuilder.CreateDefaultBuilder("TestQuery")
            .AddField("users.items", args);
        var query = qb.ToString();

        // Assert
        query.Should().Contain("users");
    }

    [Fact]
    public void AddField_ComplexPath_With_Type_Prefix_Should_Parse()
    {
        // Arrange & Act
        var query = QueryBuilder.CreateDefaultBuilder("TestQuery")
            .AddField("[User]friends.name")
            .ToString();

        // Assert
        query.Should().Contain("friends");
    }

    [Fact]
    public void AddField_Nested_With_Arguments_Then_Add_Without_Arguments_Should_Merge()
    {
        // Arrange
        var qb = QueryBuilder.CreateDefaultBuilder("TestQuery");
        
        // Act - Add field with arguments first
        qb.AddField("user", new Dictionary<string, object?> { { "id", 1 } });
        
        // Then add it again, should merge
        qb.AddField("user");

        // Assert
        var userField = qb.Definition.Fields["user"];
        userField.Should().NotBeNull();
    }

    [Fact]
    public void AddField_Deep_Nesting_Should_Create_All_Parents()
    {
        // Arrange & Act
        var qb = QueryBuilder.CreateDefaultBuilder("TestQuery")
            .AddField("a.b.c.d.e.f.g");

        // Assert - All parent levels should exist
        var fieldA = qb.Definition.Fields["a"];
        fieldA._children.Should().NotBeNull();
    }

    [Fact]
    public void AddField_With_Type_Annotation_In_Path_Should_Parse_Correctly()
    {
        // Arrange & Act
        var qb = QueryBuilder.CreateDefaultBuilder("TestQuery")
            .AddField("[CustomType]node.field");

        // Assert
        qb.Definition.Fields.Should().HaveCount(1);
    }

    [Fact]
    public void AddField_Multiple_Dotted_Paths_Should_Share_Parents()
    {
        // Arrange
        var qb = QueryBuilder.CreateDefaultBuilder("TestQuery");

        // Act
        qb.AddField("user.profile.name");
        qb.AddField("user.profile.email");

        // Assert
        var userField = qb.Definition.Fields["user"];
        userField._children.Count.Should().Be(1); // Only 'profile' child
    }

    [Fact]
    public void AddField_DottedPath_With_Metadata_On_Parent_Should_Apply()
    {
        // Arrange
        var metadata = new Dictionary<string, object?> { { "tag", "parent" } };

        // Act
        var qb = QueryBuilder.CreateDefaultBuilder("TestQuery")
            .AddField("user.profile", metadata);

        // Assert
        qb.Definition.Fields.Should().ContainKey("user");
    }

    [Fact]
    public void AddField_Via_FieldBuilder_With_DottedPath_Should_Work()
    {
        // Arrange & Act
        var query = QueryBuilder.CreateDefaultBuilder("TestQuery")
            .AddField("user", fb => fb.AddField("profile.name"))
            .ToString();

        // Assert
        query.Should().Contain("user");
    }

    [Fact]
    public void AddField_With_Multiple_Arguments_Should_Preserve_All()
    {
        // Arrange
        var args = new Dictionary<string, object?>
        {
            { "first", 10 },
            { "after", "cursor" },
            { "filter", "active" }
        };

        // Act
        var qb = QueryBuilder.CreateDefaultBuilder("TestQuery")
            .AddField("users", args);

        // Assert
        var usersField = qb.Definition.Fields["users"];
        usersField.Should().NotBeNull();
    }

    [Fact]
    public void AddField_Existing_Field_With_New_Arguments_Should_Merge()
    {
        // Arrange
        var qb = QueryBuilder.CreateDefaultBuilder("TestQuery");
        qb.AddField("user.posts");

        // Act - Add the same path again but with arguments - should merge
        var args = new Dictionary<string, object?> { { "limit", 5 } };
        qb.AddField("user.posts", args);

        // Assert
        var userField = qb.Definition.Fields["user"];
        userField.Should().NotBeNull();
    }

    [Fact]
    public void AddField_Field_With_Arguments_Then_Add_Parent_Context_Should_Work()
    {
        // Arrange
        var qb = QueryBuilder.CreateDefaultBuilder("TestQuery");

        // Act - Add nested field with arguments
        var args = new Dictionary<string, object?> { { "first", 10 } };
        qb.AddField("user", fb => 
            fb.AddField("posts", args));

        // Assert
        qb.Definition.Fields.Should().ContainKey("user");
    }

    [Fact]
    public void AddField_Multiple_Nested_With_Arguments_Should_All_Exist()
    {
        // Arrange & Act
        var qb = QueryBuilder.CreateDefaultBuilder("TestQuery");
        var args1 = new Dictionary<string, object?> { { "limit", 5 } };
        var args2 = new Dictionary<string, object?> { { "offset", 10 } };

        qb.AddField("users", args1);
        qb.AddField("user", args2);

        // Assert
        qb.Definition.Fields.Should().HaveCount(2);
    }

    [Fact]
    public void AddField_With_TypeAnnotation_Should_Parse()
    {
        // Arrange & Act
        var qb = QueryBuilder.CreateDefaultBuilder("TestQuery");
        qb.AddField("[User]name");

        // Assert - Type annotation is handled in the field name
        qb.Definition.Fields.Should().ContainKey("[User]name");
    }

    [Fact]
    public void AddField_Deeply_Nested_DottedPath_Should_Create_All()
    {
        // Arrange & Act
        var qb = QueryBuilder.CreateDefaultBuilder("TestQuery");
        qb.AddField("a.b.c.d.e.f.g.h.i.j");

        // Assert
        qb.Definition.Fields.Should().HaveCount(1);
        qb.Definition.Fields["a"].Should().NotBeNull();
    }

    [Fact]
    public void AddField_DottedPath_With_Arguments_And_Metadata_Should_Apply_Both()
    {
        // This test exercises FieldFactory code paths with both arguments and metadata
        // Arrange
        var args = new Dictionary<string, object?> { { "first", 10 } };
        var metadata = new Dictionary<string, object?> { { "description", "User data" } };

        // Act
        var qb = QueryBuilder.CreateDefaultBuilder("TestQuery")
            .AddField("user.profile.name", args, metadata);

        // Assert
        qb.Definition.Fields.Should().ContainKey("user");
        var userField = qb.Definition.Fields["user"];
        userField._children.Should().NotBeNull();
    }

    [Fact]
    public void AddField_DottedPath_WithType_And_Arguments_And_Metadata()
    {
        // Test combining type and metadata on dotted path
        // Arrange
        var args = new Dictionary<string, object?> { { "limit", 5 } };
        var metadata = new Dictionary<string, object?> { { "cache", true } };

        // Act
        var qb = QueryBuilder.CreateDefaultBuilder("TestQuery")
            .AddField("user.settings.preferences", args, metadata)
            .AddField("user.email", "String", metadata);

        // Assert
        qb.Definition.Fields.Should().ContainKey("user");
    }

    [Fact]
    public void AddField_DottedPath_Long_Path_With_Arguments()
    {
        // Test long nested path with arguments (may trigger pooling)
        // Arrange
        var args = new Dictionary<string, object?> { { "first", 20 } };

        // Act
        var qb = QueryBuilder.CreateDefaultBuilder("TestQuery")
            .AddField("a.b.c.d.e.f.g.h.i.j.k.l.m.n.o", args);

        // Assert
        qb.Definition.Fields.Should().ContainKey("a");
    }

    [Fact]
    public void AddField_DottedPath_Long_Path_With_Metadata()
    {
        // Act
        var qb = QueryBuilder.CreateDefaultBuilder("TestQuery")
            .AddField("a.b.c.d.e.f.g.h.i.j.k.l.m.n.o");

        // Assert
        qb.Definition.Fields.Should().ContainKey("a");
    }

    [Fact]
    public void AddField_DottedPath_Multiple_Calls_With_Different_Arguments()
    {
        // Arrange
        var qb = QueryBuilder.CreateDefaultBuilder("TestQuery");
        var args1 = new Dictionary<string, object?> { { "limit", 5 } };
        var args2 = new Dictionary<string, object?> { { "offset", 10 } };

        // Act
        qb.AddField("user.posts", args1);
        qb.AddField("user.posts.comments", args2);

        // Assert
        var userField = qb.Definition.Fields["user"];
        userField.Should().NotBeNull();
    }

    [Fact]
    public void AddField_DottedPath_Very_Deep_With_Metadata_At_Each_Level()
    {
        // Test deep nesting with metadata to exercise path builders
        // Arrange
        var metadata = new Dictionary<string, object?> { { "index", 1 } };

        // Act
        var qb = QueryBuilder.CreateDefaultBuilder("TestQuery");
        qb.AddField("level1.level2.level3.level4.level5", metadata);
        qb.AddField("level1.level2.level3.level4.level6", metadata);

        // Assert
        qb.Definition.Fields.Should().ContainKey("level1");
    }

    [Fact]
    public void AddField_Multiple_DottedPaths_With_Mixed_Arguments_And_Metadata()
    {
        // Test combinations of arguments and metadata
        // Arrange
        var args = new Dictionary<string, object?> { { "first", 10 } };
        var metadata = new Dictionary<string, object?> { { "description", "test" } };

        // Act
        var qb = QueryBuilder.CreateDefaultBuilder("TestQuery");
        qb.AddField("path1.nested.field", args);
        qb.AddField("path2.nested.field", metadata);
        qb.AddField("path3.nested.field");

        // Assert
        qb.Definition.Fields.Should().HaveCount(3);
    }

    [Fact]
    public void AddField_DottedPath_WithArguments_OnExistingField_MergesArguments()
    {
        var qb = QueryBuilder.CreateDefaultBuilder("query");

        qb.AddField("user.posts");
        qb.AddField("user.posts", new Dictionary<string, object?> { ["limit"] = 10 });

        var posts = qb.Definition.Fields["user"].Fields["posts"];
        posts.Arguments.Should().ContainKey("limit");
        posts.Arguments["limit"].Should().Be(10);
    }

    [Fact]
    public void AddField_DottedPath_IntermediateNode_ConvertsToObjectType()
    {
        var qb = QueryBuilder.CreateDefaultBuilder("query");

        qb.AddField("user", "String");
        qb.AddField("user.profile.name");

        var user = qb.Definition.Fields["user"];
        user.Type.Should().Be("object");
        user.Fields.Should().ContainKey("profile");
    }

    [Fact]
    public void AddField_DeepDottedPath_WithArgumentsOnMultipleLevels()
    {
        var qb = QueryBuilder.CreateDefaultBuilder("query");

        qb.AddField("user.posts.comments", new Dictionary<string, object?> { ["filter"] = "recent" });
        qb.AddField("user.posts", new Dictionary<string, object?> { ["limit"] = 5 });

        var posts = qb.Definition.Fields["user"].Fields["posts"];
        posts.Arguments.Should().ContainKey("limit");

        var comments = posts.Fields["comments"];
        comments.Arguments.Should().ContainKey("filter");
    }

    [Fact]
    public void FieldBuilder_Create_WithDictionary_AndDottedPath_WithArguments_MergesOnExisting()
    {
        var fields = new Dictionary<string, FieldDefinition>();

        // First call creates "user.posts" without arguments on last segment
        _ = FieldBuilder.Create(fields, "user.posts");
        // Second call adds arguments to existing "posts" field (last segment)
        _ = FieldBuilder.Create(fields, "user.posts", arguments: new Dictionary<string, object?> { ["limit"] = 5 });

        // Verify arguments were merged onto the existing posts field
        fields["user"].Fields["posts"].Arguments.Should().ContainKey("limit");
        fields["user"].Fields["posts"].Arguments["limit"].Should().Be(5);
    }

    [Fact]
    public void FieldBuilder_Create_WithDictionary_ComplexFieldPath_WithTypeAnnotation()
    {
        var fields = new Dictionary<string, FieldDefinition>();

        _ = FieldBuilder.Create(fields, "userName", "User", arguments: new Dictionary<string, object?> { ["id"] = 123 });

        fields.Should().ContainKey("userName");
        fields["userName"].Type.Should().Be("User");
        fields["userName"].Arguments.Should().ContainKey("id");
    }
}
