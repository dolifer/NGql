using System.Collections.Generic;
using FluentAssertions;
using NGql.Core;
using NGql.Core.Builders;
using Xunit;

namespace NGql.Core.Tests.Builders;

/// <summary>
/// Tests for GraphQL field directives (`FieldBuilder.Include`, `Skip`, `Directive`). Directives
/// render after a field's name and arguments and before its selection set, space-separated, in
/// the order they were added: `field(args) @include(if:$x) @skip(if:$y) { … }`.
/// </summary>
public class FieldDirectiveTests
{
    [Fact]
    public void Include_OnFieldWithSelectionSet_RendersBeforeBrace()
    {
        // Arrange & Act
        var query = QueryBuilder.CreateDefaultBuilder("Q")
            .AddField("user", u => u.Include("$show").AddField("name"))
            .ToString();

        // Assert
        query.Should().Be(
            "query Q{\n" +
            "    user @include(if:$show){\n" +
            "        name\n" +
            "    }\n" +
            "}");
    }

    [Fact]
    public void Include_OnLeafField_RendersWithNoBlock()
    {
        // Arrange & Act
        var query = QueryBuilder.CreateDefaultBuilder("Q")
            .AddField("user", u => u.AddField("name", n => n.Include("$show")))
            .ToString();

        // Assert
        query.Should().Be(
            "query Q{\n" +
            "    user{\n" +
            "        name @include(if:$show)\n" +
            "    }\n" +
            "}");
    }

    [Fact]
    public void Skip_OnLeafField_RendersSkipDirective()
    {
        // Arrange & Act
        var query = QueryBuilder.CreateDefaultBuilder("Q")
            .AddField("user", u => u.AddField("name", n => n.Skip("$hide")))
            .ToString();

        // Assert
        query.Should().Be(
            "query Q{\n" +
            "    user{\n" +
            "        name @skip(if:$hide)\n" +
            "    }\n" +
            "}");
    }

    [Fact]
    public void MultipleDirectives_RenderInAddOrderSpaceSeparated()
    {
        // Arrange & Act
        var query = QueryBuilder.CreateDefaultBuilder("Q")
            .AddField("user", u => u.Include("$a").Skip("$b").AddField("name"))
            .ToString();

        // Assert — insertion order preserved: include before skip.
        query.Should().Be(
            "query Q{\n" +
            "    user @include(if:$a) @skip(if:$b){\n" +
            "        name\n" +
            "    }\n" +
            "}");
    }

    [Fact]
    public void Directive_NoArguments_RendersBareName()
    {
        // Arrange & Act
        var query = QueryBuilder.CreateDefaultBuilder("Q")
            .AddField("user", u => u.AddField("name", n => n.Directive("lowerCase")))
            .ToString();

        // Assert
        query.Should().Be(
            "query Q{\n" +
            "    user{\n" +
            "        name @lowerCase\n" +
            "    }\n" +
            "}");
    }

    [Fact]
    public void Directive_WithArguments_RendersNameAndArgs()
    {
        // Arrange & Act
        var query = QueryBuilder.CreateDefaultBuilder("Q")
            .AddField("user", u => u.AddField("name",
                n => n.Directive("format", new Dictionary<string, object?> { ["as"] = "ISO8601" })))
            .ToString();

        // Assert — arg render matches field-argument spacing: `as:"ISO8601"` (no space after colon).
        query.Should().Be(
            "query Q{\n" +
            "    user{\n" +
            "        name @format(as:\"ISO8601\")\n" +
            "    }\n" +
            "}");
    }

    [Fact]
    public void NoDirectives_RendersIdenticallyToPreFeatureOutput()
    {
        // Arrange & Act
        var query = QueryBuilder.CreateDefaultBuilder("Q")
            .AddField("user", u => u.AddField("name"))
            .ToString();

        // Assert — no stray `@`, output unchanged.
        query.Should().Be(
            "query Q{\n" +
            "    user{\n" +
            "        name\n" +
            "    }\n" +
            "}");
        query.Should().NotContain("@");
    }

    [Fact]
    public void Directive_WithArgumentsAndSelectionSet_RendersInCorrectOrder()
    {
        // Arrange & Act — field arguments first, then the directive, then the selection set.
        var query = QueryBuilder.CreateDefaultBuilder("Q")
            .AddField("user",
                new Dictionary<string, object?> { ["id"] = new Variable("$id", "ID!") },
                u => u.Include("$w").AddField("name"))
            .ToString();

        // Assert — a Variable field-argument is hoisted into the operation's variable list.
        query.Should().Be(
            "query Q($id:ID!){\n" +
            "    user(id:$id) @include(if:$w){\n" +
            "        name\n" +
            "    }\n" +
            "}");
    }

    [Fact]
    public void Include_VariableWithAndWithoutDollarSign_ProduceIdenticalOutput()
    {
        // Arrange & Act
        var withDollar = QueryBuilder.CreateDefaultBuilder("Q")
            .AddField("user", u => u.AddField("name", n => n.Include("$x")))
            .ToString();

        var withoutDollar = QueryBuilder.CreateDefaultBuilder("Q")
            .AddField("user", u => u.AddField("name", n => n.Include("x")))
            .ToString();

        // Assert — `$`-normalization makes both render as `@include(if:$x)`.
        withoutDollar.Should().Be(withDollar);
        withDollar.Should().Contain("name @include(if:$x)");
    }

    [Fact]
    public void Directive_NameWithLeadingAt_IsNormalized()
    {
        // Arrange & Act — leading `@` is stripped from the stored name and re-added on render.
        var query = QueryBuilder.CreateDefaultBuilder("Q")
            .AddField("user", u => u.AddField("name", n => n.Directive("@lowerCase")))
            .ToString();

        // Assert — exactly one `@`, not `@@`.
        query.Should().Contain("name @lowerCase");
        query.Should().NotContain("@@");
    }

    [Fact]
    public void Directives_ExposedOnFieldDefinition_WithHasDirectivesGuard()
    {
        // Arrange & Act
        var field = QueryBuilder.CreateDefaultBuilder("Q")
            .AddField("user", u => u.Include("$show").Skip("$hide"))
            .Definition.Fields["user"];

        // Assert
        field.HasDirectives.Should().BeTrue();
        field.Directives.Should().HaveCount(2);
        field.Directives[0].Name.Should().Be("include");
        field.Directives[1].Name.Should().Be("skip");
    }

    [Fact]
    public void Directives_OnFieldWithout_IsEmptyAndAllocationFreeGuardFalse()
    {
        // Arrange & Act
        var field = QueryBuilder.CreateDefaultBuilder("Q")
            .AddField("user", u => u.AddField("name"))
            .Definition.Fields["user"];

        // Assert
        field.HasDirectives.Should().BeFalse();
        field.Directives.Should().BeEmpty();
    }
}
