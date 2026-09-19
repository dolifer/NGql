using FluentAssertions;
using NGql.Core.Abstractions;
using Xunit;

namespace NGql.Core.Tests.Issues;

public class RootVariableLookupTests
{
    [Fact]
    public void RootVariables_ReplaceScalarWithMatchingKey()
    {
        var block = new QueryBlock("Q");
        block.AddArgument("$id", 42);
        block.AddVariable("$id", "Int");

        block.ToString().Should().StartWith("Q($id:Int)");
    }

    [Fact]
    public void RootVariables_PreserveExplicitTypeAndCaseDistinctNames()
    {
        var block = new QueryBlock("Q");
        block.AddArgument("id", new Variable("$id", "String"));
        block.AddVariable("$id", "Int");
        block.AddVariable("$ID", "Boolean");

        block.ToString().Should().StartWith("Q($ID:Boolean, $id:String)");
    }
}
