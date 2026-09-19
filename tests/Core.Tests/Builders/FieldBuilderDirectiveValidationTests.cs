using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using NGql.Core.Builders;
using Xunit;

namespace NGql.Core.Tests.Builders;

public class FieldBuilderDirectiveValidationTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Directive_NullOrWhitespaceName_Throws(string? name)
    {
        var act = () => QueryBuilder.CreateDefaultBuilder("Test")
            .AddField("user", b => b.Directive(name!));

        act.Should().Throw<ArgumentException>()
            .WithMessage("Directive name cannot be null or whitespace.*");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void IncludeIf_NullOrWhitespaceVariable_Throws(string? variable)
    {
        var act = () => QueryBuilder.CreateDefaultBuilder("Test")
            .AddField("user", b => b.IncludeIf(variable!));

        act.Should().Throw<ArgumentException>()
            .WithMessage("Directive variable cannot be null or whitespace.*");
    }

    [Fact]
    public void SkipIf_WhitespaceVariable_Throws()
    {
        var act = () => QueryBuilder.CreateDefaultBuilder("Test")
            .AddField("user", b => b.SkipIf("  "));

        act.Should().Throw<ArgumentException>()
            .WithMessage("Directive variable cannot be null or whitespace.*");
    }

    [Theory]
    [InlineData("@")]
    [InlineData("@@")]
    [InlineData("@@@")]
    public void Directive_NameOfOnlyAtSigns_Throws(string name)
    {
        var act = () => QueryBuilder.CreateDefaultBuilder("Test")
            .AddField("user", b => b.Directive(name));

        act.Should().Throw<ArgumentException>()
            .WithMessage("Directive name cannot consist only of '@' characters.*");
    }

    [Theory]
    [InlineData("$")]
    [InlineData("$$")]
    [InlineData("$$$")]
    public void IncludeIf_VariableOfOnlyDollarSigns_Throws(string variable)
    {
        var act = () => QueryBuilder.CreateDefaultBuilder("Test")
            .AddField("user", b => b.IncludeIf(variable));

        act.Should().Throw<ArgumentException>()
            .WithMessage("Directive variable must contain a name after the '$'.*");
    }

    [Fact]
    public void SkipIf_VariableOfOnlyDollarSigns_Throws()
    {
        var act = () => QueryBuilder.CreateDefaultBuilder("Test")
            .AddField("user", b => b.SkipIf("$$"));

        act.Should().Throw<ArgumentException>()
            .WithMessage("Directive variable must contain a name after the '$'.*");
    }
}
