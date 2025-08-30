using FluentAssertions;
using NGql.Core.Builders;
using Xunit;

namespace NGql.Core.Tests.Builders;

public class ArrayMarkerTests
{
    [Fact]
    public void Multiple_AddField_Calls_Should_Not_Overwrite_Array_Marker()
    {
        // Arrange & Act
        var query = QueryBuilder.CreateDefaultBuilder("Test")
            .AddField("[] users")           // First: sets type to "[]"
            .AddField("String users");      // Second: might overwrite to "String"

        // Assert
        var usersField = query.Definition.Fields["users"];
        usersField.Type.Should().Be("[]", "array marker should not be overwritten by subsequent AddField calls");
    }

    [Fact]
    public void AddField_With_SubFields_After_Array_Marker_Should_Not_Overwrite()
    {
        // Arrange & Act
        var query = QueryBuilder.CreateDefaultBuilder("Test")
            .AddField("[] users")                    // Sets type to "[]"
            .AddField("users", subFields: ["name"]); // Might overwrite to "object"

        // Assert
        var usersField = query.Definition.Fields["users"];
        usersField.Type.Should().Be("[]", "array marker should not be overwritten when adding subfields");
    }

    [Fact]
    public void FieldBuilder_Adding_SubFields_Should_Not_Overwrite_Array_Marker()
    {
        // Arrange & Act
        var query = QueryBuilder.CreateDefaultBuilder("Test")
            .AddField("[] users", fieldBuilder => {
                fieldBuilder.AddField("name");       // Adding subfields might change parent to "object"
            });

        // Assert
        var usersField = query.Definition.Fields["users"];
        usersField.Type.Should().Be("[]", "array marker should not be overwritten by FieldBuilder operations");
    }

    [Fact]
    public void Query_Merging_With_Conflicting_Types_Should_Preserve_Array_Marker()
    {
        // Arrange
        var query1 = QueryBuilder.CreateDefaultBuilder("Q1")
            .AddField("[] users");

        var query2 = QueryBuilder.CreateDefaultBuilder("Q2")  
            .AddField("String users");

        // Act
        var merged = query1.Include(query2);

        // Assert
        var usersField = merged.Definition.Fields["users"];
        usersField.Type.Should().Be("[]", "array marker should win over conflicting types in merging");
    }

    [Fact]
    public void Query_Merging_Array_With_Object_Field_Should_Preserve_Array_Marker()
    {
        // Arrange
        var query1 = QueryBuilder.CreateDefaultBuilder("Q1")
            .AddField("[] users");

        var query2 = QueryBuilder.CreateDefaultBuilder("Q2")
            .AddField("users.name");  // This makes users an "object"

        // Act
        var merged = query1.Include(query2);

        // Assert
        var usersField = merged.Definition.Fields["users"];
        usersField.Type.Should().Be("[]", "array marker should not become 'object' when merged with subfields");
    }

    [Fact]
    public void FieldBuilder_Separate_Call_Should_Not_Overwrite_Array_Marker()
    {
        // Arrange & Act
        var query = QueryBuilder.CreateDefaultBuilder("Test")
            .AddField("[] users");                   // First: set array marker
        
        // This should be equivalent to the failing subFields scenario
        query.AddField("users", fieldBuilder => {
            fieldBuilder.AddField("name");           // Adding subfields in separate call
        });

        // Assert
        var usersField = query.Definition.Fields["users"];
        usersField.Type.Should().Be("[]", "array marker should not be overwritten by separate FieldBuilder call");
    }

    [Fact]
    public void Nested_FieldBuilder_Action_Should_Not_Overwrite_Array_Marker()
    {
        // Arrange & Act
        var query = QueryBuilder.CreateDefaultBuilder("Test")
            .AddField("user.[] posts")               // Set array marker on nested field
            .AddField("user.posts", fieldBuilder => {
                fieldBuilder.AddField("title");     // Adding subfields to array field in action
            });

        // Assert
        var userField = query.Definition.Fields["user"];
        var postsField = userField.Fields["posts"];
        postsField.Type.Should().Be("[]", "nested array marker should not be overwritten by FieldBuilder action");
    }

    [Fact]
    public void Nested_QueryBuilder_Include_Should_Not_Overwrite_Array_Marker()
    {
        // Arrange
        var query1 = QueryBuilder.CreateDefaultBuilder("Q1")
            .AddField("user.[] posts");              // Set array marker on nested field

        var query2 = QueryBuilder.CreateDefaultBuilder("Q2")
            .AddField("user.posts.title");          // Add subfield to array field

        // Act
        var merged = query1.Include(query2);

        // Assert
        var userField = merged.Definition.Fields["user"];
        var postsField = userField.Fields["posts"];
        postsField.Type.Should().Be("[]", "nested array marker should not be overwritten by QueryBuilder merging");
    }
}
