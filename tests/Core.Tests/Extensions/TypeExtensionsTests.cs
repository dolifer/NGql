using System;
using FluentAssertions;
using NGql.Core.Abstractions;
using NGql.Core.Extensions;
using Xunit;

namespace NGql.Core.Tests.Extensions;

public class TypeExtensionsTests
{
    [Fact]
    public void ShouldConvertToObjectType_WithAlreadyObjectType_ReturnsFalse()
    {
        // Arrange
        var field = new FieldDefinition("items", Constants.ObjectFieldType);

        // Act
        var result = field.ShouldConvertToObjectType();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void ShouldConvertToObjectType_WithDefaultStringType_ReturnsTrue()
    {
        // Arrange
        var field = new FieldDefinition("items", Constants.DefaultFieldType);

        // Act
        var result = field.ShouldConvertToObjectType();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void ShouldConvertToObjectType_WithNullOrWhitespaceType_ReturnsTrue()
    {
        // Arrange
        var field1 = new FieldDefinition("items", "");
        var field2 = new FieldDefinition("items", "   ");

        // Act
        var result1 = field1.ShouldConvertToObjectType();
        var result2 = field2.ShouldConvertToObjectType();

        // Assert
        result1.Should().BeTrue();
        result2.Should().BeTrue();
    }

    [Fact]
    public void ShouldConvertToObjectType_WithArrayMarker_ReturnsFalse()
    {
        // Arrange
        var field = new FieldDefinition("items", Constants.ArrayTypeMarker);

        // Act
        var result = field.ShouldConvertToObjectType();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void ShouldConvertToObjectType_WithArrayType_ReturnsFalse()
    {
        // Arrange
        var field = new FieldDefinition("items", "String[]");

        // Act
        var result = field.ShouldConvertToObjectType();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void ShouldConvertToObjectType_WithNullableType_ReturnsFalse()
    {
        // Arrange
        var field = new FieldDefinition("item", "String?");

        // Act
        var result = field.ShouldConvertToObjectType();

        // Assert
        result.Should().BeFalse();
    }

    [Theory]
    [InlineData("int")]
    [InlineData("integer")]
    [InlineData("string")]
    [InlineData("boolean")]
    [InlineData("bool")]
    [InlineData("float")]
    [InlineData("double")]
    [InlineData("decimal")]
    public void ShouldConvertToObjectType_WithPrimitiveTypes_ReturnsTrue(string primitiveType)
    {
        // Arrange
        var field = new FieldDefinition("item", primitiveType);

        // Act
        var result = field.ShouldConvertToObjectType();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void ShouldConvertToObjectType_WithCustomType_ReturnsFalse()
    {
        // Arrange
        var field = new FieldDefinition("item", "UserType");

        // Act
        var result = field.ShouldConvertToObjectType();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void GetCoreTypeName_WithEmptyType_ReturnsDefault()
    {
        // Arrange
        var field = new FieldDefinition("item", "");

        // Act
        var result = field.GetCoreTypeName();

        // Assert
        result.Should().Be(Constants.DefaultFieldType);
    }

    [Fact]
    public void GetCoreTypeName_WithNullType_ReturnsDefault()
    {
        // Arrange
        var field = new FieldDefinition("item", null!);

        // Act
        var result = field.GetCoreTypeName();

        // Assert
        result.Should().Be(Constants.DefaultFieldType);
    }

    [Fact]
    public void GetCoreTypeName_WithSimpleType_ReturnsType()
    {
        // Arrange
        var field = new FieldDefinition("item", "CustomType");

        // Act
        var result = field.GetCoreTypeName();

        // Assert
        result.Should().Be("CustomType");
    }

    [Fact]
    public void GetCoreTypeName_WithStandaloneArrayMarker_NoStrip_ReturnsPrefixedMarker()
    {
        // Arrange
        var field = new FieldDefinition("items", Constants.ArrayTypeMarker);

        // Act
        var result = field.GetCoreTypeName(stripArrayMarker: false, stripNullableMarker: false);

        // Assert
        result.Should().Be("String[]");
    }

    [Fact]
    public void GetCoreTypeName_WithStandaloneArrayMarker_StripArrayMarker_ReturnsDefault()
    {
        // Arrange
        var field = new FieldDefinition("items", Constants.ArrayTypeMarker);

        // Act
        var result = field.GetCoreTypeName(stripArrayMarker: true, stripNullableMarker: false);

        // Assert
        result.Should().Be(Constants.DefaultFieldType);
    }

    [Fact]
    public void GetCoreTypeName_WithStandaloneNullableMarker_NoStrip_ReturnsPrefixedMarker()
    {
        // Arrange
        var field = new FieldDefinition("item", Constants.NullableTypeMarker);

        // Act
        var result = field.GetCoreTypeName(stripArrayMarker: false, stripNullableMarker: false);

        // Assert
        result.Should().Be("String?");
    }

    [Fact]
    public void GetCoreTypeName_WithStandaloneNullableMarker_StripNullableMarker_ReturnsDefault()
    {
        // Arrange
        var field = new FieldDefinition("item", Constants.NullableTypeMarker);

        // Act
        var result = field.GetCoreTypeName(stripArrayMarker: false, stripNullableMarker: true);

        // Assert
        result.Should().Be(Constants.DefaultFieldType);
    }

    [Fact]
    public void GetCoreTypeName_WithArrayType_StripArrayMarker_RemovesMarker()
    {
        // Arrange
        var field = new FieldDefinition("items", "User[]");

        // Act
        var result = field.GetCoreTypeName(stripArrayMarker: true);

        // Assert
        result.Should().Be("User");
    }

    [Fact]
    public void GetCoreTypeName_WithNullableType_StripNullableMarker_RemovesMarker()
    {
        // Arrange
        var field = new FieldDefinition("item", "User?");

        // Act
        var result = field.GetCoreTypeName(stripNullableMarker: true);

        // Assert
        result.Should().Be("User");
    }

    [Fact]
    public void GetCoreTypeName_WithComplexType_StripBothMarkers_RemovesBoth()
    {
        // Arrange
        var field = new FieldDefinition("item", "User[]?");

        // Act
        var result = field.GetCoreTypeName(stripArrayMarker: true, stripNullableMarker: true);

        // Assert
        result.Should().Be("User");
    }

    [Fact]
    public void GetCoreTypeName_WithThrowOnNullFieldDefinition()
    {
        // Arrange
        FieldDefinition? nullField = null;

        // Act & Assert
        var action = () => nullField!.GetCoreTypeName();
        action.Should().Throw<ArgumentNullException>();
    }
}
