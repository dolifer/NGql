using System;
using FluentAssertions;
using Xunit;

namespace NGql.Core.Tests;

public class VariableTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Test_Create_Bad_Type(string? type)
    {
        var exception = Assert.Throws<ArgumentException>(() => new Variable("$name", type!));

        exception.Message.Should().Be("Variable type cannot be null or whitespace. (Parameter 'type')");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Test_Create_Bad_Name(string? name)
    {
        var exception = Assert.Throws<ArgumentException>(() => new Variable(name!, "String"));

        exception.Message.Should().Be("Variable name cannot be null or whitespace. (Parameter 'name')");
    }

    [Fact]
    public void Test_Create_StartsWithDollarSign()
    {
        var exception = Assert.Throws<ArgumentException>(() => new Variable("name", "String"));

        exception.Message.Should().Be("Variable name must start with '$'. (Parameter 'name')");
    }

    [Fact]
    public void Test_CompareTo()
    {
        var name = new Variable("$name", "String");

        var exception = Assert.Throws<ArgumentException>(() => name.CompareTo(new { }));

        exception.Message.Should().Be("Object must be of type Variable");
    }

    [Fact]
    public void Test_ToString()
    {
        var name = new Variable("$name", "String");
        var age = new Variable("$age", "Int");

        name.ToString().Should().Be("$name:String");
        age.ToString().Should().Be("$age:Int");
    }

    [Fact]
    public void Test_Equals()
    {
        var name1 = new Variable("$name", "String");
        var name2 = new Variable("$name", "String");
        var name3Int = new Variable("$name", "Int");

        name1.Equals(name2).Should().BeTrue();
        name2.Equals(name1).Should().BeTrue();

        name1.Equals(name3Int).Should().BeFalse();
        name2.Equals(name3Int).Should().BeFalse();

        (name1 == name2).Should().BeTrue();
        (name2 == name1).Should().BeTrue();

        (name1 != name3Int).Should().BeTrue();
        (name2 != name3Int).Should().BeTrue();

        (name3Int != name1).Should().BeTrue();
        (name3Int != name2).Should().BeTrue();
    }

    [Fact]
    public void Test_Equality()
    {
        var a = new Variable("$a", "String");
        var sameA = a; // Make it explicit that we're comparing the same instance
        var b = new Variable("$b", "String");

        (a != sameA).Should().BeFalse();

        (a > sameA).Should().BeFalse();
        (a < sameA).Should().BeFalse();

        (a > b).Should().BeFalse();
        (a >= b).Should().BeFalse();

        (a == sameA).Should().BeTrue();

        (a >= sameA).Should().BeTrue();
        (a <= sameA).Should().BeTrue();

        (a < b).Should().BeTrue();
        (a <= b).Should().BeTrue();
    }

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
        var action = () => new Variable(null!, "ID");
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
        var action = () => new Variable("$id", null!);
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
        // Reflexivity check via aliasing — avoid CS1718 by routing through a second reference.
        var v1Alias = v1;
        (v1 <= v1Alias).Should().BeTrue(); // Equal case
        (v2 > v1).Should().BeTrue();
        (v2 >= v1).Should().BeTrue();
        var v2Alias = v2;
        (v2 >= v2Alias).Should().BeTrue(); // Equal case
        (v1 == v1Alias).Should().BeTrue();
        (v1 != v2).Should().BeTrue();
    }
}
