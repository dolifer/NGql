using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using NGql.Core.Builders;
using Xunit;

namespace NGql.Core.Tests.Extensions;

public class PreserveExtensionsTests
{
    private static readonly Variable SizeVariable = new("$size", "Int");

    private class TestUser { public string? id { get; set; } public string? email { get; set; } }
    private class TestProfile { public string? firstName { get; set; } public string? lastName { get; set; } }
    private class TestSegment { public string? id { get; set; } public string? name { get; set; } }
    private class TestDeposit 
    { 
        public decimal? amount { get; set; } 
        public DateTime? date { get; set; }
        public DateTime? Date { get; set; } // Matches production code-generated property
    }
    private class TestBusiness { public string? segmentId { get; set; } public string? category { get; set; } }

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

        // Assert - verify query name and variables are preserved
        result.Definition.Name.Should().Be("ComplexQuery");
        result.Definition.Variables.Should().ContainSingle()
            .Which.Should().Be(SizeVariable);
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

        // Assert - verify query name and variables from preserved path
        result.Definition.Name.Should().Be("ComplexQuery");
        result.Definition.Variables.Should().ContainSingle()
            .Which.Should().Be(SizeVariable);
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

        // Assert - verify query name preserved, but no variables (profilePicture not included)
        result.Definition.Name.Should().Be("ComplexQuery");
        result.Definition.Variables.Should().BeEmpty();
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

        // Add query-level metadata and merging strategy
        query.Definition.Metadata["testKey"] = "testValue";
        query.Definition.Metadata["cached"] = true;
        query.Definition.MergingStrategy = MergingStrategy.NeverMerge;

        // Act
        var result = PreservationBuilder
            .Create(query)
            .Preserve("user.posts")
            .Build();

        // Assert - verify metadata, merging strategy, and query name are preserved
        result.Definition.Name.Should().Be("QueryWithArgs");
        result.Definition.MergingStrategy.Should().Be(MergingStrategy.NeverMerge);
        result.Definition.Metadata.Should().HaveCount(2)
            .And.ContainKey("testKey").WhoseValue.Should().Be("testValue");
        result.Definition.Metadata["cached"].Should().Be(true);
        result.Definition.Variables.Should().BeEmpty(); // No variables in this query
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

    [Fact]
    public Task Preserve_ParentAndChild_ParentTakesPrecedence()
    {
        // Arrange
        var query = QueryBuilder
            .CreateDefaultBuilder("OverlappingQuery")
            .AddField("user.profile.name")
            .AddField("user.profile.email")
            .AddField("user.profile.bio")
            .AddField("user.posts.title")
            .AddField("user.posts.author")
            .AddField("admin.permissions");

        // Act - Preserve both parent and child (child is redundant)
        var result = PreservationBuilder
            .Create(query)
            .Preserve("user", "user.profile")
            .Build();

        // Assert
        return result.Verify();
    }

    [Fact]
    public Task Preserve_ChildThenParent_ParentTakesPrecedence()
    {
        // Arrange
        var query = QueryBuilder
            .CreateDefaultBuilder("OverlappingQuery")
            .AddField("user.profile.name")
            .AddField("user.profile.email")
            .AddField("user.profile.bio")
            .AddField("user.posts.title")
            .AddField("user.posts.author")
            .AddField("admin.permissions");

        // Act - Preserve child first, then parent (order shouldn't matter)
        var result = PreservationBuilder
            .Create(query)
            .Preserve("user.profile")
            .Preserve("user")
            .Build();

        // Assert
        return result.Verify();
    }

    [Fact]
    public Task Preserve_MultipleOverlappingPaths_DeduplicatesCorrectly()
    {
        // Arrange
        var query = QueryBuilder
            .CreateDefaultBuilder("ComplexOverlapping")
            .AddField("user.profile.name")
            .AddField("user.profile.email")
            .AddField("user.profile.bio")
            .AddField("user.profile.avatar.url")
            .AddField("user.profile.avatar.size")
            .AddField("user.posts.title")
            .AddField("user.posts.content")
            .AddField("admin.permissions");

        // Act - Preserve with overlapping paths: user includes user.profile and user.profile.avatar
        var result = PreservationBuilder
            .Create(query)
            .Preserve("user", "user.profile", "user.profile.avatar")
            .Build();

        // Assert
        return result.Verify();
    }

    [Fact]
    public Task Preserve_SiblingPaths_BothIncluded()
    {
        // Arrange
        var query = QueryBuilder
            .CreateDefaultBuilder("SiblingsQuery")
            .AddField("user.profile.name")
            .AddField("user.profile.email")
            .AddField("user.posts.title")
            .AddField("user.posts.content")
            .AddField("user.settings.theme")
            .AddField("admin.permissions");

        // Act - Preserve sibling paths (not overlapping)
        var result = PreservationBuilder
            .Create(query)
            .Preserve("user.profile", "user.posts")
            .Build();

        // Assert - Both siblings should be included
        result.Definition.Fields.Should().ContainKey("user");
        var userField = result.Definition.Fields["user"];
        userField.Fields.Should().ContainKeys("profile", "posts");
        userField.Fields.Should().NotContainKey("settings");
        result.Definition.Fields.Should().NotContainKey("admin");

        return result.Verify();
    }

    [Fact]
    public Task Preserve_MixedOverlappingAndSiblings_CorrectlyMerges()
    {
        // Arrange
        var query = QueryBuilder
            .CreateDefaultBuilder("MixedQuery")
            .AddField("user.profile.name")
            .AddField("user.profile.email")
            .AddField("user.posts.title")
            .AddField("user.posts.content")
            .AddField("user.posts.author.name")
            .AddField("user.settings.theme")
            .AddField("admin.permissions");

        // Act - Mix of overlapping (user.posts + user.posts.author) and sibling (user.profile)
        // With new behavior: user.posts.author is more specific, so it wins over user.posts
        var result = PreservationBuilder
            .Create(query)
            .Preserve("user.profile", "user.posts", "user.posts.author")
            .Build();

        // Assert - Should include profile and only posts.author (more specific path wins)
        return result.Verify();
    }

    [Fact]
    public async Task Preserve_RootPathThenSpecific_SpecificWins()
    {
        // Arrange
        var query = QueryBuilder
            .CreateDefaultBuilder("RootPathQuery")
            .AddField("user.profile.name")
            .AddField("user.profile.email")
            .AddField("user.posts.title");

        // Act - Preserve root "user" then specific "user.profile.name"
        var result = PreservationBuilder
            .Create(query)
            .Preserve("user")
            .Preserve("user.profile.name")
            .Build();

        // Assert - Should only have user.profile.name, not all of user
        await result.Verify();
    }

    // PreserveFromExpression tests
    [Fact]
    public async Task PreserveFromExpression_SingleField_PreservesOnlyThatField()
    {
        var query = QueryBuilder
            .CreateDefaultBuilder("UserQuery")
            .AddField("UserQuery:data.edges.node.id")
            .AddField("data.edges.node.email");

        var result = PreservationBuilder
            .Create(query)
            .PreserveFromExpression((TestUser u) => u.id != null, "edges.node")
            .Build();

        await result.Verify();
    }

    [Fact]
    public async Task PreserveFromExpression_MultipleFields_PreservesAll()
    {
        var query = QueryBuilder
            .CreateDefaultBuilder("UserQuery")
            .AddField("UserQuery:data.edges.node.id")
            .AddField("data.edges.node.email");

        var result = PreservationBuilder
            .Create(query)
            .PreserveFromExpression((TestUser u) => u.id != null && u.email != null, "edges.node")
            .Build();

        await result.Verify();
    }

    [Fact]
    public async Task PreserveFromExpression_WholeObject_PreservesAllFields()
    {
        var query = QueryBuilder
            .CreateDefaultBuilder("UserQuery")
            .AddField("UserQuery:data.edges.node.id")
            .AddField("data.edges.node.email");

        var result = PreservationBuilder
            .Create(query)
            .PreserveFromExpression((TestUser u) => u, "edges.node")
            .Build();

        await result.Verify();
    }

    [Fact]
    public async Task PreserveFromExpression_WholeObjectThenSpecific_SpecificWins()
    {
        var query = QueryBuilder
            .CreateDefaultBuilder("UserQuery")
            .AddField("UserQuery:data.edges.node.id")
            .AddField("data.edges.node.email");

        var result = PreservationBuilder
            .Create(query)
            .PreserveFromExpression((TestUser u) => u, "edges.node")
            .PreserveFromExpression((TestUser u) => u.email, "edges.node")
            .Build();

        await result.Verify();
    }

    [Fact]
    public async Task PreserveFromExpression_ChainedCalls_AccumulatesFields()
    {
        var query = QueryBuilder
            .CreateDefaultBuilder("UserQuery")
            .AddField("UserQuery:data.edges.node.id")
            .AddField("data.edges.node.email");

        var result = PreservationBuilder
            .Create(query)
            .PreserveFromExpression((TestUser u) => u.id, "edges.node")
            .PreserveFromExpression((TestUser u) => u.email, "edges.node")
            .Build();

        await result.Verify();
    }

    [Fact]
    public async Task PreserveFromExpression_ComplexExpression_ExtractsAllReferencedFields()
    {
        var query = QueryBuilder
            .CreateDefaultBuilder("ProfileQuery")
            .AddField("ProfileQuery:data.edges.node.firstName")
            .AddField("data.edges.node.lastName");

        var result = PreservationBuilder
            .Create(query)
            .PreserveFromExpression((TestProfile p) => (p.firstName ?? "").Length > 0 && p.lastName != null, "edges.node")
            .Build();

        await result.Verify();
    }
}
