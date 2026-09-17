using FluentAssertions;
using NGql.Core.Abstractions;
using NGql.Core.Extensions;
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

        block.GetArguments(true)["$id"].Should().Be(new Variable("$id", "Int"));
    }

    [Fact]
    public void RootVariables_PreserveExplicitTypeAndCaseDistinctNames()
    {
        var block = new QueryBlock("Q");
        block.AddArgument("id", new Variable("$id", "String"));
        block.AddVariable("$id", "Int");
        block.AddVariable("$ID", "Boolean");

        var arguments = block.GetArguments(true);
        arguments.Should().HaveCount(2);
        arguments["$id"].Should().Be(new Variable("$id", "String"));
        arguments["$ID"].Should().Be(new Variable("$ID", "Boolean"));
    }
}
