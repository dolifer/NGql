using FluentAssertions;
using NGql.Core.Builders;
using Xunit;

namespace NGql.Core.Tests.Builders;

public class TypeDriftEdgeCasesTests
{
    [Fact]
    public void Concurrent_Field_Updates_Should_Not_Cause_Type_Drift()
    {
        // Scenario: Multiple updates to same field with different types
        var query = QueryBuilder.CreateDefaultBuilder("Test")
            .AddField("String user")
            .AddField("user", fieldBuilder => fieldBuilder.AddField("name"))
            .AddField("CustomType user"); // Should this override or preserve?

        var userField = query.Definition.Fields["user"];
        userField.Type.Should().Be("CustomType"); // Last explicit type wins
    }

    [Fact]
    public void Nested_Path_With_Conflicting_Types_Should_Handle_Gracefully()
    {
        // Scenario: user.profile has type String, but user.profile.name is added
        var query = QueryBuilder.CreateDefaultBuilder("Test")
            .AddField("String user.profile")
            .AddField("user.profile.name"); // This creates subfields under a String field

        var userField = query.Definition.Fields["user"];
        var profileField = userField.Fields["profile"];
        profileField.Type.Should().Be("object"); // String type should be converted to object when subfields are added
        profileField.Fields.Should().ContainKey("name");
    }

    [Fact]
    public void Array_Type_With_Subfields_Should_Not_Drift_To_Object()
    {
        // Scenario: Array field gets subfields added
        var query = QueryBuilder.CreateDefaultBuilder("Test")
            .AddField("[] items")
            .AddField("items", fieldBuilder =>
            {
                fieldBuilder.AddField("id").AddField("name");
            });

        var itemsField = query.Definition.Fields["items"];
        itemsField.Type.Should().Be("[]"); // Should preserve array type
    }

    [Fact]
    public void Query_Merging_Should_Not_Override_Explicit_Types()
    {
        // Scenario: Merging queries with different types for same field
        var query1 = QueryBuilder.CreateDefaultBuilder("Q1")
            .AddField("String user.name");

        var query2 = QueryBuilder.CreateDefaultBuilder("Q2")
            .AddField("user.name", fieldBuilder =>
            {
                fieldBuilder.AddField("first").AddField("last");
            });

        var merged = query1.Include(query2);
        var userField = merged.Definition.Fields["user"];
        var nameField = userField.Fields["name"];
        nameField.Type.Should().Be("String"); // Should preserve explicit type from first query
    }

    [Fact]
    public void Empty_String_Type_Should_Not_Be_Treated_As_Default()
    {
        // Edge case: empty string as type
        var query = QueryBuilder.CreateDefaultBuilder("Test")
            .AddField(" user", subFields: ["name"]); // Space before field name

        var userField = query.Definition.Fields["user"];
        userField.Type.Should().Be("object"); // Should default to object with subfields
    }

    [Fact]
    public void Multiple_Nested_Levels_Should_Preserve_Types()
    {
        // Deep nesting scenario - use custom types that should be preserved
        var query = QueryBuilder.CreateDefaultBuilder("Test")
            .AddField("User user")
            .AddField("Profile user.profile")
            .AddField("Name user.profile.name") // Custom type, not primitive
            .AddField("user.profile.name", subFields: ["first", "last"]);

        var userField = query.Definition.Fields["user"];
        var profileField = userField.Fields["profile"];
        var nameField = profileField.Fields["name"];

        userField.Type.Should().Be("User");
        profileField.Type.Should().Be("Profile");
        nameField.Type.Should().Be("Name"); // Custom type should be preserved even with subfields
    }

    [Fact]
    public void Alias_Fields_Should_Not_Affect_Type_Determination()
    {
        // Scenario: Aliased fields with types
        var query = QueryBuilder.CreateDefaultBuilder("Test")
            .AddField("String userName:user.name")
            .AddField("userName", subFields: ["prefix", "suffix"]);

        // This is tricky - the alias might cause lookup issues
        var userField = query.Definition.Fields["user"];
        var nameField = userField.Fields["name"];
        nameField.Type.Should().Be("String");
    }

    [Fact]
    public void Null_Arguments_Should_Not_Cause_Type_Drift()
    {
        // Edge case: null arguments with custom type
        var query = QueryBuilder.CreateDefaultBuilder("Test")
            .AddField("User user", arguments: null!)
            .AddField("user", subFields: ["name"]);

        var userField = query.Definition.Fields["user"];
        userField.Type.Should().Be("User"); // Custom type should be preserved
    }
}
