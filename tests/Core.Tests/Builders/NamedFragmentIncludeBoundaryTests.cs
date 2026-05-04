using System;
using FluentAssertions;
using NGql.Core.Builders;
using Xunit;

namespace NGql.Core.Tests.Builders;

/// <summary>
/// Documents NGql's current behavior when <see cref="QueryBuilder.Include"/> meets fragments
/// (named, inline, or spreads). Today the merger does not touch fragment collections — both
/// definitions and spreads are silently dropped from the incoming query. These tests pin that
/// behavior so it cannot regress accidentally.
///
/// A future change to support fragments through <c>Include</c> should rewrite these tests
/// (rather than delete them) — they document the boundary, not a permanent contract.
/// </summary>
public class NamedFragmentIncludeBoundaryTests
{
    [Fact]
    public void Include_Drops_NamedFragments_From_Incoming_Query()
    {
        var fragmentSource = QueryBuilder.CreateDefaultBuilder("FragmentSource")
            .AddFragment("UserSummary", "User", f => f.AddField("id").AddField("name"))
            .AddField("user", u => u.SpreadFragment("UserSummary"));

        var target = QueryBuilder.CreateDefaultBuilder("Target")
            .AddField("admins", a => a.AddField("id"));

        var act = () => target.Include(fragmentSource);

        // Currently: silently drops named fragments AND spreads from the incoming definition.
        // Pinning this so it doesn't quietly start producing broken GraphQL when someone
        // refactors the merger.
        act.Should().Throw<NotSupportedException>()
           .WithMessage("*Include*fragments*");
    }

    [Fact]
    public void Include_Drops_InlineFragments_From_Incoming_Query()
    {
        var inlineSource = QueryBuilder.CreateDefaultBuilder("InlineSource")
            .AddField("nodes", n => n
                .OnType("Repository", r => r.AddField("name")));

        var target = QueryBuilder.CreateDefaultBuilder("Target")
            .AddField("admins", a => a.AddField("id"));

        var act = () => target.Include(inlineSource);

        act.Should().Throw<NotSupportedException>()
           .WithMessage("*Include*fragments*");
    }

    [Fact]
    public void Include_Drops_SpreadFragments_From_Incoming_Query()
    {
        // Spreads alone (no fragment definition source-side) — still rejected because the
        // spread reference is a fragment-related construct.
        var spreadSource = QueryBuilder.CreateDefaultBuilder("SpreadSource")
            .AddField("user", u => u.SpreadFragment("UserSummary"));

        var target = QueryBuilder.CreateDefaultBuilder("Target")
            .AddField("admins", a => a.AddField("id"));

        var act = () => target.Include(spreadSource);

        act.Should().Throw<NotSupportedException>()
           .WithMessage("*Include*fragments*");
    }

    [Fact]
    public void Include_Detects_Fragments_Nested_Inside_Child_Fields()
    {
        // The guard recurses through child fields — a fragment buried under a parent without
        // its own fragments must still be caught. Exercises the recursive branch in
        // FieldHasAnyFragment that the top-level fragment cases don't reach.
        var deepSource = QueryBuilder.CreateDefaultBuilder("DeepSource")
            .AddField("organization", o => o
                .AddField("departments", d => d
                    .AddField("teams", t => t
                        .OnType("EngineeringTeam", e => e.AddField("repoCount")))));

        var target = QueryBuilder.CreateDefaultBuilder("Target")
            .AddField("admins", a => a.AddField("id"));

        var act = () => target.Include(deepSource);

        act.Should().Throw<NotSupportedException>()
           .WithMessage("*Include*fragments*");
    }

    [Fact]
    public void Include_Without_Fragments_Continues_To_Work()
    {
        // Sanity check: Include with no fragments anywhere is unaffected.
        var source = QueryBuilder.CreateDefaultBuilder("Source")
            .AddField("user", u => u.AddField("id").AddField("name"));

        var target = QueryBuilder.CreateDefaultBuilder("Target", MergingStrategy.MergeByFieldPath)
            .AddField("user", u => u.AddField("email"));

        var act = () => target.Include(source);

        act.Should().NotThrow();
        target.Definition.Fields["user"].Fields.Keys
            .Should().BeEquivalentTo("id", "name", "email");
    }
}
