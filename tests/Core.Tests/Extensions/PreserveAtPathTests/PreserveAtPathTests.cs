using System.Threading.Tasks;
using NGql.Core.Builders;
using Xunit;

namespace NGql.Core.Tests.Extensions.PreserveAtPathTests;

public class PreserveAtPathTests
{
    [Fact]
    public Task PreserveAtPath_SpecificNodePath_PreservesFieldAtNode()
    {
        // Arrange
        var query = QueryBuilder
            .CreateDefaultBuilder("TestQuery")
            .AddField("TestQuery:data.edges.node.UserId:userId")
            .AddField("data.edges.node.Profile:profile.Name:name")
            .AddField("data.edges.node.Profile:profile.Age:age");

        // Act - preserve 'profile.name' field specifically at the 'edges.node' path
        var result = PreservationBuilder.Create(query)
            .PreserveAtPath("profile.name", "edges.node")
            .Build();

        // Assert - should only preserve profile.name
        return result.Verify();
    }

    [Fact]
    public Task PreserveAtPath_MultipleRoots_PreservesInAllRoots()
    {
        // Arrange
        var queryA = QueryBuilder
            .CreateDefaultBuilder("QueryA")
            .AddField("QueryA:data.edges.node.UserId:userId")
            .AddField("data.edges.node.Email:email");

        var queryB = QueryBuilder
            .CreateDefaultBuilder("QueryB")
            .AddField("QueryB:data.edges.node.UserId:userId")
            .AddField("data.edges.node.Name:name");

        var mergedQuery = QueryBuilder
            .CreateDefaultBuilder("MergedQuery", MergingStrategy.NeverMerge)
            .Include(queryA)
            .Include(queryB);

        // Act - preserve email at node path (should work for all roots that have it)
        var result = PreservationBuilder.Create(mergedQuery)
            .PreserveAtPath("email", "edges.node")
            .Build();

        // Assert
        return result.Verify();
    }

    [Fact]
    public Task PreserveAtPath_NonExistentField_IgnoresGracefully()
    {
        // Arrange
        var query = QueryBuilder
            .CreateDefaultBuilder("TestQuery")
            .AddField("TestQuery:data.edges.node.UserId:userId")
            .AddField("data.edges.node.Email:email");

        // Act - try to preserve non-existent field at path
        var result = PreservationBuilder.Create(query)
            .PreserveAtPath("nonExistentField", "edges.node")
            .Build();

        // Assert - ignores gracefully and returns original query unchanged
        return result.Verify();
    }

    [Fact]
    public Task PreserveAtPath_WithAliasedFields_MatchesByAlias()
    {
        // Arrange
        var query = QueryBuilder
            .CreateDefaultBuilder("TestQuery")
            .AddField("TestQuery:data.edges.node.UserId:userId")
            .AddField("data.edges.node.UserEmail:email") // alias different from field name
            .AddField("data.edges.node.Name:userName"); // alias different from field name

        // Act - preserve using alias
        var result = PreservationBuilder.Create(query)
            .PreserveAtPath("email", "edges.node")
            .Build();

        // Assert
        return result.Verify();
    }
}
