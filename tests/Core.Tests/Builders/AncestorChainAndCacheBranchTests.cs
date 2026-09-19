using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using NGql.Core.Builders;
using Xunit;

namespace NGql.Core.Tests.Builders;

public class AncestorChainAndCacheBranchTests
{
    // A root field whose alias differs from its stored key is not found by the _effectiveName
    // lookup, and its dot-free Path gives TryWalkPath nothing to walk — so the ancestor chain
    // falls back to a reference search across every root, backtracking out of each root that
    // does not contain the target before reaching the one that does.
    [Fact]
    public void AddField_AliasedRootAfterUnrelatedRoot_ResolvesAncestorsByReferenceSearch()
    {
        var builder = QueryBuilder.CreateDefaultBuilder("Test")
            .AddField("other.child");

        builder.AddField("m:metrics", new Dictionary<string, object?> { ["id"] = 1 }, b => b.AddField("amount"));

        var result = builder.ToString();

        result.Should().Contain("other");
        result.Should().Contain("m:metrics");
        result.Should().Contain("amount");
    }

    [Fact]
    public void AddField_AliasedRootAfterSeveralUnrelatedRoots_ResolvesAncestorsByReferenceSearch()
    {
        var builder = QueryBuilder.CreateDefaultBuilder("Test")
            .AddField("alpha.one")
            .AddField("beta.deep.deeper.leaf");

        builder.AddField("m:metrics", new Dictionary<string, object?> { ["id"] = 1 }, b => b.AddField("amount"));

        var result = builder.ToString();

        result.Should().Contain("deeper");
        result.Should().Contain("m:metrics");
        result.Should().Contain("amount");
    }

    [Fact]
    public void AddField_AliasedDottedPathAfterUnrelatedRoot_ResolvesAncestorsByReferenceSearch()
    {
        var args = new Dictionary<string, object?> { ["from"] = "2024-01-01" };

        var query = QueryBuilder.CreateDefaultBuilder("Test")
            .AddField("other.child")
            .AddField("m:metrics.d:deposits", args, b => b.AddField("amount"));

        var result = query.ToString();

        result.Should().Contain("other");
        result.Should().Contain("m:metrics");
        result.Should().Contain("d:deposits");
        result.Should().Contain("amount");
    }

    [Fact]
    public void AddField_AliasedDottedPathAfterSeveralUnrelatedRoots_ResolvesAncestorsByReferenceSearch()
    {
        var args = new Dictionary<string, object?> { ["from"] = "2024-01-01" };

        var query = QueryBuilder.CreateDefaultBuilder("Test")
            .AddField("alpha.one")
            .AddField("beta.two")
            .AddField("m:metrics.d:deposits", args, b => b.AddField("amount"));

        var result = query.ToString();

        result.Should().Contain("alpha");
        result.Should().Contain("beta");
        result.Should().Contain("d:deposits");
        result.Should().Contain("amount");
    }

    [Fact]
    public void AddField_AliasedDottedPathNestedUnderUnrelatedDeepRoot_ResolvesAncestors()
    {
        var args = new Dictionary<string, object?> { ["limit"] = 5 };

        var query = QueryBuilder.CreateDefaultBuilder("Test")
            .AddField("other.deep.deeper.leaf")
            .AddField("m:metrics.d:deposits", args, b => b.AddField("amount"));

        var result = query.ToString();

        result.Should().Contain("deeper");
        result.Should().Contain("d:deposits");
        result.Should().Contain("amount");
    }

    [Fact]
    public void GetPathTo_AfterIncludeAddsField_ResolvesNewlyIncludedField()
    {
        var builder = QueryBuilder.CreateDefaultBuilder("Test").AddField("user.name");
        builder.GetPathTo("user").Should().NotBeEmpty();

        var fragment = QueryBuilder.CreateDefaultBuilder("Fragment").AddField("account.balance");
        builder.Include(fragment);

        builder.GetPathTo("account").Should().NotBeEmpty();
    }

    [Fact]
    public void GetPathTo_AfterIncludeWithoutPriorLookup_ResolvesNewlyIncludedField()
    {
        var builder = QueryBuilder.CreateDefaultBuilder("Test").AddField("user.name");
        var fragment = QueryBuilder.CreateDefaultBuilder("Fragment").AddField("account.balance");

        builder.Include(fragment);

        builder.GetPathTo("account").Should().NotBeEmpty();
    }
}
