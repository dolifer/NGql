using System;
using FluentAssertions;
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
}
