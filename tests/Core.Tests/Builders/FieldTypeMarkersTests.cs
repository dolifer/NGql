using System.Collections.Generic;
using FluentAssertions;
using NGql.Core.Abstractions;
using NGql.Core.Builders;
using NGql.Core.Extensions;
using Xunit;

namespace NGql.Core.Tests.Builders;

public class FieldTypeMarkersTests
{
    [Fact]
    public void Pure_Array_Marker_Is_Preserved_When_Adding_Nested_Fields()
    {
        // Arrange
        var fieldBuilder = FieldBuilder.Create(new SortedDictionary<string, FieldDefinition>(), "root", "Root");

        // Act: Using just "[]" as the type marker
        var result = fieldBuilder
            .AddField("items", "[]") // Pure array marker
            .AddField("items.name")
            .AddField("items.age", "int")
            .Build();

        // Assert: The array marker should be preserved
        result.Fields.Should().ContainKey("items");
        var items = result.Fields["items"];
        items.Type.Should().Be("[]"); // Still pure array marker
        items.IsArray.Should().BeTrue();
        items.Fields.Should().ContainKeys("name", "age");
    }

    [Fact]
    public void Pure_Nullable_Marker_Is_Preserved_When_Adding_Nested_Fields()
    {
        // Arrange
        var fieldBuilder = FieldBuilder.Create(new SortedDictionary<string, FieldDefinition>(), "root", "Root");

        // Act: Using just "?" as the type marker
        var result = fieldBuilder
            .AddField("details", "?") // Pure nullable marker
            .AddField("details.createdAt")
            .AddField("details.updatedAt")
            .Build();

        // Assert: The nullable marker should be preserved
        result.Fields.Should().ContainKey("details");
        var details = result.Fields["details"];
        details.Type.Should().Be("?"); // Still pure nullable marker
        details.IsNullable.Should().BeTrue();
        details.Fields.Should().ContainKeys("createdAt", "updatedAt");
    }

    [Fact]
    public void GetCoreTypeName_Preserves_Type_Markers_By_Default()
    {
        // Pure markers
        new FieldDefinition("items", "[]").GetCoreTypeName().Should().Be("String[]");
        new FieldDefinition("nullable", "?").GetCoreTypeName().Should().Be("String?");

        // Regular types
        new FieldDefinition("name", "String").GetCoreTypeName().Should().Be("String");
        new FieldDefinition("age", "Int").GetCoreTypeName().Should().Be("Int");

        // Array types
        new FieldDefinition("friends", "Person[]").GetCoreTypeName().Should().Be("Person[]");
        new FieldDefinition("scores", "int[]").GetCoreTypeName().Should().Be("int[]");

        // Nullable types
        new FieldDefinition("address", "Address?").GetCoreTypeName().Should().Be("Address?");
        new FieldDefinition("count", "int?").GetCoreTypeName().Should().Be("int?");
    }

    [Fact]
    public void GetCoreTypeName_Can_Strip_Type_Markers_When_Requested()
    {
        // Test with stripArrayMarker parameter
        new FieldDefinition("items", "[]").GetCoreTypeName(stripArrayMarker: true).Should().Be("String");
        new FieldDefinition("friends", "Person[]").GetCoreTypeName(stripArrayMarker: true).Should().Be("Person");

        // Test with stripNullableMarker parameter
        new FieldDefinition("nullable", "?").GetCoreTypeName(stripNullableMarker: true).Should().Be("String");
        new FieldDefinition("address", "Address?").GetCoreTypeName(stripNullableMarker: true).Should().Be("Address");

        // Test with both parameters
        new FieldDefinition("items", "[]").GetCoreTypeName(true, true).Should().Be("String");
        new FieldDefinition("nullable", "?").GetCoreTypeName(true, true).Should().Be("String");
    }

    [Fact]
    public void GetBaseTypeName_Always_Strips_All_Type_Markers()
    {
        // Pure markers
        new FieldDefinition("items", "[]").GetBaseTypeName().Should().Be("String");
        new FieldDefinition("nullable", "?").GetBaseTypeName().Should().Be("String");

        // Regular types
        new FieldDefinition("name", "String").GetBaseTypeName().Should().Be("String");
        new FieldDefinition("age", "Int").GetBaseTypeName().Should().Be("Int");

        // Array types
        new FieldDefinition("friends", "Person[]").GetBaseTypeName().Should().Be("Person");
        new FieldDefinition("scores", "int[]").GetBaseTypeName().Should().Be("int");

        // Nullable types
        new FieldDefinition("address", "Address?").GetBaseTypeName().Should().Be("Address");
        new FieldDefinition("count", "int?").GetBaseTypeName().Should().Be("int");
    }

    [Fact]
    public void Complex_Structure_With_Type_Markers_Is_Preserved()
    {
        // Arrange
        var fieldBuilder = FieldBuilder.Create(new SortedDictionary<string, FieldDefinition>(), "document", "Document");

        // Act: Create a complex structure with pure markers and combined types
        var result = fieldBuilder
            .AddField("simple") // Default String
            .AddField("items", "[]") // Pure array marker
            .AddField("items.id", "ID")
            .AddField("items.name")
            .AddField("metadata", "?") // Pure nullable marker
            .AddField("metadata.created", "DateTime")
            .AddField("categories", "Category[]") // Typed array
            .AddField("categories.name")
            .AddField("categories.priority", "int")
            .AddField("mainCategory", "Category?") // Nullable type
            .AddField("mainCategory.name")
            .Build();

        // Assert: All types should be preserved correctly
        result.Fields["simple"].Type.Should().Be(Constants.DefaultFieldType);

        var items = result.Fields["items"];
        items.Type.Should().Be("[]");
        items.IsArray.Should().BeTrue();
        items.Fields.Should().ContainKeys("id", "name");

        var metadata = result.Fields["metadata"];
        metadata.Type.Should().Be("?");
        metadata.IsNullable.Should().BeTrue();
        metadata.Fields["created"].Type.Should().Be("DateTime");

        var categories = result.Fields["categories"];
        categories.Type.Should().Be("Category[]");
        categories.IsArray.Should().BeTrue();
        categories.Fields["name"].Type.Should().Be(Constants.DefaultFieldType);
        categories.Fields["priority"].Type.Should().Be("int");

        var mainCategory = result.Fields["mainCategory"];
        mainCategory.Type.Should().Be("Category?");
        mainCategory.IsNullable.Should().BeTrue();
        mainCategory.Fields["name"].Type.Should().Be(Constants.DefaultFieldType);
    }

    [Fact]
    public void IsPureTypeMarker_Correctly_Identifies_Marker_Types()
    {
        // Pure markers should return true
        new FieldDefinition("items", "[]").IsPureTypeMarker().Should().BeTrue();
        new FieldDefinition("nullable", "?").IsPureTypeMarker().Should().BeTrue();

        // Regular types should return false
        new FieldDefinition("name", "String").IsPureTypeMarker().Should().BeFalse();
        new FieldDefinition("age", "Int").IsPureTypeMarker().Should().BeFalse();

        // Complex types should return false
        new FieldDefinition("friends", "Person[]").IsPureTypeMarker().Should().BeFalse();
        new FieldDefinition("address", "Address?").IsPureTypeMarker().Should().BeFalse();
    }
}
