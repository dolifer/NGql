using FluentAssertions;
using NGql.Core.Builders;
using Xunit;

namespace NGql.Core.Tests.Builders;

/// <summary>
/// Documents NGql's behavior when <see cref="QueryBuilder.Include"/> meets fragments (named,
/// inline, or spreads). Historically the merger rejected fragment-bearing queries with a
/// <c>NotSupportedException</c> — these tests once pinned that limitation. Include now merges
/// fragments correctly (the #20 follow-up), so each case asserts the merged output instead.
/// </summary>
public class NamedFragmentIncludeBoundaryTests
{
    [Fact]
    public void Include_Merges_NamedFragments_From_Incoming_Query()
    {
        var fragmentSource = QueryBuilder.CreateDefaultBuilder("FragmentSource")
            .AddFragment("UserSummary", "User", f => f.AddField("id").AddField("name"))
            .AddField("user", u => u.SpreadFragment("UserSummary"));

        var target = QueryBuilder.CreateDefaultBuilder("Target")
            .AddField("admins", a => a.AddField("id"));

        target.Include(fragmentSource);

        var rendered = target.ToString();
        rendered.Should().Contain("...UserSummary", "the spread survives the merge");
        rendered.Should().Contain("fragment UserSummary on User", "the definition carries over");
    }

    [Fact]
    public void Include_Merges_InlineFragments_From_Incoming_Query()
    {
        var inlineSource = QueryBuilder.CreateDefaultBuilder("InlineSource")
            .AddField("nodes", n => n
                .OnType("Repository", r => r.AddField("name")));

        var target = QueryBuilder.CreateDefaultBuilder("Target")
            .AddField("admins", a => a.AddField("id"));

        target.Include(inlineSource);

        target.ToString().Should().Contain("... on Repository",
            "the incoming field's inline fragment survives the merge");
    }

    [Fact]
    public void Include_Merges_SpreadFragments_From_Incoming_Query()
    {
        // Spreads alone (no fragment definition source-side) — the spread reference carries over
        // even though no matching definition exists (NGql is schemaless).
        var spreadSource = QueryBuilder.CreateDefaultBuilder("SpreadSource")
            .AddField("user", u => u.SpreadFragment("UserSummary"));

        var target = QueryBuilder.CreateDefaultBuilder("Target")
            .AddField("admins", a => a.AddField("id"));

        target.Include(spreadSource);

        target.ToString().Should().Contain("...UserSummary");
    }

    [Fact]
    public void Include_Carries_Fragments_Nested_Inside_Child_Fields()
    {
        // A fragment buried under a parent without its own fragments must still carry over —
        // exercises the recursive merge branch, not just top-level fields.
        var deepSource = QueryBuilder.CreateDefaultBuilder("DeepSource")
            .AddField("organization", o => o
                .AddField("departments", d => d
                    .AddField("teams", t => t
                        .OnType("EngineeringTeam", e => e.AddField("repoCount")))));

        var target = QueryBuilder.CreateDefaultBuilder("Target")
            .AddField("admins", a => a.AddField("id"));

        target.Include(deepSource);

        target.ToString().Should().Contain("... on EngineeringTeam",
            "deeply-nested inline fragments merge through the field tree");
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
