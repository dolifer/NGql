using System.Collections.Generic;
using System.Threading.Tasks;
using NGql.Core.Builders;
using NGql.Core.Extensions;
using NGql.Core.Features;
using Xunit;

namespace NGql.Core.Tests.Extensions;

public class PreserveExtensionsTests
{
    private static readonly Variable SizeVariable = new("$size", "Int");

    [Fact]
    public Task Preserve_SinglePath_Parent_ReturnsFullQuery()
    {
        // Arrange
        var query = QueryBuilder
            .CreateDefaultBuilder("ComplexQuery")
            .AddField("parent.child1.grandchild1.name")
            .AddField("parent.child1.grandchild1.email")
            .AddField("parent.child1.profilePicture", new Dictionary<string, object?> { {"size", SizeVariable} })
            .AddField("parent.child2.grandchild2");

        // Act
        var result = PreservationBuilder
            .Create(query)
            .Preserve("parent")
            .Build();

        // Assert
        return result.Verify();
    }

    [Fact]
    public Task Preserve_SinglePath_Child1_ReturnsChild1Subtree()
    {
        // Arrange
        var query = QueryBuilder
            .CreateDefaultBuilder("ComplexQuery")
            .AddField("parent.child1.grandchild1.name")
            .AddField("parent.child1.grandchild1.email")
            .AddField("parent.child1.profilePicture", new Dictionary<string, object?> { {"size", SizeVariable} })
            .AddField("parent.child2.grandchild2");

        // Act
        var result = PreservationBuilder
            .Create(query)
            .Preserve("parent.child1")
            .Build();

        // Assert
        return result.Verify();
    }

    [Fact]
    public Task Preserve_SinglePath_Grandchild1_ReturnsGrandchild1Subtree()
    {
        // Arrange
        var query = QueryBuilder
            .CreateDefaultBuilder("ComplexQuery")
            .AddField("parent.child1.grandchild1.name")
            .AddField("parent.child1.grandchild1.email")
            .AddField("parent.child1.profilePicture", new Dictionary<string, object?> { {"size", SizeVariable} })
            .AddField("parent.child2.grandchild2");

        // Act
        var result = PreservationBuilder
            .Create(query)
            .Preserve("parent.child1.grandchild1")
            .Build();

        // Assert
        return result.Verify();
    }

    [Fact]
    public Task Preserve_SinglePath_LeafField_ReturnsOnlyThatField()
    {
        // Arrange
        var query = QueryBuilder
            .CreateDefaultBuilder("ComplexQuery")
            .AddField("parent.child1.grandchild1.name")
            .AddField("parent.child1.grandchild1.email")
            .AddField("parent.child1.profilePicture", new Dictionary<string, object?> { {"size", SizeVariable} })
            .AddField("parent.child2.grandchild2");

        // Act
        var result = PreservationBuilder
            .Create(query)
            .Preserve("parent.child1.grandchild1.name")
            .Build();

        // Assert
        return result.Verify();
    }

    [Fact]
    public Task Preserve_MultiplePaths_ReturnsCombinedSubtrees()
    {
        // Arrange
        var query = QueryBuilder
            .CreateDefaultBuilder("ComplexQuery")
            .AddField("parent.child1.grandchild1.name")
            .AddField("parent.child1.grandchild1.email")
            .AddField("parent.child1.profilePicture", new Dictionary<string, object?> { {"size", SizeVariable} })
            .AddField("parent.child2.grandchild2");

        // Act
        var result = PreservationBuilder
            .Create(query)
            .Preserve("parent.child1.grandchild1", "parent.child2")
            .Build();

        // Assert
        return result.Verify();
    }

    [Fact]
    public Task Preserve_NonexistentPath_ReturnsOriginalQuery()
    {
        // Arrange
        var query = QueryBuilder
            .CreateDefaultBuilder("ComplexQuery")
            .AddField("parent.child1.name")
            .AddField("parent.child2.email");

        // Act
        var result = PreservationBuilder
            .Create(query)
            .Preserve("nonexistent.path")
            .Build();

        // Assert
        return result.Verify();
    }

    [Fact]
    public Task Preserve_EmptyPaths_ReturnsOriginalQuery()
    {
        // Arrange
        var query = QueryBuilder
            .CreateDefaultBuilder("TestQuery")
            .AddField("user.name");

        // Act
        var result = PreservationBuilder
            .Create(query)
            .Build();

        // Assert
        return result.Verify();
    }

    [Fact]
    public Task Preserve_NullPaths_ReturnsOriginalQuery()
    {
        // Arrange
        var query = QueryBuilder
            .CreateDefaultBuilder("TestQuery")
            .AddField("user.name");

        // Act
        var result = PreservationBuilder
            .Create(query)
            .Preserve(null)
            .Build();

        // Assert
        return result.Verify();
    }

    [Fact]
    public Task Preserve_WithArguments_PreservesArguments()
    {
        // Arrange
        var query = QueryBuilder
            .CreateDefaultBuilder("QueryWithArgs")
            .AddField("user", new Dictionary<string, object?> { {"id", "123"} })
            .AddField("user.name")
            .AddField("user.posts", new Dictionary<string, object?> { {"limit", 10} })
            .AddField("user.posts.title");

        // Act
        var result = PreservationBuilder
            .Create(query)
            .Preserve("user.posts")
            .Build();

        // Assert
        return result.Verify();
    }

    [Fact]
    public Task Preserve_WithAliases_SupportsAliasInPath()
    {
        // Arrange
        var query = QueryBuilder
            .CreateDefaultBuilder("AliasQuery")
            .AddField("userAlias:user.profileAlias:profile.name")
            .AddField("userAlias:user.profileAlias:profile.bio")
            .AddField("userAlias:user.posts.title");

        // Act
        var result = PreservationBuilder
            .Create(query)
            .Preserve("userAlias.profileAlias")
            .Build();

        // Assert
        return result.Verify();
    }

    [Fact]
    public Task Preserve_WithAliases_SupportsFieldNameInPath()
    {
        // Arrange
        var query = QueryBuilder
            .CreateDefaultBuilder("AliasQuery")
            .AddField("userAlias:user.profileAlias:profile.name")
            .AddField("userAlias:user.profileAlias:profile.bio")
            .AddField("userAlias:user.posts.title");

        // Act - using field names instead of aliases
        var result = PreservationBuilder
            .Create(query)
            .Preserve("user.profile")
            .Build();

        // Assert
        return result.Verify();
    }

    [Fact]
    public Task Preserve_WithAliases_MixedAliasAndFieldNames()
    {
        // Arrange
        var query = QueryBuilder
            .CreateDefaultBuilder("MixedQuery")
            .AddField("userAlias:user.profile.name")
            .AddField("userAlias:user.profileAlias:profile.bio")
            .AddField("userAlias:user.posts.title");

        // Act - mix alias and field name
        var result = PreservationBuilder
            .Create(query)
            .Preserve("userAlias.profileAlias", "user.posts")
            .Build();

        // Assert
        return result.Verify();
    }

    [Fact]
    public Task Preserve_ChainingMultipleCalls_AccumulatesPaths()
    {
        // Arrange
        var query = QueryBuilder
            .CreateDefaultBuilder("ChainedQuery")
            .AddField("user.profile.name")
            .AddField("user.profile.email")
            .AddField("user.posts.title")
            .AddField("user.posts.author")
            .AddField("user.settings.theme")
            .AddField("admin.permissions");

        // Act - Chain multiple Preserve() calls to accumulate paths
        var result = PreservationBuilder
            .Create(query)
            .Preserve("user.profile")
            .Preserve("user.posts")
            .Build();

        // Assert
        return result.Verify();
    }
}
