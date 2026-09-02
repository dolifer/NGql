using System.Threading.Tasks;
using NGql.Core.Builders;
using Xunit;

namespace NGql.Core.Tests.Extensions;

public class PreservationPruningTests
{
    [Fact]
    public Task Preserve_ChildAddedAfterParent_PrunesParent()
    {
        // Arrange
        var query = QueryBuilder
            .CreateDefaultBuilder("TestQuery")
            .AddField("TestQuery:data.edges.node.UserId:userId")
            .AddField("data.edges.node.Profile:profile.Name:name")
            .AddField("data.edges.node.Profile:profile.Age:age");

        // Act - parent added first, then a more specific child
        var result = PreservationBuilder.Create(query)
            .Preserve("TestQuery.edges.node.profile")
            .Preserve("TestQuery.edges.node.profile.name")
            .Build();

        // Assert - only profile.name should remain, not all of profile
        return result.Verify();
    }

    [Fact]
    public Task Preserve_ParentAddedAfterChild_DoesNotPruneChild()
    {
        // Arrange
        var query = QueryBuilder
            .CreateDefaultBuilder("TestQuery")
            .AddField("TestQuery:data.edges.node.UserId:userId")
            .AddField("data.edges.node.Profile:profile.Name:name")
            .AddField("data.edges.node.Profile:profile.Age:age");

        // Act - child added first, then a broader parent
        var result = PreservationBuilder.Create(query)
            .Preserve("TestQuery.edges.node.profile.name")
            .Preserve("TestQuery.edges.node.profile")
            .Build();

        // Assert - parent now covers both name and age; child is not left dangling nor duplicated
        return result.Verify();
    }

    [Fact]
    public Task Preserve_CaseInsensitivePrefix_StillPrunesParent()
    {
        // Arrange
        var query = QueryBuilder
            .CreateDefaultBuilder("TestQuery")
            .AddField("TestQuery:data.edges.node.UserId:userId")
            .AddField("data.edges.node.Profile:profile.Name:name")
            .AddField("data.edges.node.Profile:profile.Age:age");

        // Act - parent added with different casing than the later child
        var result = PreservationBuilder.Create(query)
            .Preserve("TESTQUERY.EDGES.NODE.PROFILE")
            .Preserve("TestQuery.edges.node.profile.name")
            .Build();

        // Assert - case-insensitive prefix match still prunes the parent
        return result.Verify();
    }

    [Theory]
    [InlineData("use", "user.name", "ShortPrefix")]
    [InlineData("user", "userProfile", "NoDotBoundary")]
    public Task Preserve_TextualPrefixWithoutDotBoundary_IsNotPruned(string existingPath, string newPath, string scenario)
    {
        // Arrange
        var query = QueryBuilder
            .CreateDefaultBuilder("TestQuery")
            .AddField($"TestQuery:{existingPath}.Id:id")
            .AddField($"TestQuery:{newPath}.Id:id2");

        // Act - existingPath shares a textual prefix with newPath but is not a dot-delimited ancestor
        var result = PreservationBuilder.Create(query)
            .Preserve(existingPath)
            .Preserve(newPath)
            .Build();

        // Assert - both paths must survive; neither is a true ancestor of the other
        return result.Verify($"Preserve_TextualPrefixWithoutDotBoundary_IsNotPruned_{scenario}");
    }

    [Fact]
    public Task Preserve_MultipleParents_AllPrunedByOneChild()
    {
        // Arrange
        var query = QueryBuilder
            .CreateDefaultBuilder("TestQuery")
            .AddField("TestQuery:data.edges.node.UserId:userId")
            .AddField("data.edges.node.Profile:profile.Settings:settings.Theme:theme")
            .AddField("data.edges.node.Profile:profile.Settings:settings.Language:language");

        // Act - two ancestor paths of the same child are both present when the child is added
        var result = PreservationBuilder.Create(query)
            .Preserve("TestQuery.edges.node.profile")
            .Preserve("TestQuery.edges.node.profile.settings")
            .Preserve("TestQuery.edges.node.profile.settings.theme")
            .Build();

        // Assert - both ancestor paths are pruned, only the leaf remains
        return result.Verify();
    }
}
