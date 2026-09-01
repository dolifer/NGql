using System.Threading.Tasks;
using FluentAssertions;
using NGql.Core.Builders;
using Xunit;

namespace NGql.Core.Tests.Extensions.PreserveAtPathTests;

/// <summary>
/// <see cref="PreservationBuilder.PreserveAtPath(string, string)"/> resolves nodePath relative to
/// every root — no root-name guessing. <see cref="PreservationBuilder.PreserveAtPathInRoot(string, string, string)"/>
/// is the unambiguous alternative: it scopes to exactly one named root and is a clean no-op
/// (never falls back to another root) when that root doesn't exist or nodePath doesn't resolve
/// under it.
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
    public Task PreserveAtPath_RelativeNodePath_AppliesToAllResolvingRoots()
    {
        // Arrange - a relative nodePath (not scoped to any specific root) applies to every root
        // that actually resolves it: both aRoot.node and bRoot.node share the "node" segment.
        var query = BuildTwoRootQuery();

        // Act
        var result = PreservationBuilder.Create(query)
            .PreserveAtPath("id", "node")
            .Build();

        // Assert - snapshot pins both roots narrowed to node{id}, both secrets dropped
        return result.Verify();
    }

    [Fact]
    public Task PreserveAtPathInRoot_NodePathDoesNotResolveUnderNamedRoot_IsCleanNoOp()
    {
        // Arrange - "aRoot" exists but has no "node" child directly under it named "node" at
        // this relative path in a way that would collide across roots; here we force the
        // documented failure mode: root "aRoot" has no "node" child (rename it so PreserveAtPathInRoot
        // cannot resolve nodePath="node" under it), while sibling root "bRoot" does have "node".
        var rootA = QueryBuilder
            .CreateDefaultBuilder("QueryA")
            .AddField("aRoot.other.id")
            .AddField("aRoot.other.secretA");

        var rootB = QueryBuilder
            .CreateDefaultBuilder("QueryB")
            .AddField("bRoot.node.id")
            .AddField("bRoot.node.secretB");

        var query = QueryBuilder
            .CreateDefaultBuilder("Merged", MergingStrategy.NeverMerge)
            .Include(rootA)
            .Include(rootB);

        // Act - scope to "aRoot" only; nodePath "node" does not resolve under it
        var result = PreservationBuilder.Create(query)
            .PreserveAtPathInRoot("id", "node", root: "aRoot")
            .Build();

        // Assert - clean no-op: bRoot must NOT be narrowed (its secret survives), and aRoot must
        // NOT be deleted (its fields survive too). Nothing is retargeted anywhere.
        return result.Verify();
    }

    [Fact]
    public Task PreserveAtPathInRoot_NamedRootResolves_NarrowsOnlyThatRootDespiteUnrelatedNameCollision()
    {
        // Arrange - "dataA" has edges.node.{id,secretA}; an unrelated root literally named
        // "edges" also exists (with an unrelated "tot" field, no "node" child). Scoping to
        // "dataA" explicitly must narrow only dataA and must never touch the colliding "edges" root.
        var dataA = QueryBuilder
            .CreateDefaultBuilder("QueryA")
            .AddField("dataA.edges.node.id")
            .AddField("dataA.edges.node.secretA");

        var unrelatedEdges = QueryBuilder
            .CreateDefaultBuilder("QueryC")
            .AddField("edges.tot");

        var query = QueryBuilder
            .CreateDefaultBuilder("Merged", MergingStrategy.NeverMerge)
            .Include(dataA)
            .Include(unrelatedEdges);

        // Act - scope explicitly to "dataA"
        var result = PreservationBuilder.Create(query)
            .PreserveAtPathInRoot("id", "edges.node", root: "dataA")
            .Build();

        // Assert - dataA narrowed to edges.node{id}, secretA dropped. The unrelated "edges" root
        // is absent from the output (Build() only emits explicitly preserved paths) but was never
        // scoped-to, probed, or matched against "edges.node" — it simply received no preservation
        // calls at all, which is the point: the name collision with "dataA.edges" never mattered.
        return result.Verify();
    }

    [Fact]
    public Task PreserveAtPathInRoot_RootNameDoesNotExist_IsCleanNoOp()
    {
        // Arrange
        var query = BuildTwoRootQuery();

        // Act - "doesNotExist" names no root in the query
        var result = PreservationBuilder.Create(query)
            .PreserveAtPathInRoot("id", "node", root: "doesNotExist")
            .Build();

        // Assert - query is unchanged: both roots retain all their fields including secrets
        return result.Verify();
    }

    [Fact]
    public Task PreserveAtPathInRoot_RootedNodePath_DoesNotLeakIntoSiblingRoot()
    {
        // Arrange - two roots, each with a node named "node" and distinct children. Root
        // matching uses alias-or-name, and each root here is aliased ("a"/"b"), so that alias
        // is the effective root identifier.
        var query = BuildTwoRootQuery();

        // Act - scope preservation to root "a" only
        var result = PreservationBuilder.Create(query)
            .PreserveAtPathInRoot("id", "node", root: "a")
            .Build();

        // Assert - snapshot pins root a narrowed to node{id} with secretA dropped. Root b is
        // absent entirely: PreservationBuilder.Build() only emits paths explicitly added to the
        // preserve set, and this call never touches root b at all — it is not scoped-to, not
        // narrowed, and not deleted by any special-case logic.
        return result.Verify();
    }
}
