using FluentAssertions;
using NGql.Core.Builders;
using Xunit;

namespace NGql.Core.Tests.Extensions.PreserveAtPathTests;

/// <summary>
/// Regression: a dotted nodePath whose first segment names a specific root (e.g. "a.node")
/// must scope preservation to that root only. Previously the resolver matched the node by its
/// last segment under every sibling root, so a request against one root's node leaked into a
/// same-named node under a sibling root, silently narrowing fields the caller never referenced.
/// </summary>
public class PreserveAtPathScopingTests
{
    private static QueryBuilder BuildTwoRootQuery()
    {
        var rootA = QueryBuilder
            .CreateDefaultBuilder("QueryA")
            .AddField("a:aRoot.node.id")
            .AddField("aRoot.node.secretA");

        var rootB = QueryBuilder
            .CreateDefaultBuilder("QueryB")
            .AddField("b:bRoot.node.id")
            .AddField("bRoot.node.secretB");

        return QueryBuilder
            .CreateDefaultBuilder("Merged", MergingStrategy.NeverMerge)
            .Include(rootA)
            .Include(rootB);
    }

    [Fact]
    public void PreserveAtPath_RootedNodePath_DoesNotLeakIntoSiblingRoot()
    {
        // Arrange - two roots, each with a node named "node" and distinct children
        var query = BuildTwoRootQuery();

        // Act - scope preservation to root "a" only
        var result = PreservationBuilder.Create(query)
            .PreserveAtPath("id", "a.node")
            .Build()
            .ToString();

        // Assert - the sibling root's node must NOT be narrowed by the a-scoped request. A leak
        // would render "b:bRoot{node{id}}" (secretB silently dropped); with the fix, root b is
        // never touched by the preserve set at all, so its identifiers do not appear.
        result.Should().NotContain("bRoot");
        result.Should().NotContain("b:");
    }

    [Fact]
    public void PreserveAtPath_RootedNodePath_PreservesTargetRootNode()
    {
        // Arrange
        var query = BuildTwoRootQuery();

        // Act - scope preservation to root "a"
        var result = PreservationBuilder.Create(query)
            .PreserveAtPath("id", "a.node")
            .Build()
            .ToString();

        // Assert - root a's node keeps id (the targeted field) and drops secretA
        result.Should().Contain("id");
        result.Should().NotContain("secretA");
    }

    [Fact]
    public void PreserveAtPath_RelativeNodePath_StillAppliesToAllRoots()
    {
        // Arrange - a relative nodePath (first segment not a root name) keeps the pre-existing
        // cross-root behavior: every root that actually has the resolved node.field is preserved.
        var query = BuildTwoRootQuery();

        // Act - "node" is relative: applies to both aRoot.node and bRoot.node
        var result = PreservationBuilder.Create(query)
            .PreserveAtPath("id", "node")
            .Build()
            .ToString();

        // Assert - both roots preserved id, both secrets dropped
        result.Should().Contain("id");
        result.Should().NotContain("secretA");
        result.Should().NotContain("secretB");
    }
}
