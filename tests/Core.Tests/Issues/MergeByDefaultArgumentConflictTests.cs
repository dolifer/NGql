using System.Collections.Generic;
using FluentAssertions;
using NGql.Core.Builders;
using Xunit;

namespace NGql.Core.Tests.Issues;

/// <summary>
/// With the default merging strategy, including a field whose arguments differ from an existing
/// same-named field merged the two and let the incoming arguments overwrite, so the first fragment
/// silently received the second fragment's data. Differing arguments now keep both fields, the
/// second auto-aliased like <see cref="MergingStrategy.MergeByFieldPath"/> does, and
/// <see cref="QueryBuilder.GetPathTo"/> points each fragment at its own field.
/// </summary>
public class MergeByDefaultArgumentConflictTests
{
    private static QueryBuilder Users(string name, Dictionary<string, object?>? arguments, string child)
        => arguments is null
            ? QueryBuilder.CreateDefaultBuilder(name).AddField("users", new[] { child })
            : QueryBuilder.CreateDefaultBuilder(name).AddField("users", arguments, new[] { child });

    [Fact]
    public void Include_DifferentArguments_AliasesSecondField()
    {
        var target = QueryBuilder.CreateDefaultBuilder("Q")
            .Include(Users("F1", new() { ["first"] = 1 }, "id"))
            .Include(Users("F2", new() { ["first"] = 2 }, "name"));

        target.ToString().Should().Be(
            "query Q{\n    users(first:1){\n        id\n    }\n    users_1:users(first:2){\n        name\n    }\n}");
        target.GetPathTo("F1").Should().Equal("users");
        target.GetPathTo("F2").Should().Equal("users_1");
    }

    [Fact]
    public void Include_ArgumentsOnOneSideOnly_MergesIntoOneField()
    {
        // A fragment that sets no arguments has no opinion on them: the common pattern of one
        // builder configuring a field's arguments and others adding selections keeps working.
        var target = QueryBuilder.CreateDefaultBuilder("Q")
            .Include(Users("F1", null, "id"))
            .Include(Users("F2", new() { ["first"] = 2 }, "name"));

        target.ToString().Should().Be("query Q{\n    users(first:2){\n        id\n        name\n    }\n}");
        target.GetPathTo("F2").Should().Equal("users");
    }

    [Fact]
    public void Include_DisjointArgumentKeys_MergesAndCombinesArguments()
    {
        var target = QueryBuilder.CreateDefaultBuilder("Q")
            .Include(Users("F1", new() { ["first"] = 1 }, "id"))
            .Include(Users("F2", new() { ["after"] = "x" }, "name"));

        target.ToString().Should().Be("query Q{\n    users(after:\"x\", first:1){\n        id\n        name\n    }\n}");
    }

    [Fact]
    public void Include_SameFragmentTwice_MergesIntoItsAliasedCopy()
    {
        var second = Users("F2", new() { ["first"] = 2 }, "name");
        var target = QueryBuilder.CreateDefaultBuilder("Q")
            .Include(Users("F1", new() { ["first"] = 1 }, "id"))
            .Include(second)
            .Include(second);

        target.Definition.Fields.Should().HaveCount(2);
        target.GetPathTo("F2").Should().Equal("users_1");
    }

    [Fact]
    public void Include_SameArguments_MergesIntoOneField()
    {
        var target = QueryBuilder.CreateDefaultBuilder("Q")
            .Include(Users("F1", new() { ["first"] = 1 }, "id"))
            .Include(Users("F2", new() { ["first"] = 1 }, "name"));

        target.ToString().Should().Be("query Q{\n    users(first:1){\n        id\n        name\n    }\n}");
        target.GetPathTo("F2").Should().Equal("users");
    }

    [Fact]
    public void Include_NestedDifferentArguments_AliasesNestedField()
    {
        var first = QueryBuilder.CreateDefaultBuilder("F1")
            .AddField("user", u => u.AddField("posts", new Dictionary<string, object?> { ["first"] = 1 }, new[] { "id" }));
        var second = QueryBuilder.CreateDefaultBuilder("F2")
            .AddField("user", u => u.AddField("posts", new Dictionary<string, object?> { ["first"] = 2 }, new[] { "title" }));

        var target = QueryBuilder.CreateDefaultBuilder("Q").Include(first).Include(second);

        target.ToString().Should().Be(
            "query Q{\n    user{\n        posts(first:1){\n            id\n        }\n        posts_1:posts(first:2){\n            title\n        }\n    }\n}");
    }

    [Fact]
    public void Include_ThreeConflictingArgumentSets_NumbersEachCopy()
    {
        var target = QueryBuilder.CreateDefaultBuilder("Q")
            .Include(Users("F1", new() { ["first"] = 1 }, "id"))
            .Include(Users("F2", new() { ["first"] = 2 }, "id"))
            .Include(Users("F3", new() { ["first"] = 3 }, "id"));

        target.GetPathTo("F2").Should().Equal("users_1");
        target.GetPathTo("F3").Should().Equal("users_2");
    }

    [Fact]
    public void Include_AliasedFieldConflicting_KeepsItsAliasWhenFree()
    {
        var first = QueryBuilder.CreateDefaultBuilder("F1").AddField("users", new Dictionary<string, object?> { ["first"] = 1 }, new[] { "id" });
        var second = QueryBuilder.CreateDefaultBuilder("F2").AddField("top:users", new Dictionary<string, object?> { ["first"] = 2 }, new[] { "id" });

        var target = QueryBuilder.CreateDefaultBuilder("Q").Include(first).Include(second);

        target.ToString().Should().Be(
            "query Q{\n    top:users(first:2){\n        id\n    }\n    users(first:1){\n        id\n    }\n}");
    }

    [Fact]
    public void Include_AliasedFieldConflictingWithSameAlias_GetsNumberedAlias()
    {
        var first = QueryBuilder.CreateDefaultBuilder("F1").AddField("top:users", new Dictionary<string, object?> { ["first"] = 1 }, new[] { "id" });
        var second = QueryBuilder.CreateDefaultBuilder("F2").AddField("top:users", new Dictionary<string, object?> { ["first"] = 2 }, new[] { "id" });

        var target = QueryBuilder.CreateDefaultBuilder("Q").Include(first).Include(second);

        target.ToString().Should().Be(
            "query Q{\n    top:users(first:1){\n        id\n    }\n    top_1:users(first:2){\n        id\n    }\n}");
    }
}
