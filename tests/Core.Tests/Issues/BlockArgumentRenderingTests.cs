using System;
using System.Collections.Generic;
using System.Text;
using FluentAssertions;
using NGql.Core.Abstractions;
using Xunit;

namespace NGql.Core.Tests.Issues;

public class BlockArgumentRenderingTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void PooledArguments_MatchDictionaryPrecedence(bool root)
    {
        var random = new Random(531);
        for (var run = 0; run < 100; run++)
        {
            var block = new QueryBlock("Q");
            for (var i = 0; i < 20; i++)
            {
                var key = i < 5 ? $"$v{i}" : $"argument{i}";
                block.AddArgument(key, random.Next(2) == 0 ? random.Next(100) : new Variable($"$v{random.Next(5)}", "String"));
            }
            block.AddVariable("$V0", "Boolean");
            block.AddVariable("$v0", "Int");
            var expected = new StringBuilder("Q(");
            foreach (var (key, value) in ReferenceArguments(block, root))
            {
                if (expected.Length > 2) expected.Append(", ");
                if (value is Variable variable) variable.Print(expected, key, root);
                else expected.Append(key).Append(':').Append(value);
            }
            expected.Append(')');
            var rendered = block.ToString();
            if (!root)
            {
                var parent = new QueryBlock("Parent");
                parent.AddField(block);
                rendered = parent.ToString();
            }
            rendered.Should().Contain(expected.ToString());
        }
    }

    private static SortedDictionary<string, object> ReferenceArguments(QueryBlock block, bool root)
    {
        var arguments = new SortedDictionary<string, object>(StringComparer.Ordinal);
        foreach (var (key, value) in block.Arguments)
            arguments[root && value is Variable variable ? variable.Name : key] = value;
        if (root)
        {
            foreach (var variable in block.Variables)
            {
                if (!arguments.TryGetValue(variable.Name, out var value) || value is not Variable)
                    arguments[variable.Name] = variable;
            }
        }
        return arguments;
    }

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
