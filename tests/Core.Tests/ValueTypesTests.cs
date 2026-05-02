using System;
using System.Linq;
using FluentAssertions;
using NGql.Core.Builders;
using Xunit;

namespace NGql.Core.Tests;

public class ValueTypesTests
{
    #region EnumValue Tests

    [Fact]
    public void EnumValue_FromString_Valid()
    {
        // Arrange & Act
        var enumValue = new EnumValue("ACTIVE");

        // Assert
        enumValue.Value.Should().Be("ACTIVE");
    }

    [Fact]
    public void EnumValue_FromString_WithWhitespace_ShouldThrow()
    {
        // Arrange & Act & Assert
        var action = () => new EnumValue("  ");
        action.Should().Throw<ArgumentException>()
            .WithMessage("*Enum value cannot be null or whitespace*");
    }

    [Fact]
    public void EnumValue_FromNull_ShouldThrow()
    {
        // Arrange & Act & Assert
        var action = () => new EnumValue(null!);
        action.Should().Throw<ArgumentNullException>();
    }
    
    [Fact]
    public void EnumValue_FromNullEnumValue_ShouldThrow()
    {
        // Arrange & Act & Assert
        EnumValue? source = null; // mirrors the post-deserialisation state where the source field can be null
        var action = () => new EnumValue(source!); // exercises the Enum branch: enumValue.ToString() ?? throw new ArgumentException("Enum value cannot be null.", nameof(value))
        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void EnumValue_FromEnum_Valid()
    {
        // Arrange & Act
        var enumValue = new EnumValue(System.Net.HttpStatusCode.OK);

        // Assert
        enumValue.Value.Should().Be("OK");
    }

    [Fact]
    public void EnumValue_FromEnum_InvalidType_ShouldThrow()
    {
        // Arrange & Act & Assert
        var action = () => new EnumValue(42); // int, not enum
        action.Should().Throw<ArgumentException>()
            .WithMessage("*Invalid enum value type*");
    }

    [Fact]
    public void EnumValue_ToString_ReturnsValue()
    {
        // Arrange
        var enumValue = new EnumValue("STATUS_PENDING");

        // Act
        var result = enumValue.ToString();

        // Assert
        result.Should().Be("STATUS_PENDING");
    }

    [Fact]
    public void EnumValue_Equality_SameValue_ShouldBeEqual()
    {
        // Arrange
        var enum1 = new EnumValue("ACTIVE");
        var enum2 = new EnumValue("ACTIVE");

        // Act & Assert
        (enum1 == enum2).Should().BeTrue();
        (enum1 != enum2).Should().BeFalse();
        enum1.Equals(enum2).Should().BeTrue();
    }
    
    [Fact]
    public void EnumValue_Equality_SameObject_ShouldBeEqual()
    {
        // Arrange
        var enum1 = new EnumValue("ACTIVE");
        var enum2 = (object)enum1;

        // Act & Assert
        enum1.Equals(enum2).Should().BeTrue();
    }

    [Fact]
    public void EnumValue_Equality_DifferentValue_ShouldNotBeEqual()
    {
        // Arrange
        var enum1 = new EnumValue("ACTIVE");
        var enum2 = new EnumValue("INACTIVE");

        // Act & Assert
        (enum1 == enum2).Should().BeFalse();
        (enum1 != enum2).Should().BeTrue();
        enum1.Equals(enum2).Should().BeFalse();
    }

    [Fact]
    public void EnumValue_Equality_CaseInsensitive()
    {
        // Arrange
        var enum1 = new EnumValue("Active");
        var enum2 = new EnumValue("ACTIVE");

        // Act & Assert
        enum1.Equals(enum2).Should().BeTrue();
    }

    [Fact]
    public void EnumValue_CompareTo_LessThan()
    {
        // Arrange
        var enum1 = new EnumValue("AAA");
        var enum2 = new EnumValue("BBB");

        // Act & Assert
        (enum1 < enum2).Should().BeTrue();
        (enum1 <= enum2).Should().BeTrue();
        (enum2 > enum1).Should().BeTrue();
        (enum2 >= enum1).Should().BeTrue();
    }

    [Fact]
    public void EnumValue_CompareTo_Equal()
    {
        // Arrange
        var enum1 = new EnumValue("EQUAL");
        var enum2 = new EnumValue("equal");

        // Act & Assert
        enum1.CompareTo(enum2).Should().Be(0);
        (enum1 <= enum2).Should().BeTrue();
        (enum1 >= enum2).Should().BeTrue();
    }

    [Fact]
    public void EnumValue_CompareTo_WithNull()
    {
        // Arrange
        var enumValue = new EnumValue("VALUE");

        // Act
        var result = enumValue.CompareTo(null);

        // Assert
        result.Should().Be(1); // Non-null is greater than null
    }

    [Fact]
    public void EnumValue_CompareTo_WithIncompatibleType_ShouldThrow()
    {
        // Arrange
        var enumValue = new EnumValue("VALUE");

        // Act & Assert
        var action = () => enumValue.CompareTo("not-an-enum");
        action.Should().Throw<ArgumentException>()
            .WithMessage("*Object must be of type EnumValue*");
    }

    [Fact]
    public void EnumValue_GetHashCode_SameCaseInsensitiveValue_ShouldMatch()
    {
        // Arrange
        var enum1 = new EnumValue("ACTIVE");
        var enum2 = new EnumValue("active");

        // Act & Assert
        enum1.GetHashCode().Should().Be(enum2.GetHashCode());
    }

    [Fact]
    public void EnumValue_GetHashCode_DifferentValue_ShouldNotMatch()
    {
        // Arrange
        var enum1 = new EnumValue("ACTIVE");
        var enum2 = new EnumValue("INACTIVE");

        // Act & Assert
        enum1.GetHashCode().Should().NotBe(enum2.GetHashCode());
    }

    [Fact]
    public void EnumValue_InCollection_CanFindByEquality()
    {
        // Arrange
        var enumValues = new[] 
        {
            new EnumValue("PENDING"),
            new EnumValue("ACTIVE"),
            new EnumValue("COMPLETED")
        };
        var searchValue = new EnumValue("active");

        // Act
        var found = System.Linq.Enumerable.Contains(enumValues, searchValue);

        // Assert
        found.Should().BeTrue();
    }

    [Fact]
    public void EnumValue_Equals_WithNullObject_ShouldReturnFalse()
    {
        // Arrange
        var enumValue = new EnumValue("ACTIVE");

        // Act & Assert
        object? nullObj = null;
        enumValue.Equals(nullObj).Should().BeFalse();
    }

    [Fact]
    public void EnumValue_Equals_WithNonEnumValueObject_ShouldThrow()
    {
        // Arrange
        var enumValue = new EnumValue("ACTIVE");

        // Act & Assert
        var action = () => enumValue.Equals((object)"not-enum");
        action.Should().Throw<ArgumentException>()
            .WithMessage("*Object must be of type*");
    }

    #endregion


    #region Integration Tests

    [Fact]
    public void EnumValue_UsedInQuery_WithQueryBuilder()
    {
        // Test EnumValue in realistic QueryBuilder scenario
        var query = QueryBuilder.CreateDefaultBuilder("TestQuery")
            .AddField("status", new System.Collections.Generic.Dictionary<string, object?>
            {
                ["state"] = new EnumValue("ACTIVE")
            });

        var result = query.ToString();
        result.Should().Contain("status");
        result.Should().Contain("ACTIVE");
    }

    [Fact]
    public void Variable_UsedInQuery_WithQueryBuilder()
    {
        // Test Variable in realistic Query scenario
        var query = new Query("TestQuery")
            .Variable("$userId", "ID")
            .Select("user");

        query.Variables.Should().ContainSingle(v => v.Name == "$userId" && v.Type == "ID");
    }

    [Fact]
    public void EnumValue_MultipleEnums_InCollection()
    {
        // Test multiple enum values in a single query
        var enumValues = new[]
        {
            new EnumValue("PENDING"),
            new EnumValue("ACTIVE"),
            new EnumValue("COMPLETED"),
            new EnumValue("CANCELLED")
        };

        var query = QueryBuilder.CreateDefaultBuilder("StatusQuery")
            .AddField("tasks", new System.Collections.Generic.Dictionary<string, object?>
            {
                ["statuses"] = enumValues.Cast<object>().ToArray()
            });

        var result = query.ToString();
        result.Should().Contain("PENDING");
        result.Should().Contain("ACTIVE");
        result.Should().Contain("COMPLETED");
        result.Should().Contain("CANCELLED");
    }

    [Fact]
    public void Variable_MultipleVariables_InQuery()
    {
        // Test multiple variables in a single query
        var query = new Query("MultiVarQuery")
            .Variable("$id", "ID")
            .Variable("$name", "String")
            .Variable("$limit", "Int")
            .Select("user");

        query.Variables.Should().HaveCount(3);
        query.Variables.Should().Contain(v => v.Name == "$id");
        query.Variables.Should().Contain(v => v.Name == "$name");
        query.Variables.Should().Contain(v => v.Name == "$limit");
    }

    #endregion

    #region String Escaping (GraphQL spec § 2.9.4)

    [Theory]
    [InlineData("plain", "\"plain\"")]
    [InlineData("with \"quotes\"", "\"with \\\"quotes\\\"\"")]
    [InlineData("path\\with\\slashes", "\"path\\\\with\\\\slashes\"")]
    [InlineData("line1\nline2", "\"line1\\nline2\"")]
    [InlineData("tab\there", "\"tab\\there\"")]
    [InlineData("carriage\rreturn", "\"carriage\\rreturn\"")]
    [InlineData("back\bspace", "\"back\\bspace\"")]
    [InlineData("form\ffeed", "\"form\\ffeed\"")]
    [InlineData("unicode  control", "\"unicode \\u0001 control\"")]
    [InlineData("emoji 👋 passes through", "\"emoji 👋 passes through\"")]
    [InlineData("", "\"\"")]
    public void StringArgument_Escapes_Per_GraphQL_Spec(string input, string expectedQuotedLiteral)
    {
        // Arrange & Act — render a string argument through the builder so the assertion
        // sees the exact wire form, including the surrounding quotes.
        var query = QueryBuilder.CreateDefaultBuilder("Q")
            .AddField("f", new System.Collections.Generic.Dictionary<string, object?> { ["s"] = input })
            .ToString();

        // Assert
        query.Should().Contain($"f(s:{expectedQuotedLiteral})");
    }

    #endregion
}
