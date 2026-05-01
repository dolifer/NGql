using System;
using System.Collections.Generic;
using FluentAssertions;
using NGql.Core.Abstractions;
using NGql.Core.Extensions;
using Xunit;

namespace NGql.Core.Tests.Builders;

public class SpanExtensionsTests
{
    [Theory]
    [InlineData("fieldname", false, false, false)]
    [InlineData("user.profile.name", false, true, false)]
    [InlineData("String:fieldname", false, false, true)]
    [InlineData("my field", true, false, false)]
    [InlineData("my.field:Type", false, true, true)]
    [InlineData("user.name profile:Type", true, true, true)]
    [InlineData("", false, false, false)]
    public void ClassifyFieldFast_VariousInputs(string input, bool expectedHasSpaces, bool expectedHasDots, bool expectedHasColons)
    {
        // Arrange
        var span = input.AsSpan();

        // Act
        var (hasSpaces, hasDots, hasColons) = span.ClassifyFieldFast();

        // Assert
        hasSpaces.Should().Be(expectedHasSpaces, $"hasSpaces for '{input}'");
        hasDots.Should().Be(expectedHasDots, $"hasDots for '{input}'");
        hasColons.Should().Be(expectedHasColons, $"hasColons for '{input}'");
    }

    [Theory]
    [InlineData("fieldname", false)]
    [InlineData("user.profile.name", true)]
    [InlineData("user.profile", true)]
    [InlineData("", false)]
    [InlineData("single", false)]
    public void IsDottedField_VariousInputs(string input, bool expected)
    {
        // Arrange
        var span = input.AsSpan();

        // Act
        var result = span.IsDottedField();

        // Assert
        result.Should().Be(expected, $"IsDottedField for '{input}'");
    }

    [Theory]
    [InlineData("fieldname", true)]
    [InlineData("user.profile", false)]
    [InlineData("String:field", false)]
    [InlineData("my field", false)]
    [InlineData("", true)]
    [InlineData("single_field", true)]
    public void IsSimpleField_VariousInputs(string input, bool expected)
    {
        // Arrange
        var span = input.AsSpan();

        // Act
        var result = span.IsSimpleField();

        // Assert
        result.Should().Be(expected, $"IsSimpleField for '{input}'");
    }

    [Theory]
    [InlineData("fieldname", "fieldname")]
    [InlineData("String:fieldname", "fieldname")]
    [InlineData("Type:user.profile.name", "user.profile.name")]
    [InlineData("", "")]
    [InlineData("single", "single")]
    public void ExtractFieldName_VariousInputs(string input, string expected)
    {
        // Arrange
        var span = input.AsSpan();

        // Act
        var result = span.ExtractFieldName();

        // Assert
        result.ToString().Should().Be(expected, $"ExtractFieldName for '{input}'");
    }

    [Theory]
    [InlineData("fieldname...", "fieldname")]
    [InlineData("fieldname   ", "fieldname")]
    [InlineData("fieldname. . ", "fieldname")]
    [InlineData("fieldname", "fieldname")]
    [InlineData("text  .  .  ", "text")]
    public void TrimEndDotsAndSpaces_VariousInputs(string input, string expected)
    {
        // Arrange
        var span = input.AsSpan();

        // Act
        var result = span.TrimEndDotsAndSpaces();

        // Assert
        result.ToString().Should().Be(expected, $"TrimEndDotsAndSpaces for '{input}'");
    }

    [Theory]
    [InlineData("FieldName", "fieldname", true)]
    [InlineData("FieldName", "other", false)]
    [InlineData("ABC", "abc", true)]
    [InlineData("user", "USER", true)]
    [InlineData("", "", true)]
    public void EqualsIgnoreCase_VariousInputs(string input1, string input2, bool expected)
    {
        // Arrange
        var span1 = input1.AsSpan();
        var span2 = input2.AsSpan();

        // Act
        var result = span1.EqualsIgnoreCase(span2);

        // Assert
        result.Should().Be(expected, $"EqualsIgnoreCase for '{input1}' vs '{input2}'");
    }

    [Theory]
    [InlineData("abc", true)]
    [InlineData("123", true)]
    [InlineData("field_123", true)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("...", false)]
    [InlineData("user", true)]
    public void HasLetterOrDigit_VariousInputs(string input, bool expected)
    {
        // Arrange
        var span = input.AsSpan();

        // Act
        var result = span.HasLetterOrDigit();

        // Assert
        result.Should().Be(expected, $"HasLetterOrDigit for '{input}'");
    }

    [Theory]
    [InlineData(240, "verylongfieldname", "String")]
    [InlineData(250, "complexfield", "ComplexType")]
    public void GetOrAddSimpleField_WithLongPathExceeds256_UsesStringConcatenation(int parentPathLength, string fieldName, string fieldType)
    {
        // Arrange - test both FieldChildren and Dictionary variants with different path lengths
        var children = new FieldChildren();
        var dict = new Dictionary<string, FieldDefinition>();
        var longParentPath = new string('x', parentPathLength);
        var fieldTypeSpan = fieldType.AsSpan();

        // Act - FieldChildren variant
        var resultFromChildren = children.GetOrAddSimpleField(fieldName.AsSpan(), fieldTypeSpan, null, longParentPath, null);

        // Act - Dictionary variant
        var resultFromDict = dict.GetOrAddSimpleField(fieldName.AsSpan(), fieldTypeSpan, null, longParentPath, null);

        // Assert
        resultFromChildren.Should().NotBeNull();
        resultFromChildren.Name.Should().Be(fieldName);
        resultFromChildren.Path.Should().Contain(longParentPath);

        resultFromDict.Should().NotBeNull();
        resultFromDict.Name.Should().Be(fieldName);
        resultFromDict.Path.Should().Contain(longParentPath);
        dict.Should().ContainKey(fieldName);
    }

    [Theory]
    [InlineData(256, true)]
    [InlineData(257, false)]
    [InlineData(512, false)]
    [InlineData(1024, false)]
    public void GetOrAddSimpleField_LongPathBoundary_RendersCorrectly(int pathLength, bool shouldFitInBuffer)
    {
        // Arrange
        var children = new FieldChildren();
        var dict = new Dictionary<string, FieldDefinition>();
        var fieldName = "field";
        var fieldType = "String";
        var parentPath = new string('p', pathLength);

        // Act - FieldChildren variant
        var resultChildren = children.GetOrAddSimpleField(fieldName.AsSpan(), fieldType.AsSpan(), null, parentPath, null);

        // Act - Dictionary variant
        var resultDict = dict.GetOrAddSimpleField(fieldName.AsSpan(), fieldType.AsSpan(), null, parentPath, null);

        // Assert both render correctly regardless of buffer strategy.
        // shouldFitInBuffer flag documents whether this length stays within the stack/inline buffer
        // threshold; the actual rendering must succeed in both regimes.
        var bufferRegime = shouldFitInBuffer ? "inline buffer" : "heap fallback";
        resultChildren.Should().NotBeNull(because: bufferRegime);
        resultChildren.Path.Should().StartWith(parentPath, because: bufferRegime);
        resultChildren.Name.Should().Be(fieldName, because: bufferRegime);

        resultDict.Should().NotBeNull(because: bufferRegime);
        resultDict.Path.Should().StartWith(parentPath, because: bufferRegime);
        resultDict.Name.Should().Be(fieldName, because: bufferRegime);
    }

    [Fact]
    public void GetOrAddSimpleField_WithMetadataOnExistingField_MergesMetadata()
    {
        var children = new FieldChildren();
        var fieldName = "user";
        var metadata1 = new Dictionary<string, object?> { ["key1"] = "value1" };
        var metadata2 = new Dictionary<string, object?> { ["key2"] = "value2" };

        _ = children.GetOrAddSimpleField(fieldName.AsSpan(), "User".AsSpan(), null, "", metadata1);
        var field2 = children.GetOrAddSimpleField(fieldName.AsSpan(), "User".AsSpan(), null, "", metadata2);

        field2.Metadata.Should().ContainKey("key1");
        field2.Metadata.Should().ContainKey("key2");
    }

    [Theory]
    [InlineData(200, "field123")]
    [InlineData(240, "longfield")]
    public void GetOrAddSimpleField_Dictionary_WithShortPath_UsesStackalloc(int parentPathLength, string fieldName)
    {
        var dict = new Dictionary<string, FieldDefinition>();
        var parentPath = new string('x', parentPathLength);

        var result = dict.GetOrAddSimpleField(fieldName.AsSpan(), "String".AsSpan(), null, parentPath, null);

        result.Should().NotBeNull();
        result.Name.Should().Be(fieldName);
        dict.Should().ContainKey(fieldName);
    }
}
