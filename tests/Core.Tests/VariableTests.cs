using FluentAssertions;
using Xunit;

namespace NGql.Core.Tests;

public class VariableTests
{
    [Fact]
    public void Test_Equals()
    {
        var variable1 = new Variable("$name", "String");
        var variable2 = new Variable("$name", "String");
        var variable3 = new Variable("$name", "Int");

        variable1.Equals(variable2).Should().BeTrue();
        variable2.Equals(variable1).Should().BeTrue();
        variable1.Equals(variable3).Should().BeFalse();
        
        (variable1 == variable2).Should().BeTrue();
        (variable2 == variable1).Should().BeTrue();
        
        (variable1 != variable3).Should().BeTrue();
        (variable3 != variable1).Should().BeTrue();

        (variable2 != variable3).Should().BeTrue();
        (variable3 != variable2).Should().BeTrue();
    }
    
    [Fact]
    public void Test_Compare()
    {
        var variable1 = new Variable("$a", "String");
        var variable2 = new Variable("$b", "String");
        var variable3 = new Variable("$a", "String");

        (variable1 < variable2).Should().BeTrue();
        (variable1 > variable2).Should().BeFalse();
        (variable1 <= variable3).Should().BeTrue();
        
        (variable2 > variable3).Should().BeTrue();
        (variable3 < variable2).Should().BeTrue();
        (variable2 >= variable1).Should().BeTrue();
    }
}
