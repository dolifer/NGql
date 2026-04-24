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
        var action = () => new EnumValue(null);
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

    #region Variable Tests

    [Fact]
    public void Variable_Valid_WithDollarSign()
    {
        // Arrange & Act
        var variable = new Variable("$userId", "ID");

        // Assert
        variable.Name.Should().Be("$userId");
        variable.Type.Should().Be("ID");
    }

    [Fact]
    public void Variable_MissingDollarSign_ShouldThrow()
    {
        // Arrange & Act & Assert
        var action = () => new Variable("userId", "ID");
        action.Should().Throw<ArgumentException>()
            .WithMessage("*Variable name must start with '$'*");
    }

    [Fact]
    public void Variable_NullName_ShouldThrow()
    {
        // Arrange & Act & Assert
        var action = () => new Variable(null, "ID");
        action.Should().Throw<ArgumentException>()
            .WithMessage("*Variable name cannot be null or whitespace*");
    }

    [Fact]
    public void Variable_EmptyName_ShouldThrow()
    {
        // Arrange & Act & Assert
        var action = () => new Variable("", "ID");
        action.Should().Throw<ArgumentException>()
            .WithMessage("*Variable name cannot be null or whitespace*");
    }

    [Fact]
    public void Variable_NullType_ShouldThrow()
    {
        // Arrange & Act & Assert
        var action = () => new Variable("$id", null);
        action.Should().Throw<ArgumentException>()
            .WithMessage("*Variable type cannot be null or whitespace*");
    }

    [Fact]
    public void Variable_EmptyType_ShouldThrow()
    {
        // Arrange & Act & Assert
        var action = () => new Variable("$id", "");
        action.Should().Throw<ArgumentException>()
            .WithMessage("*Variable type cannot be null or whitespace*");
    }

    [Fact]
    public void Variable_ToString_FormatsCorrectly()
    {
        // Arrange
        var variable = new Variable("$userId", "ID!");

        // Act
        var result = variable.ToString();

        // Assert
        result.Should().Be("$userId:ID!");
    }

    [Fact]
    public void Variable_Equality_SameNameAndType_ShouldBeEqual()
    {
        // Arrange
        var var1 = new Variable("$id", "String");
        var var2 = new Variable("$id", "String");

        // Act & Assert
        (var1 == var2).Should().BeTrue();
        (var1 != var2).Should().BeFalse();
        var1.Equals(var2).Should().BeTrue();
    }

    [Fact]
    public void Variable_Equality_DifferentName_ShouldNotBeEqual()
    {
        // Arrange
        var var1 = new Variable("$id", "String");
        var var2 = new Variable("$name", "String");

        // Act & Assert
        (var1 == var2).Should().BeFalse();
        (var1 != var2).Should().BeTrue();
    }

    [Fact]
    public void Variable_Equality_DifferentType_ShouldNotBeEqual()
    {
        // Arrange
        var var1 = new Variable("$id", "String");
        var var2 = new Variable("$id", "Int");

        // Act & Assert
        (var1 == var2).Should().BeFalse();
        (var1 != var2).Should().BeTrue();
    }

    [Fact]
    public void Variable_Equality_CaseInsensitive()
    {
        // Arrange
        var var1 = new Variable("$ID", "STRING");
        var var2 = new Variable("$id", "string");

        // Act & Assert
        var1.Equals(var2).Should().BeTrue();
    }

    [Fact]
    public void Variable_CompareTo_ByName()
    {
        // Arrange
        var var1 = new Variable("$aaa", "Type");
        var var2 = new Variable("$bbb", "Type");

        // Act & Assert
        (var1 < var2).Should().BeTrue();
        (var2 > var1).Should().BeTrue();
    }

    [Fact]
    public void Variable_CompareTo_SameName_ByType()
    {
        // Arrange
        var var1 = new Variable("$id", "Int");
        var var2 = new Variable("$id", "String");

        // Act & Assert
        (var1 < var2).Should().BeTrue(); // "Int" < "String"
    }

    [Fact]
    public void Variable_CompareTo_WithNull()
    {
        // Arrange
        var variable = new Variable("$id", "String");

        // Act
        var result = variable.CompareTo(null);

        // Assert
        result.Should().Be(1); // Non-null is greater than null
    }

    [Fact]
    public void Variable_CompareTo_WithIncompatibleType_ShouldThrow()
    {
        // Arrange
        var variable = new Variable("$id", "String");

        // Act & Assert
        var action = () => variable.CompareTo("not-a-variable");
        action.Should().Throw<ArgumentException>()
            .WithMessage("*Object must be of type Variable*");
    }

    [Fact]
    public void Variable_GetHashCode_SameCaseInsensitiveValues_ShouldMatch()
    {
        // Arrange
        var var1 = new Variable("$ID", "STRING");
        var var2 = new Variable("$id", "string");

        // Act & Assert
        var1.GetHashCode().Should().Be(var2.GetHashCode());
    }

    [Fact]
    public void Variable_GetHashCode_DifferentValues_ShouldNotMatch()
    {
        // Arrange
        var var1 = new Variable("$id", "String");
        var var2 = new Variable("$name", "Int");

        // Act & Assert
        var1.GetHashCode().Should().NotBe(var2.GetHashCode());
    }

    [Fact]
    public void Variable_InCollection_CanFindByEquality()
    {
        // Arrange
        var variables = new[] 
        {
            new Variable("$id", "ID"),
            new Variable("$name", "String"),
            new Variable("$offset", "Int")
        };
        var searchVariable = new Variable("$ID", "id");

        // Act
        var found = System.Linq.Enumerable.Contains(variables, searchVariable);

        // Assert
        found.Should().BeTrue();
    }

    [Fact]
    public void Variable_AdvancedName_WithUnderscore_ShouldBeValid()
    {
        // Arrange & Act
        var variable = new Variable("$user_id_123", "String");

        // Assert
        variable.Name.Should().Be("$user_id_123");
    }

    [Fact]
    public void Variable_ComplexType_WithNullable_ShouldBeValid()
    {
        // Arrange & Act
        var variable = new Variable("$input", "[UserInput!]!");

        // Assert
        variable.Type.Should().Be("[UserInput!]!");
    }

    [Fact]
    public void Variable_AllOperators_Comprehensive()
    {
        // Test all comparison operators work together
        var v1 = new Variable("$a", "Type");
        var v2 = new Variable("$b", "Type");

        // Arrange & Assert - All operator combinations
        (v1 < v2).Should().BeTrue();
        (v1 <= v2).Should().BeTrue();
        (v1 <= v1).Should().BeTrue(); // Equal case
        (v2 > v1).Should().BeTrue();
        (v2 >= v1).Should().BeTrue();
        (v2 >= v2).Should().BeTrue(); // Equal case
        (v1 == v1).Should().BeTrue();
        (v1 != v2).Should().BeTrue();
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
}
