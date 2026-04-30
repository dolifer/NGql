using System.Collections.Generic;
using FluentAssertions;
using Xunit;
using static NGql.Core.Builders.QueryBuilder;

namespace NGql.Core.Tests.Builders;

public class QueryBuilderGetPathToTests
{
    [Theory]
    [InlineData("user.profile.edges", "edges", "user,profile")]
    [InlineData("user.profile.edges.id", "id", "user,profile,edges")]
    [InlineData("user.profile.edges.Id:id", "id", "user,profile,edges")]
    [InlineData("TUsers:user.PAlias:profile.edges.Id:id", "id", "TUsers,PAlias,edges")]
    public void GetPathTo_WithAliasedField_ShouldReturnAlias(string field, string query, string expected)
    {
        // Arrange
        var expectedItems = expected.Split(',');
        var builder = CreateDefaultBuilder("TestQuery");
        builder.AddField(field, ["String FullName:name", "Int age"]);

        // Act
        var path = builder.GetPathTo("TestQuery", query);

        // Assert
        path.Should().BeEquivalentTo(expectedItems);
    }

    [Theory]
    [InlineData("MyAlias:actual_field.base", "actual_field.base.edges.node", "MyAlias", "edges", "MyAlias,base")]
    [InlineData("QueryAlias:real_query_name.section", "real_query_name.section.items.data", "QueryAlias", "items", "QueryAlias,section")]
    [InlineData("BatchAlias:batch_query_field.base", "batch_query_field.base.edges.node", "BatchAlias", "edges", "BatchAlias,base")]
    public void GetPathTo_WithIncludedQueryWithRootFieldAlias_ShouldReturnAlias(string rootField, string nestedField, string queryName, string targetNode, string expected)
    {
        // Arrange
        var expectedItems = expected.Split(',');

        // Create a query with a root field that has an alias (this is the key difference from the other test)
        var sourceQuery = CreateDefaultBuilder(queryName)
            .AddField(rootField, new Dictionary<string, object?> { { "first", 10 } })
            .AddField(nestedField, ["String name", "Int id"]);

        var mainBuilder = CreateDefaultBuilder("TestQuery");
        mainBuilder.Include(sourceQuery); // ← This is the Include operation that was failing!

        // Act
        var path = mainBuilder.GetPathTo(queryName, targetNode); // ← Query across included queries

        // Assert
        path.Should().BeEquivalentTo(expectedItems);
    }

    [Fact]
    public void GetPathTo_WithEmptyQueryName_ReturnsEmptyArray()
    {
        var builder = CreateDefaultBuilder("Q").AddField("user.id");

        // Empty query name → GetMappedPath returns "" → early return [].
        var path = builder.GetPathTo("", "user");

        path.Should().BeEmpty();
    }

    [Fact]
    public void GetPathTo_NodePathNotFound_RootMatchesField_ReturnsRootEffectiveName()
    {
        // Root field "data" matches the queryName so FindRootField succeeds and BuildPathToNode is
        // invoked. The target node "doesNotExist" doesn't appear anywhere — DFS returns false and
        // BuildPathToNode falls back to a single-element [rootField._effectiveName] result.
        var builder = CreateDefaultBuilder("data").AddField("data.profile.name");

        var path = builder.GetPathTo("data", "doesNotExist");

        var expectedSingle = new List<string> { "data" };
        path.Should().BeEquivalentTo(expectedSingle);
    }

    [Fact]
    public void GetPathTo_TargetInSecondSubtree_BacktracksFromFirstSubtree()
    {
        // Single root with two child subtrees. Target only lives under the second.
        // DFS descends into the first ("name"), backtracks, then finds the target ("score") in the second.
        // Exercises the backtrack branch in FindPathToNodeOptimized.
        var builder = CreateDefaultBuilder("data")
            .AddField("data.profile.name")
            .AddField("data.metrics.score");

        var path = builder.GetPathTo("data", "score");

        var expectedTwo = new List<string> { "data", "metrics" };
        path.Should().BeEquivalentTo(expectedTwo);
    }
}
