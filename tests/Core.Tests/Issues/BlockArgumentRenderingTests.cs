using System.Collections.Generic;
using FluentAssertions;
using NGql.Core.Abstractions;
using Xunit;

namespace NGql.Core.Tests.Issues;

public class BlockArgumentRenderingTests
{
    [Fact]
    public void SingleNestedVariable_RendersReferenceAndRootDeclaration()
    {
        var root = new QueryBlock("Q");
        var nested = new QueryBlock("item");
        nested.AddArgument("id", new Variable("$value", "Int"));
        root.AddField(nested);

        root.ToString().Should().StartWith("Q($value:Int)").And.Contain("item(id:$value)");
    }

    [Fact]
    public void SingleArgument_RendersNullAndReadsReplacement()
    {
        var root = new QueryBlock("Q");
        root.AddArgument("value", null!);
        root.ToString().Should().StartWith("Q(value:null)");
        root.AddArgument("value", 42);
        root.ToString().Should().StartWith("Q(value:42)");
    }

    [Fact]
    public void Rendering_UsesOrdinalOrderAtRootAndInNestedBlocks()
    {
        var root = new QueryBlock("Q");
        var nested = new QueryBlock("item");
        foreach (var block in new[] { root, nested })
        {
            block.AddArgument("a", 1);
            block.AddArgument("Z", 2);
            block.AddArgument("b", 3);
        }
        root.AddField(nested);

        root.ToString().Should().Contain("Q(Z:2, a:1, b:3)")
            .And.Contain("item(Z:2, a:1, b:3)");
    }

    [Fact]
    public void Rendering_PreservesVariablePrecedenceAndReReadsMutations()
    {
        var root = new QueryBlock("Q");
        root.AddArgument("id", new Variable("$id", "String"));
        root.AddArgument("$other", 42);
        root.AddVariable("$id", "Int");
        root.AddVariable("$ID", "Boolean");
        root.AddVariable("$other", "Int");

        root.ToString().Should().StartWith("Q($ID:Boolean, $id:String, $other:Int)");
        root.AddArgument("z", 1);
        root.ToString().Should().StartWith("Q($ID:Boolean, $id:String, $other:Int, z:1)");
    }

    [Fact]
    public void Insertion_LargeBlockPreservesCaseCollisionAndOverwriteRules()
    {
        var root = new QueryBlock("Q");
        for (var i = 0; i < 1000; i++) root.AddArgument($"argument{i:D4}", i);
        root.AddArgument("argument0500", -1);

        var add = () => root.AddArgument("ARGUMENT0500", 2);
        add.Should().Throw<System.ArgumentException>();
        var batch = () => root.AddArgument(new Dictionary<string, object>
        {
            ["newArgument"] = 1,
            ["ARGUMENT0500"] = 2
        });
        batch.Should().Throw<System.ArgumentException>();
        root.Arguments.Should().HaveCount(1000);
        root.Arguments["argument0500"].Should().Be(-1);
    }
}
