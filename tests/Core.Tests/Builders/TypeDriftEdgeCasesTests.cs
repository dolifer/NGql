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

        // Assert: PreservationBuilder preserves the CustomType
        var preserved = PreservationBuilder
            .Create(query)
            .Preserve("user")
            .Build();

        var preservedUserField = preserved.Definition.Fields["user"];
        preservedUserField.Type.Should().Be("CustomType", "type should be preserved during field preservation");
        preservedUserField.Fields.Should().ContainKey("name", "nested fields should also be preserved");
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

        // Assert: PreservationBuilder preserves the converted object type
        var preserved = PreservationBuilder
            .Create(query)
            .Preserve("user.profile")
            .Build();

        var preservedUserField = preserved.Definition.Fields["user"];
        var preservedProfileField = preservedUserField.Fields["profile"];
        preservedProfileField.Type.Should().Be("object", "primitive type converted to object should be preserved");
        preservedProfileField.Fields.Should().ContainKey("name");
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

        // Assert: PreservationBuilder preserves the array type marker
        var preserved = PreservationBuilder
            .Create(query)
            .Preserve("items")
            .Build();

        var preservedItemsField = preserved.Definition.Fields["items"];
        preservedItemsField.Type.Should().Be("[]", "array type marker [] should be preserved");
        preservedItemsField.Fields.Should().HaveCount(2)
            .And.ContainKeys("id", "name");
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

        // Assert: PreservationBuilder preserves all custom types in nested hierarchy
        var preserved = PreservationBuilder
            .Create(query)
            .Preserve("user.profile.name")
            .Build();

        var preservedUserField = preserved.Definition.Fields["user"];
        var preservedProfileField = preservedUserField.Fields["profile"];
        var preservedNameField = preservedProfileField.Fields["name"];

        preservedUserField.Type.Should().Be("User", "custom type User should be preserved at root level");
        preservedProfileField.Type.Should().Be("Profile", "custom type Profile should be preserved at intermediate level");
        preservedNameField.Type.Should().Be("Name", "custom type Name should be preserved at leaf level with subfields");
        preservedNameField.Fields.Should().HaveCount(2).And.ContainKeys("first", "last");
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

        // Assert: PreservationBuilder preserves custom type even with null arguments
        var preserved = PreservationBuilder
            .Create(query)
            .Preserve("user")
            .Build();

        var preservedUserField = preserved.Definition.Fields["user"];
        preservedUserField.Type.Should().Be("User", "custom type should be preserved even when original had null arguments");
        preservedUserField.Fields.Should().ContainKey("name");
    }

    [Fact]
    public void DottedPath_Field_Operations_With_Complex_Arguments()
    {
        // Exercises the FieldFactory slow path for dotted fields with arguments and nested args.
        var arguments = new System.Collections.Generic.Dictionary<string, object?>
        {
            { "first", 10 },
            { "filter", new System.Collections.Generic.Dictionary<string, object?> { { "status", "active" } } }
        };

        var query = QueryBuilder.CreateDefaultBuilder("Test")
            .AddField("users.profile.settings", arguments)
            .ToString();

        // Assert: Complex arguments should be preserved through dotted path
        query.Should().Contain("settings");
        query.Should().Contain("first:10");
        query.Should().Contain("status:\"active\"");
    }

    [Fact]
    public void Multiple_Dotted_Paths_With_Overlapping_Segments()
    {
        // This creates complex merging scenarios in FieldFactory
        var args1 = new System.Collections.Generic.Dictionary<string, object?> { { "limit", 5 } };
        var args2 = new System.Collections.Generic.Dictionary<string, object?> { { "offset", 10 } };

        var query = QueryBuilder.CreateDefaultBuilder("Test")
            .AddField("search.results.item", args1)
            .AddField("search.results.metadata", args2)
            .ToString();

        query.Should().Contain("item(limit:5)");
        query.Should().Contain("metadata(offset:10)");
    }

    [Fact]
    public void Very_Deep_Dotted_Nesting_Should_Process_Recursively()
    {
        // Tests deep recursion in FieldFactory
        var args = new System.Collections.Generic.Dictionary<string, object?> { { "id", 1 } };

        var query = QueryBuilder.CreateDefaultBuilder("Test")
            .AddField("a.b.c.d.e.f.g.h.i.j.k.l.m.n.o.p", args)
            .ToString();

        query.Should().Contain("p");
        query.Should().Contain("id:1");
    }

    [Fact]
    public void DottedField_With_Arguments_And_Metadata_And_Children()
    {
        // Tests complex interaction of arguments, metadata, and child fields
        var fieldArgs = new System.Collections.Generic.Dictionary<string, object?> { { "first", 20 } };
        var fieldMetadata = new System.Collections.Generic.Dictionary<string, object?> { { "cached", true } };

        var query = QueryBuilder.CreateDefaultBuilder("Test")
            .AddField("connection.edges.node", fieldArgs, fieldMetadata, builder =>
            {
                builder.AddField("id")
                       .AddField("name");
            })
            .ToString();

        query.Should().Contain("node(first:20)");
        query.Should().Contain("id");
        query.Should().Contain("name");
    }
}
