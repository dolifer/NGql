using System.Text;
using FluentAssertions;
using NGql.Core.Builders;
using NGql.Core.Pooling;
using Xunit;

namespace NGql.Core.Tests;

/// <summary>
/// Targeted tests covering branches that survived the broader coverage push.
/// Each test exercises a specific previously-uncovered branch identified via opencover output.
/// </summary>
public class CoverageGapTests
{
    [Fact]
    public void ThreadLocalPool_ReturnNull_IsNoOp()
    {
        var pool = new ThreadLocalPool<StringBuilder>(
            factory: () => new StringBuilder(),
            reset: sb => sb.Clear(),
            poolName: "test-pool");

        // Should not throw and should not corrupt the pool
        var act = () => pool.Return(null);
        act.Should().NotThrow();

        // Pool still works after the null Return
        var sb = pool.Get();
        sb.Should().NotBeNull();
    }

    [Fact]
    public void QueryTextBuilder_RendersEmptyQuery_ProducesEmptyBody()
    {
        var query = QueryBuilder.CreateDefaultBuilder("Empty").ToString();

        query.Should().Contain("query Empty");
        query.Should().Contain("{");
        query.Should().Contain("}");
    }

    [Fact]
    public void Preserve_PathDeeperThanTree_StopsAtLeaf()
    {
        var source = QueryBuilder.CreateDefaultBuilder("Source")
            .AddField("user.profile");

        // Request a path that goes deeper than the tree (profile is a leaf)
        var preserved = PreservationBuilder.Create(source)
            .Preserve("user.profile.email")
            .Build();

        preserved.ToString().Should().NotContain("email");
    }

    [Fact]
    public void Preserve_FromExpressionAgainstLeaf_NoChildrenIsNoOp()
    {
        var source = QueryBuilder.CreateDefaultBuilder("Source")
            .AddField("user");

        var preserved = PreservationBuilder.Create(source)
            .PreserveFromExpression<TestUser>(x => x.Name != null)
            .Build();

        // user is a leaf, so no children to preserve into; result should still render
        preserved.ToString().Should().Contain("query Source");
    }

    [Fact]
    public void PreserveAtPath_NodeNotInTree_NoOp()
    {
        var source = QueryBuilder.CreateDefaultBuilder("Source")
            .AddField("user.name");

        // nodePath "user.missing" doesn't exist in the tree
        var preserved = PreservationBuilder.Create(source)
            .PreserveAtPath("name", "user.missing")
            .Build();

        preserved.ToString().Should().Contain("query Source");
    }

    [Fact]
    public void PreserveAtPath_NodeIsLeaf_NoOp()
    {
        var source = QueryBuilder.CreateDefaultBuilder("Source")
            .AddField("user.name");

        // user.name is a leaf — has no fields under it to preserve
        var preserved = PreservationBuilder.Create(source)
            .PreserveAtPath("anything", "user.name")
            .Build();

        preserved.ToString().Should().Contain("query Source");
    }

    [Fact]
    public void FindFieldRecursively_FieldHasNoAlias_StillMatchesByName()
    {
        var source = QueryBuilder.CreateDefaultBuilder("Source")
            .AddField("outer.inner.target");

        var fragment = QueryBuilder.CreateDefaultBuilder("Frag")
            .AddField("outer.inner.target");

        var merged = QueryBuilder.CreateDefaultBuilder("Merged")
            .Include(source)
            .Include(fragment);

        var result = merged.ToString();
        result.Should().Contain("target");
    }

    [Fact]
    public void PreserveFromExpression_NodeNotInRootField_EarlyReturns()
    {
        // sourceQuery has only "user.name". PreserveFromExpression with a parameter
        // whose property "missing.deep" doesn't resolve under any root field should
        // exercise the early-return arms in PreserveFromRoot.
        var source = QueryBuilder.CreateDefaultBuilder("Source")
            .AddField("user.name");

        var preserved = PreservationBuilder.Create(source)
            .PreserveFromExpression<TestUserWithMissing>(x => x.Missing.Deep != null)
            .Build();

        // Even with no resolution, the build succeeds without throwing
        preserved.ToString().Should().Contain("query Source");
    }

    [Fact]
    public void PreserveFromExpression_NodePathResolvesToLeaf_EarlyReturns()
    {
        // Tree is "user" (leaf, no children). PreserveFromExpression where the
        // expression touches "user.something" — "user" exists but is a leaf,
        // so PreserveFromRoot's HasFields check fails.
        var source = QueryBuilder.CreateDefaultBuilder("Source")
            .AddField("user");

        var preserved = PreservationBuilder.Create(source)
            .PreserveFromExpression<TestUserWithMissing>(x => x.Name != null)
            .Build();

        preserved.ToString().Should().Contain("query Source");
    }

    private sealed class TestUser
    {
        public string Name { get; set; } = "";
    }

    private sealed class TestUserWithMissing
    {
        public string Name { get; set; } = "";
        public TestNested Missing { get; set; } = new();
    }

    private sealed class TestNested
    {
        public string Deep { get; set; } = "";
    }
}
