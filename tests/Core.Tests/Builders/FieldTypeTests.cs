using System.Collections.Generic;
using FluentAssertions;
using NGql.Core.Abstractions;
using NGql.Core.Builders;
using Xunit;

namespace NGql.Core.Tests.Builders;

public class FieldTypeTests
{
    [Fact]
    public void Array_Type_Is_Preserved_When_Adding_Nested_Fields()
    {
        // Arrange
        var fieldBuilder = FieldBuilder.Create(new SortedDictionary<string, FieldDefinition>(), "root", "Root");

        // Act: First add an array field, then add nested fields to it
        var result = fieldBuilder
            .AddField("items", Constants.ArrayTypeMarker) // Mark as array
            .AddField("items.name", "String")
            .AddField("items.age", "Int")
            .Build();

        // Assert: The array type marker should be preserved
        result.Fields.Should().ContainKey("items");
        var items = result.Fields["items"];
        items.Type.Should().Be(Constants.ArrayTypeMarker); // Still an array
        items.Fields.Should().ContainKeys("name", "age"); // But has nested fields
    }

    [Fact]
    public void Custom_Array_Type_Is_Preserved_When_Adding_Nested_Fields()
    {
        // Arrange
        var fieldBuilder = FieldBuilder.Create(new SortedDictionary<string, FieldDefinition>(), "root", "Root");

        // Act: Add a custom array type with nested fields
        var result = fieldBuilder
            .AddField("people", "Person[]") // Custom array type
            .AddField("people.firstName")
            .AddField("people.lastName")
            .Build();

        // Assert: The custom array type should be preserved
        result.Fields.Should().ContainKey("people");
        var people = result.Fields["people"];
        people.Type.Should().Be("Person[]"); // Still Person[]
        people.Fields.Should().ContainKeys("firstName", "lastName"); // But has nested fields
    }

    [Fact]
    public void Nullable_Type_Is_Preserved_When_Adding_Nested_Fields()
    {
        // Arrange
        var fieldBuilder = FieldBuilder.Create(new SortedDictionary<string, FieldDefinition>(), "root", "Root");

        // Act: Add a nullable type with nested fields
        var result = fieldBuilder
            .AddField("metadata", "Metadata?") // Nullable type
            .AddField("metadata.createdAt", "DateTime")
            .AddField("metadata.updatedAt", "DateTime?")
            .Build();

        // Assert: The nullable type should be preserved
        result.Fields.Should().ContainKey("metadata");
        var metadata = result.Fields["metadata"];
        metadata.Type.Should().Be("Metadata?"); // Still Metadata?
        metadata.Fields.Should().ContainKeys("createdAt", "updatedAt"); // But has nested fields
    }

    [Fact]
    public void Default_Type_Is_Converted_To_Object_When_Adding_Nested_Fields()
    {
        // Arrange
        var fieldBuilder = FieldBuilder.Create(new SortedDictionary<string, FieldDefinition>(), "root", "Root");

        // Act: Add a field with default type, then add nested fields
        var result = fieldBuilder
            .AddField("profile") // Default type (String)
            .AddField("profile.name")
            .Build();

        // Assert: The default type should be converted to object
        result.Fields.Should().ContainKey("profile");
        var profile = result.Fields["profile"];
        profile.Type.Should().Be(Constants.ObjectFieldType); // Converted to object
        profile.Fields.Should().ContainKey("name"); // And has nested fields
    }

    [Fact]
    public void Already_Object_Type_Remains_Object_When_Adding_Nested_Fields()
    {
        // Arrange
        var fieldBuilder = FieldBuilder.Create(new SortedDictionary<string, FieldDefinition>(), "root", "Root");

        // Act: Add a field explicitly as object, then add nested fields
        var result = fieldBuilder
            .AddField("contact", Constants.ObjectFieldType) // Explicitly object
            .AddField("contact.email")
            .AddField("contact.phone")
            .Build();

        // Assert: The object type should remain
        result.Fields.Should().ContainKey("contact");
        var contact = result.Fields["contact"];
        contact.Type.Should().Be(Constants.ObjectFieldType); // Still object
        contact.Fields.Should().ContainKeys("email", "phone"); // And has nested fields
    }

    [Fact]
    public void Complex_Nested_Structure_Preserves_Types_Correctly()
    {
        // Arrange
        var fieldBuilder = FieldBuilder.Create(new SortedDictionary<string, FieldDefinition>(), "document", "Document");

        // Act: Create a complex nested structure with various types
        var result = fieldBuilder
            .AddField("title")
            .AddField("authors", "Author[]") // Array
            .AddField("authors.name")
            .AddField("authors.email")
            .AddField("metadata", "Metadata?") // Nullable
            .AddField("metadata.created", "DateTime")
            .AddField("sections") // Default -> will become object
            .AddField("sections.heading")
            .AddField("sections.paragraphs", "string[]") // Array of strings
            .Build();

        // Assert: Check all types are preserved correctly
        result.Fields["title"].Type.Should().Be(Constants.DefaultFieldType);

        var authors = result.Fields["authors"];
        authors.Type.Should().Be("Author[]"); // Still array
        authors.Fields["name"].Type.Should().Be(Constants.DefaultFieldType);

        var metadata = result.Fields["metadata"];
        metadata.Type.Should().Be("Metadata?"); // Still nullable
        metadata.Fields["created"].Type.Should().Be("DateTime");

        var sections = result.Fields["sections"];
        sections.Type.Should().Be(Constants.ObjectFieldType); // Converted to object
        sections.Fields["paragraphs"].Type.Should().Be("string[]"); // Still array
    }
}
