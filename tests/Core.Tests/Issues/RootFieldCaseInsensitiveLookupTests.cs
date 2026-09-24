using System;
using System.Collections.Generic;
using FluentAssertions;
using NGql.Core;
using NGql.Core.Builders;
using Xunit;

namespace NGql.Core.Tests.Issues;

/// <summary>
/// Regression: root fields added with a lambda or with arguments were looked up by a
/// case-sensitive linear scan of the root dictionary, while the dictionary itself is
/// case-insensitive. A second root differing only in case missed the scan and then overwrote
/// the first under the shared key, silently dropping its children. The scan also made adding
/// n such roots O(n²).
/// </summary>
public class RootFieldCaseInsensitiveLookupTests
{
    [Fact]
    public void AddField_LambdaRootDifferingOnlyInCase_MergesIntoExistingRoot()
    {
        var query = QueryBuilder.CreateDefaultBuilder("Q")
            .AddField("User", b => b.AddField("id"))
            .AddField("user", b => b.AddField("name"));

        query.ToString().Should().Be("query Q{\n    User{\n        id\n        name\n    }\n}");
    }

    [Fact]
    public void AddField_ArgumentRootDifferingOnlyInCase_KeepsExistingChildren()
    {
        var query = QueryBuilder.CreateDefaultBuilder("Q")
            .AddField("User.id")
            .AddField("user", new Dictionary<string, object?> { ["x"] = 1 });

        query.ToString().Should().Be("query Q{\n    User(x:1){\n        id\n    }\n}");
    }

    [Fact]
    public void AddField_ManyLambdaRoots_KeepsEveryRoot()
    {
        var query = QueryBuilder.CreateDefaultBuilder("Q");
        for (var i = 0; i < 2000; i++) query.AddField("root" + i, b => b.AddField("id"));

        query.Definition.Fields.Should().HaveCount(2000);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(40)]
    public void AddField_ArgumentKeysCollidingByCase_ThrowsWithoutDeclaringVariables(int argumentCount)
    {
        var arguments = new Dictionary<string, object?> { ["leaked"] = new Variable("$leaked", "Int") };
        for (var i = 1; i < argumentCount - 1; i++) arguments["arg" + i] = i;
        arguments["LEAKED"] = 1;
        var query = QueryBuilder.CreateDefaultBuilder("Q");

        var act = () => query.AddField("user", arguments);

        act.Should().Throw<ArgumentException>()
            .WithMessage("An item with the same key has already been added. Colliding key: 'LEAKED'.*");
        query.Definition._variables.Should().BeNull();
    }

    [Theory]
    [InlineData(2)]
    [InlineData(40)]
    public void AddField_DistinctArgumentKeys_RendersEveryArgument(int argumentCount)
    {
        var arguments = new Dictionary<string, object?>();
        for (var i = 0; i < argumentCount; i++) arguments["arg" + i] = i;

        var rendered = QueryBuilder.CreateDefaultBuilder("Q").AddField("user", arguments).ToString();

        rendered.Should().Contain("arg0:0").And.Contain($"arg{argumentCount - 1}:{argumentCount - 1}");
    }

    [Fact]
    public void AddField_CaseInsensitiveArgumentDictionary_SkipsCollisionCheck()
    {
        var arguments = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase) { ["b"] = 2, ["a"] = 1 };

        var rendered = QueryBuilder.CreateDefaultBuilder("Q").AddField("user", arguments).ToString();

        rendered.Should().Be("query Q{\n    user(a:1, b:2)\n}");
    }
}
