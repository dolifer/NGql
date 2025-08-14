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
}
