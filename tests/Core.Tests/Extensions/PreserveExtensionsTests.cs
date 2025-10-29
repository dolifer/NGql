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
        var result = query.Preserve("parent");

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
        var result = query.Preserve("parent.child1");

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
        var result = query.Preserve("parent.child1.grandchild1");

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
        var result = query.Preserve("parent.child1.grandchild1.name");

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
        var result = query.Preserve("parent.child1.grandchild1", "parent.child2");

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
        var result = query.Preserve("nonexistent.path");

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
        var result = query.Preserve();

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
        var result = query.Preserve(null);

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
        var result = query.Preserve("user.posts");

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
        var result = query.Preserve("userAlias.profileAlias");

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
        var result = query.Preserve("user.profile");

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
        var result = query.Preserve("userAlias.profileAlias", "user.posts");

        // Assert
        return result.Verify();
    }
}
