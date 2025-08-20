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
        
        var exception = Assert.Throws<ArgumentException>(() => name.CompareTo(new{}));
        
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
}
