using System;
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

    // FIX #1: merging two queries that each place the SAME directive on a same-path field must not
    // duplicate it — `@include(if:$x)` is non-repeatable per the GraphQL spec.
    [Theory]
    [InlineData(MergingStrategy.MergeByDefault)]
    [InlineData(MergingStrategy.MergeByFieldPath)]
    public void Include_MergedSameDirectiveOnSamePathField_RendersOnce(MergingStrategy strategy)
    {
        // Arrange — two fragments, each with @include(if:$x) on user.name.
        var a = QueryBuilder.CreateDefaultBuilder("A", strategy)
            .AddField("user", u => u.AddField("name", n => n.Include("$x")));
        var b = QueryBuilder.CreateDefaultBuilder("B", strategy)
            .AddField("user", u => u.AddField("name", n => n.Include("$x")));

        // Act
        var merged = QueryBuilder.CreateDefaultBuilder("Q", MergingStrategy.MergeByFieldPath)
            .Include(a)
            .Include(b)
            .ToString();

        // Assert — the directive survives exactly once, not `@include(if:$x) @include(if:$x)`.
        merged.Should().Contain("@include(if:$x)");
        System.Text.RegularExpressions.Regex.Matches(merged, "@include").Should().HaveCount(1);
    }

    [Fact]
    public void Include_MergedDistinctDirectivesOnSamePathField_BothSurvive()
    {
        // Arrange — different directives on the same field: include vs skip, distinct vars.
        var a = QueryBuilder.CreateDefaultBuilder("A", MergingStrategy.MergeByFieldPath)
            .AddField("user", u => u.AddField("name", n => n.Include("$x")));
        var b = QueryBuilder.CreateDefaultBuilder("B", MergingStrategy.MergeByFieldPath)
            .AddField("user", u => u.AddField("name", n => n.Skip("$y")));

        // Act
        var merged = QueryBuilder.CreateDefaultBuilder("Q", MergingStrategy.MergeByFieldPath)
            .Include(a)
            .Include(b)
            .ToString();

        // Assert — dedup is structural: differing directives are NOT collapsed.
        merged.Should().Contain("@include(if:$x)");
        merged.Should().Contain("@skip(if:$y)");
    }

    [Fact]
    public void Include_SameDirectiveTwiceOnOneBuilder_RendersOnce()
    {
        // Arrange & Act — direct-add path: two identical Include calls on the same field.
        var query = QueryBuilder.CreateDefaultBuilder("Q")
            .AddField("user", u => u.AddField("name", n => n.Include("$x").Include("$x")))
            .ToString();

        // Assert — AddDirective dedups structurally-identical directives for consistency.
        System.Text.RegularExpressions.Regex.Matches(query, "@include").Should().HaveCount(1);
    }

    // FIX #2: an extra leading `$` must collapse to a single `$`, and a `$`-only value must throw.
    [Fact]
    public void Include_VariableWithDoubleDollar_CollapsesToSingleDollar()
    {
        // Arrange & Act
        var query = QueryBuilder.CreateDefaultBuilder("Q")
            .AddField("user", u => u.AddField("name", n => n.Include("$$show")))
            .ToString();

        // Assert — renders `if:$show`, not the invalid `if:$$show`.
        query.Should().Contain("name @include(if:$show)");
        query.Should().NotContain("$$");
    }

    [Fact]
    public void Include_VariableOnlyDollarSigns_Throws()
    {
        // Arrange
        var builder = QueryBuilder.CreateDefaultBuilder("Q");

        // Act
        var act = () => builder.AddField("user", u => u.AddField("name", n => n.Include("$")));

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    // FIX #3: multiple leading `@` on a directive name must collapse; an `@`-only name must throw.
    [Fact]
    public void Directive_NameWithDoubleAt_CollapsesToSingleAt()
    {
        // Arrange & Act
        var query = QueryBuilder.CreateDefaultBuilder("Q")
            .AddField("user", u => u.AddField("name", n => n.Directive("@@foo")))
            .ToString();

        // Assert — renders `@foo`, not the invalid `@@foo`.
        query.Should().Contain("name @foo");
        query.Should().NotContain("@@");
    }

    [Theory]
    [InlineData("@")]
    [InlineData("@@")]
    public void Directive_NameOnlyAtSigns_Throws(string name)
    {
        // Arrange
        var builder = QueryBuilder.CreateDefaultBuilder("Q");

        // Act
        var act = () => builder.AddField("user", u => u.AddField("name", n => n.Directive(name)));

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    // FIX #4: directive arguments go through the same OrdinalIgnoreCase pipeline as field args, so
    // case-colliding keys throw; distinct keys still render.
    [Fact]
    public void Directive_CaseCollidingArgumentKeys_Throws()
    {
        // Arrange
        var builder = QueryBuilder.CreateDefaultBuilder("Q");
        var colliding = new Dictionary<string, object?> { ["If"] = 1, ["if"] = 2 };

        // Act
        var act = () => builder.AddField("user", u => u.AddField("name", n => n.Directive("d", colliding)));

        // Assert — matches field-argument collision behavior.
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Directive_DistinctArgumentKeys_RenderBoth()
    {
        // Arrange & Act
        var query = QueryBuilder.CreateDefaultBuilder("Q")
            .AddField("user", u => u.AddField("name",
                n => n.Directive("d", new Dictionary<string, object?> { ["a"] = 1, ["b"] = 2 })))
            .ToString();

        // Assert — distinct keys survive and render (sorted, matching field-arg normalization).
        query.Should().Contain("name @d(a:1, b:2)");
    }
}
