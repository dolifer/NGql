using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using NGql.Core;
using NGql.Core.Builders;
using NGql.Core.Tests.Extensions;
using Xunit;

namespace NGql.Core.Tests.Builders;

/// <summary>
/// Tests for GraphQL field directives (`FieldBuilder.IncludeIf`, `SkipIf`, `Directive`). Directives
/// render after a field's name and arguments and before its selection set, space-separated, in
/// the order they were added: `field(args) @include(if:$x) @skip(if:$y) { … }`.
/// </summary>
public class FieldDirectiveTests
{
    [Fact]
    public void IncludeIf_OnFieldWithSelectionSet_RendersBeforeBrace()
    {
        // Arrange & Act
        var query = QueryBuilder.CreateDefaultBuilder("Q")
            .AddField("user", u => u.IncludeIf("$show").AddField("name"))
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
    public void IncludeIf_OnLeafField_RendersWithNoBlock()
    {
        // Arrange & Act
        var query = QueryBuilder.CreateDefaultBuilder("Q")
            .AddField("user", u => u.AddField("name", n => n.IncludeIf("$show")))
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
    public void SkipIf_OnLeafField_RendersSkipDirective()
    {
        // Arrange & Act
        var query = QueryBuilder.CreateDefaultBuilder("Q")
            .AddField("user", u => u.AddField("name", n => n.SkipIf("$hide")))
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
            .AddField("user", u => u.IncludeIf("$a").SkipIf("$b").AddField("name"))
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
                u => u.IncludeIf("$w").AddField("name"))
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
    public void IncludeIf_VariableWithAndWithoutDollarSign_ProduceIdenticalOutput()
    {
        // Arrange & Act
        var withDollar = QueryBuilder.CreateDefaultBuilder("Q")
            .AddField("user", u => u.AddField("name", n => n.IncludeIf("$x")))
            .ToString();

        var withoutDollar = QueryBuilder.CreateDefaultBuilder("Q")
            .AddField("user", u => u.AddField("name", n => n.IncludeIf("x")))
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
            .AddField("user", u => u.IncludeIf("$show").SkipIf("$hide"))
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
    public void IncludeIf_MergedSameDirectiveOnSamePathField_RendersOnce(MergingStrategy strategy)
    {
        // Arrange — two fragments, each with @include(if:$x) on user.name.
        var a = QueryBuilder.CreateDefaultBuilder("A", strategy)
            .AddField("user", u => u.AddField("name", n => n.IncludeIf("$x")));
        var b = QueryBuilder.CreateDefaultBuilder("B", strategy)
            .AddField("user", u => u.AddField("name", n => n.IncludeIf("$x")));

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
    public void IncludeIf_MergedDistinctDirectivesOnSamePathField_BothSurvive()
    {
        // Arrange — different directives on the same field: include vs skip, distinct vars.
        var a = QueryBuilder.CreateDefaultBuilder("A", MergingStrategy.MergeByFieldPath)
            .AddField("user", u => u.AddField("name", n => n.IncludeIf("$x")));
        var b = QueryBuilder.CreateDefaultBuilder("B", MergingStrategy.MergeByFieldPath)
            .AddField("user", u => u.AddField("name", n => n.SkipIf("$y")));

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
    public void IncludeIf_SameDirectiveTwiceOnOneBuilder_RendersOnce()
    {
        // Arrange & Act — direct-add path: two identical IncludeIf calls on the same field.
        var query = QueryBuilder.CreateDefaultBuilder("Q")
            .AddField("user", u => u.AddField("name", n => n.IncludeIf("$x").IncludeIf("$x")))
            .ToString();

        // Assert — AddDirective dedups structurally-identical directives for consistency.
        System.Text.RegularExpressions.Regex.Matches(query, "@include").Should().HaveCount(1);
    }

    // FIX #2: an extra leading `$` must collapse to a single `$`, and a `$`-only value must throw.
    [Fact]
    public void IncludeIf_VariableWithDoubleDollar_CollapsesToSingleDollar()
    {
        // Arrange & Act
        var query = QueryBuilder.CreateDefaultBuilder("Q")
            .AddField("user", u => u.AddField("name", n => n.IncludeIf("$$show")))
            .ToString();

        // Assert — renders `if:$show`, not the invalid `if:$$show`.
        query.Should().Contain("name @include(if:$show)");
        query.Should().NotContain("$$");
    }

    [Fact]
    public void IncludeIf_VariableOnlyDollarSigns_Throws()
    {
        // Arrange
        var builder = QueryBuilder.CreateDefaultBuilder("Q");

        // Act
        var act = () => builder.AddField("user", u => u.AddField("name", n => n.IncludeIf("$")));

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

    // Issue #23's exact "Proposed API" example: two Variable-typed conditions, one on a leaf field
    // (SkipIf), one on an object field (IncludeIf), both promoted into the operation signature.
    [Fact]
    public Task IncludeIfSkipIf_Variable_Issue23Example_MatchesProposedOutput()
    {
        // Arrange
        var expand = new Variable("$expand", "Boolean!");
        var hideEmail = new Variable("$hideEmail", "Boolean!");

        // Act
        var qb = QueryBuilder.CreateDefaultBuilder("GetUser")
            .AddField("user", u => u
                .AddField("id")
                .AddField("name")
                .AddField("email", e => e.SkipIf(hideEmail))
                .AddField("profile", p => p
                    .IncludeIf(expand)
                    .AddField("bio")
                    .AddField("avatarUrl")));

        // Assert
        return qb.Verify();
    }

    [Fact]
    public void IncludeIf_Variable_PromotesVariableIntoOperationSignature()
    {
        // Arrange & Act
        var query = QueryBuilder.CreateDefaultBuilder("Q")
            .AddField("user", u => u.AddField("name", n => n.IncludeIf(new Variable("$show", "Boolean!"))))
            .ToString();

        // Assert
        query.Should().StartWith("query Q($show:Boolean!){");
        query.Should().Contain("name @include(if:$show)");
    }

    [Fact]
    public void SkipIf_Variable_PromotesVariableIntoOperationSignature()
    {
        // Arrange & Act
        var query = QueryBuilder.CreateDefaultBuilder("Q")
            .AddField("user", u => u.AddField("name", n => n.SkipIf(new Variable("$hide", "Boolean!"))))
            .ToString();

        // Assert
        query.Should().StartWith("query Q($hide:Boolean!){");
        query.Should().Contain("name @skip(if:$hide)");
    }

    [Fact]
    public void IncludeIf_Variable_OnInlineFragmentSurface_PromotesVariableIntoOperationSignature()
    {
        // Arrange & Act — issue #23's "On inline fragments" example: IncludeIf called directly on
        // the OnType lambda's top-level builder.
        var query = QueryBuilder.CreateDefaultBuilder("Q")
            .AddField("user", u => u
                .OnType("Admin", a => a
                    .IncludeIf(new Variable("$showAdmin", "Boolean!"))
                    .AddField("permissions")))
            .ToString();

        // Assert
        query.Should().StartWith("query Q($showAdmin:Boolean!){");
    }

    [Fact]
    public void IncludeIfSkipIf_SameVariableOnTwoFields_DeclaresOnce()
    {
        // Arrange
        var expand = new Variable("$expand", "Boolean!");

        // Act
        var query = QueryBuilder.CreateDefaultBuilder("Q")
            .AddField("user", u => u
                .AddField("profile", p => p.IncludeIf(expand).AddField("bio"))
                .AddField("settings", s => s.IncludeIf(expand).AddField("theme")))
            .ToString();

        // Assert — the signature declares $expand exactly once despite two IncludeIf(expand) calls.
        query.Should().StartWith("query Q($expand:Boolean!){");
        System.Text.RegularExpressions.Regex.Matches(query, @"\$expand:Boolean!").Should().HaveCount(1);
    }

    [Fact]
    public void IncludeIf_Variable_MergesWithFieldArgumentVariable()
    {
        // Arrange & Act — a directive-promoted variable and a field-argument-promoted variable
        // both land in the same operation signature.
        var query = QueryBuilder.CreateDefaultBuilder("Q")
            .AddField("user",
                new Dictionary<string, object?> { ["id"] = new Variable("$id", "ID!") },
                u => u.IncludeIf(new Variable("$expand", "Boolean!")).AddField("name"))
            .ToString();

        // Assert
        query.Should().StartWith("query Q($expand:Boolean!, $id:ID!){");
    }

    [Fact]
    public void IncludeIf_Variable_MergesWithExplicitlyDeclaredVariable()
    {
        // Arrange — caller pre-declares $show via the SortedSet<Variable> mutation escape hatch,
        // then attaches the directive via the non-promoting string overload.
        var builder = QueryBuilder.CreateDefaultBuilder("Q");
        builder.Definition.Variables.Add(new Variable("$show", "Boolean!"));

        // Act
        var query = builder
            .AddField("user", u => u.AddField("name", n => n.IncludeIf("$show")))
            .ToString();

        // Assert
        query.Should().StartWith("query Q($show:Boolean!){");
    }

    [Fact]
    public void IncludeIf_VariableNonBooleanType_Throws()
    {
        // Arrange
        var builder = QueryBuilder.CreateDefaultBuilder("Q");
        var notBoolean = new Variable("$expand", "String!");

        // Act
        var act = () => builder.AddField("user", u => u.AddField("name", n => n.IncludeIf(notBoolean)));

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void SkipIf_VariableNonBooleanType_Throws()
    {
        // Arrange
        var builder = QueryBuilder.CreateDefaultBuilder("Q");
        var notBoolean = new Variable("$hide", "Int");

        // Act
        var act = () => builder.AddField("user", u => u.AddField("name", n => n.SkipIf(notBoolean)));

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("Boolean")]
    [InlineData("Boolean!")]
    public void IncludeIf_VariableBooleanOrBooleanNonNull_DoesNotThrow(string type)
    {
        // Arrange
        var builder = QueryBuilder.CreateDefaultBuilder("Q");
        var condition = new Variable("$show", type);

        // Act
        var act = () => builder.AddField("user", u => u.AddField("name", n => n.IncludeIf(condition)));

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void Directive_ArgumentsContainVariable_PromotesVariableIntoOperationSignature()
    {
        // Arrange & Act — the generic Directive(name, arguments) overload promotes Variable values
        // in its arguments dictionary exactly like field arguments and IncludeIf/SkipIf.
        var query = QueryBuilder.CreateDefaultBuilder("Q")
            .AddField("user", u => u.AddField("name",
                n => n.Directive("format", new Dictionary<string, object?> { ["locale"] = new Variable("$locale", "String!") })))
            .ToString();

        // Assert
        query.Should().StartWith("query Q($locale:String!){");
        query.Should().Contain("name @format(locale:$locale)");
    }

    // ---------------------------------------------------------------------------------------
    // Issue #23 gap-closing tests
    // ---------------------------------------------------------------------------------------

    [Fact]
    public Task IncludeIf_AndSkipIf_OnSameField_BothRender()
    {
        // Arrange & Act — issue #23's "both @include and @skip on the same field" edge case.
        var a = new Variable("$a", "Boolean!");
        var b = new Variable("$b", "Boolean!");
        var qb = QueryBuilder.CreateDefaultBuilder("Q")
            .AddField("field", f => f.IncludeIf(a).SkipIf(b));

        // Assert
        return qb.Verify();
    }

    [Fact]
    public void IncludeIf_CalledTwiceWithDifferentVariables_LastCallWins()
    {
        // Arrange & Act — issue #23: "Same IncludeIf called twice on the same field: last-call-wins
        // (not 'two @include directives')". Two @include on one field is spec-invalid (GraphQL
        // §5.7.3 — a non-repeatable directive may appear at most once per location).
        var a = new Variable("$a", "Boolean!");
        var b = new Variable("$b", "Boolean!");
        var query = QueryBuilder.CreateDefaultBuilder("Q")
            .AddField("x", f => { f.IncludeIf(a); f.IncludeIf(b); })
            .ToString();

        // Assert
        query.Should().Contain("x @include(if:$b)");
        query.Should().NotContain("if:$a");
        System.Text.RegularExpressions.Regex.Matches(query, "@include").Should().HaveCount(1);
    }

    [Fact]
    public void SkipIf_CalledTwiceWithDifferentVariables_LastCallWins()
    {
        // Arrange & Act — same last-call-wins contract for @skip.
        var a = new Variable("$a", "Boolean!");
        var b = new Variable("$b", "Boolean!");
        var query = QueryBuilder.CreateDefaultBuilder("Q")
            .AddField("x", f => { f.SkipIf(a); f.SkipIf(b); })
            .ToString();

        // Assert
        query.Should().Contain("x @skip(if:$b)");
        query.Should().NotContain("if:$a");
        System.Text.RegularExpressions.Regex.Matches(query, "@skip").Should().HaveCount(1);
    }

    [Fact]
    public void Directive_CalledTwiceWithNameInclude_AlsoCollapsesLastCallWins()
    {
        // Arrange & Act — the generic Directive(name, arguments) overload, called directly with the
        // reserved name "include", must collapse exactly like IncludeIf(Variable) — there is no
        // entry point that can silently emit two @include directives on one field.
        var query = QueryBuilder.CreateDefaultBuilder("Q")
            .AddField("x", f => f
                .Directive("include", new Dictionary<string, object?> { ["if"] = new Variable("$a", "Boolean!") })
                .Directive("include", new Dictionary<string, object?> { ["if"] = new Variable("$b", "Boolean!") }))
            .ToString();

        // Assert
        query.Should().Contain("x @include(if:$b)");
        query.Should().NotContain("if:$a");
        System.Text.RegularExpressions.Regex.Matches(query, "@include").Should().HaveCount(1);
    }

    [Fact]
    public void IncludeIf_ThenSkipIf_ThenIncludeIfAgain_PreservesRelativeOrderOnReplace()
    {
        // Arrange & Act — last-call-wins replaces IN PLACE (same list position), so the directive
        // order relative to other directives on the field does not change on replacement.
        var query = QueryBuilder.CreateDefaultBuilder("Q")
            .AddField("x", f => f.IncludeIf(new Variable("$a", "Boolean!"))
                .SkipIf(new Variable("$b", "Boolean!"))
                .IncludeIf(new Variable("$c", "Boolean!")))
            .ToString();

        // Assert — include stays first (now $c), skip stays second, matching original insertion order.
        query.Should().Contain("x @include(if:$c) @skip(if:$b)");
    }

    [Theory]
    [InlineData(MergingStrategy.MergeByFieldPath)]
    public Task MergeByFieldPath_ConflictingIncludeIfVariables_AutoAliases(MergingStrategy strategy)
    {
        // Arrange — issue #23: two fragments requesting the same field path under DIFFERENT
        // IncludeIf conditions must NOT merge into one node (that would apply one side's condition
        // to content the other side asked for unconditionally) — same auto-alias behavior as an
        // argument conflict.
        var a = new Variable("$a", "Boolean!");
        var b = new Variable("$b", "Boolean!");
        var fragmentA = QueryBuilder.CreateDefaultBuilder("A", strategy)
            .AddField("user", f => { f.IncludeIf(a); f.AddField("id"); });
        var fragmentB = QueryBuilder.CreateDefaultBuilder("B", strategy)
            .AddField("user", f => { f.IncludeIf(b); f.AddField("name"); });

        // Act
        var merged = QueryBuilder.CreateDefaultBuilder("Q", strategy)
            .Include(fragmentA)
            .Include(fragmentB);

        // Assert
        return merged.Verify();
    }

    [Fact]
    public void MergeByFieldPath_ConflictingIncludeIfVariables_KeepsFieldsSeparate()
    {
        // Arrange — same setup as the Verify test above, asserted via direct string checks so the
        // "two roots, not one merged node" contract is explicit and doesn't rely on snapshot review.
        var a = new Variable("$a", "Boolean!");
        var b = new Variable("$b", "Boolean!");
        var fragmentA = QueryBuilder.CreateDefaultBuilder("A", MergingStrategy.MergeByFieldPath)
            .AddField("user", f => { f.IncludeIf(a); f.AddField("id"); });
        var fragmentB = QueryBuilder.CreateDefaultBuilder("B", MergingStrategy.MergeByFieldPath)
            .AddField("user", f => { f.IncludeIf(b); f.AddField("name"); });

        // Act
        var merged = QueryBuilder.CreateDefaultBuilder("Q", MergingStrategy.MergeByFieldPath)
            .Include(fragmentA)
            .Include(fragmentB)
            .ToString();

        // Assert — two separate "user" roots (auto-aliased), each keeping only its own field and
        // its own condition. Neither root ends up with BOTH id and name.
        merged.Should().Contain("user @include(if:$a){");
        merged.Should().Contain("user_1:user @include(if:$b){");
        System.Text.RegularExpressions.Regex.Matches(merged, "@include").Should().HaveCount(2);
    }

    [Fact]
    public void MergeByFieldPath_SameIncludeIfVariable_StillMergesNormally()
    {
        // Arrange — two fragments requesting the SAME field under the SAME condition must still
        // merge into one node (directive identity must not cause a false split when conditions
        // genuinely match) — this is the conservatism-preserving counterpart to the conflict test.
        var a = new Variable("$a", "Boolean!");
        var fragmentA = QueryBuilder.CreateDefaultBuilder("A", MergingStrategy.MergeByFieldPath)
            .AddField("user", f => { f.IncludeIf(a); f.AddField("id"); });
        var fragmentB = QueryBuilder.CreateDefaultBuilder("B", MergingStrategy.MergeByFieldPath)
            .AddField("user", f => { f.IncludeIf(a); f.AddField("name"); });

        // Act
        var merged = QueryBuilder.CreateDefaultBuilder("Q", MergingStrategy.MergeByFieldPath)
            .Include(fragmentA)
            .Include(fragmentB)
            .ToString();

        // Assert — one merged "user" root carrying both id and name under the single shared condition.
        merged.Should().Contain("user @include(if:$a){");
        merged.Should().NotContain("user_1");
        merged.Should().Contain("id");
        merged.Should().Contain("name");
        System.Text.RegularExpressions.Regex.Matches(merged, "@include").Should().HaveCount(1);
    }

    [Fact]
    public void MergeByFieldPath_IncludeIfVsNoDirective_AutoAliases()
    {
        // Arrange — one fragment conditional, the other unconditional, on the same field path: also
        // a directive-identity conflict (conditional state differs), must auto-alias.
        var a = new Variable("$a", "Boolean!");
        var fragmentA = QueryBuilder.CreateDefaultBuilder("A", MergingStrategy.MergeByFieldPath)
            .AddField("user", f => { f.IncludeIf(a); f.AddField("id"); });
        var fragmentB = QueryBuilder.CreateDefaultBuilder("B", MergingStrategy.MergeByFieldPath)
            .AddField("user", f => f.AddField("name"));

        // Act
        var merged = QueryBuilder.CreateDefaultBuilder("Q", MergingStrategy.MergeByFieldPath)
            .Include(fragmentA)
            .Include(fragmentB)
            .ToString();

        // Assert
        merged.Should().Contain("user @include(if:$a){");
        merged.Should().Contain("user_1:user{");
        System.Text.RegularExpressions.Regex.Matches(merged, "@include").Should().HaveCount(1);
    }

    [Fact]
    public void PreservationBuilder_PreservesIntermediateFieldDirective_AndPromotesVariable()
    {
        // Arrange — issue #23: "directives travel with the preserved field. No new API needed."
        // The directive lives on an INTERMEDIATE node (`user`), not the preserved leaf (`id`).
        var expand = new Variable("$expand", "Boolean!");
        var src = QueryBuilder.CreateDefaultBuilder("S")
            .AddField("user", f => { f.IncludeIf(expand); f.AddField("id"); f.AddField("secret"); });

        // Act
        var preserved = PreservationBuilder.Create(src).Preserve("user.id").Build().ToString();

        // Assert — the @include directive AND its variable declaration survive; "secret" does not.
        preserved.Should().StartWith("query S($expand:Boolean!){");
        preserved.Should().Contain("user @include(if:$expand){");
        preserved.Should().Contain("id");
        preserved.Should().NotContain("secret");
    }

    [Fact]
    public void PreservationBuilder_PreservesLeafFieldDirective_AndPromotesVariable()
    {
        // Arrange — the directive lives on the preserved LEAF itself rather than an ancestor.
        var hide = new Variable("$hide", "Boolean!");
        var src = QueryBuilder.CreateDefaultBuilder("S")
            .AddField("user", f => f.AddField("email", e => e.SkipIf(hide)).AddField("name"));

        // Act
        var preserved = PreservationBuilder.Create(src).Preserve("user.email").Build().ToString();

        // Assert
        preserved.Should().StartWith("query S($hide:Boolean!){");
        preserved.Should().Contain("email @skip(if:$hide)");
        preserved.Should().NotContain("name");
    }

    [Fact]
    public void IncludeIf_VariableWithNonBooleanType_ThrowsAtCallTime()
    {
        // Arrange — issue #23's explicit edge case: "Variable not a Boolean: throw at IncludeIf/
        // SkipIf call time." (Already covered elsewhere in this file for the base case; restated
        // here as part of the issue's enumerated contract set.)
        var builder = QueryBuilder.CreateDefaultBuilder("Q");
        var notBoolean = new Variable("$x", "Int!");

        // Act
        var act = () => builder.AddField("field", f => f.IncludeIf(notBoolean));

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public Task OnType_IncludeIf_RendersDirectiveOnInlineFragment()
    {
        // Arrange & Act — issue #23 Gap 4: "... on Admin @include(if:$a){ … }".
        var showAdmin = new Variable("$showAdmin", "Boolean!");
        var qb = QueryBuilder.CreateDefaultBuilder("Q")
            .AddField("node", n => n.OnType("Admin", ad => ad.IncludeIf(showAdmin).AddField("permissions")));

        // Assert
        return qb.Verify();
    }

    [Fact]
    public void OnType_IncludeIf_RendersDirectiveBetweenTypeConditionAndBrace()
    {
        // Arrange & Act
        var showAdmin = new Variable("$showAdmin", "Boolean!");
        var query = QueryBuilder.CreateDefaultBuilder("Q")
            .AddField("node", n => n.OnType("Admin", ad => ad.IncludeIf(showAdmin).AddField("permissions")))
            .ToString();

        // Assert
        query.Should().Contain("... on Admin @include(if:$showAdmin){");
        query.Should().StartWith("query Q($showAdmin:Boolean!){");
    }

    [Fact]
    public void OnType_SkipIf_RendersDirectiveOnInlineFragment()
    {
        // Arrange & Act
        var hideAdmin = new Variable("$hideAdmin", "Boolean!");
        var query = QueryBuilder.CreateDefaultBuilder("Q")
            .AddField("node", n => n.OnType("Admin", ad => ad.SkipIf(hideAdmin).AddField("permissions")))
            .ToString();

        // Assert
        query.Should().Contain("... on Admin @skip(if:$hideAdmin){");
    }

    [Fact]
    public void OnType_DirectiveOnFragment_ExposedOnInlineFragmentDefinition()
    {
        // Arrange & Act
        var showAdmin = new Variable("$showAdmin", "Boolean!");
        var query = QueryBuilder.CreateDefaultBuilder("Q")
            .AddField("node", n => n.OnType("Admin", ad => ad.IncludeIf(showAdmin).AddField("permissions")));

        var fragment = query.Definition.Fields["node"].InlineFragments["Admin"];

        // Assert
        fragment.HasDirectives.Should().BeTrue();
        fragment.Directives.Should().ContainSingle().Which.Name.Should().Be("include");
    }

    [Fact]
    public void OnType_WithoutDirective_InlineFragmentHasNoDirectives()
    {
        // Arrange & Act
        var query = QueryBuilder.CreateDefaultBuilder("Q")
            .AddField("node", n => n.OnType("Admin", ad => ad.AddField("permissions")));

        var fragment = query.Definition.Fields["node"].InlineFragments["Admin"];

        // Assert
        fragment.HasDirectives.Should().BeFalse();
        fragment.Directives.Should().BeEmpty();
    }

    [Fact]
    public void OnType_TwoTypesWithDifferentDirectives_BothIndependentlyRender()
    {
        // Arrange & Act — two OnType fragments on the same parent field, each with its own
        // independent directive, must not leak into each other.
        var showAdmin = new Variable("$showAdmin", "Boolean!");
        var showMod = new Variable("$showMod", "Boolean!");
        var query = QueryBuilder.CreateDefaultBuilder("Q")
            .AddField("node", n => n
                .OnType("Admin", ad => ad.IncludeIf(showAdmin).AddField("permissions"))
                .OnType("Moderator", mo => mo.IncludeIf(showMod).AddField("banCount")))
            .ToString();

        // Assert
        query.Should().Contain("... on Admin @include(if:$showAdmin){");
        query.Should().Contain("... on Moderator @include(if:$showMod){");
    }

    [Fact]
    public void Include_MergingTwoBuildersWithSameInlineFragmentType_MergesDirectivesAndFields()
    {
        // Arrange — two independently-built queries each declare an OnType("Admin", …) fragment
        // under the SAME parent field path. MergeByDefault merges same-name root fields, which
        // recurses into merging the two "Admin" inline fragments (same type name) into one —
        // exercising the inline-fragment merge path for BOTH field bodies and directives together.
        var showAdmin = new Variable("$showAdmin", "Boolean!");
        var a = QueryBuilder.CreateDefaultBuilder("A")
            .AddField("node", n => n.OnType("Admin", ad => ad.IncludeIf(showAdmin).AddField("permissions")));
        var b = QueryBuilder.CreateDefaultBuilder("B")
            .AddField("node", n => n.OnType("Admin", ad => ad.IncludeIf(showAdmin).AddField("role")));

        // Act
        var merged = QueryBuilder.CreateDefaultBuilder("Q")
            .Include(a)
            .Include(b)
            .ToString();

        // Assert — one merged "Admin" fragment carrying both fields and the (identical, deduped)
        // directive exactly once.
        merged.Should().Contain("... on Admin @include(if:$showAdmin){");
        merged.Should().Contain("permissions");
        merged.Should().Contain("role");
        System.Text.RegularExpressions.Regex.Matches(merged, "@include").Should().HaveCount(1);
        System.Text.RegularExpressions.Regex.Matches(merged, "\\.\\.\\. on Admin").Should().HaveCount(1);
    }

    [Fact]
    public void OnType_CustomDirectiveCalledTwiceIdentically_DedupsStructurally()
    {
        // Arrange & Act — a repeatable (non-include/skip) directive called twice with identical
        // arguments on an inline fragment must dedup, exactly like on a plain field.
        var query = QueryBuilder.CreateDefaultBuilder("Q")
            .AddField("node", n => n.OnType("Admin", ad => ad
                .Directive("cacheable")
                .Directive("cacheable")
                .AddField("permissions")))
            .ToString();

        // Assert
        System.Text.RegularExpressions.Regex.Matches(query, "@cacheable").Should().HaveCount(1);
    }
}
