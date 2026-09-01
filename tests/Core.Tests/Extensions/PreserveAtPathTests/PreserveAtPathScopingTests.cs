using System.Threading.Tasks;
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
    public Task PreserveAtPath_RootedNodePath_PreservesTargetRootNode()
    {
        // Arrange
        var query = BuildTwoRootQuery();

        // Act - scope preservation to root "a"
        var result = PreservationBuilder.Create(query)
            .PreserveAtPath("id", "a.node")
            .Build();

        // Assert - snapshot pins the exact rendered structure: root a narrowed to node{id} with
        // secretA dropped, root b entirely absent. A substring check on "id" alone would also
        // pass for a no-op (unfiltered) result, since "id" is a substring of "secretA"/"secretB"
        // renders too; the snapshot rules that out.
        return result.Verify();
    }

    [Fact]
    public Task PreserveAtPath_RelativeNodePath_StillAppliesToAllRoots()
    {
        // Arrange - a relative nodePath (first segment not a root name) keeps the pre-existing
        // cross-root behavior: every root that actually has the resolved node.field is preserved.
        var query = BuildTwoRootQuery();

        // Act - "node" is relative: applies to both aRoot.node and bRoot.node
        var result = PreservationBuilder.Create(query)
            .PreserveAtPath("id", "node")
            .Build();

        // Assert - snapshot pins both roots narrowed to node{id}, both secrets dropped
        return result.Verify();
    }

    [Fact]
    public Task PreserveAtPath_RelativeNodePathCollidingWithUnrelatedRootName_AppliesToAllMatchingRoots()
    {
        // Arrange - three roots: dataA and dataB each have edges.node.{id,secret}, plus an
        // unrelated root literally named "edges" with no "node" child. The nodePath "edges.node"
        // is RELATIVE (its first segment is not meant to name a root), but it collides in name
        // with the unrelated "edges" root. Resolving nodePath by first-segment alone would wrongly
        // scope to the "edges" root, find no "node" child there, and preserve nothing anywhere -
        // silently returning the query completely unfiltered with all secrets intact.
        var dataA = QueryBuilder
            .CreateDefaultBuilder("QueryA")
            .AddField("dataA.edges.node.id")
            .AddField("dataA.edges.node.secretA");

        var dataB = QueryBuilder
            .CreateDefaultBuilder("QueryB")
            .AddField("dataB.edges.node.id")
            .AddField("dataB.edges.node.secretB");

        var unrelatedEdges = QueryBuilder
            .CreateDefaultBuilder("QueryC")
            .AddField("edges.tot");

        var query = QueryBuilder
            .CreateDefaultBuilder("Merged", MergingStrategy.NeverMerge)
            .Include(dataA)
            .Include(dataB)
            .Include(unrelatedEdges);

        // Act - "edges.node" collides with the unrelated "edges" root's name
        var result = PreservationBuilder.Create(query)
            .PreserveAtPath("id", "edges.node")
            .Build();

        // Assert - snapshot pins dataA/dataB both narrowed to node{id} with secrets dropped, and
        // the unrelated "edges" root untouched. The query must not come back unfiltered.
        return result.Verify();
    }
}
