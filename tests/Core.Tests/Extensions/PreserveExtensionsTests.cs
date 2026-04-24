using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using NGql.Core.Builders;
using Xunit;

namespace NGql.Core.Tests.Extensions;

public class PreserveExtensionsTests
{
    [Fact]
    public Task Preserve_SingleFieldPath_PreservesOnlyThatField()
    {
        // Arrange
        var query = QueryBuilder
            .CreateDefaultBuilder("TestQuery")
            .AddField("TestQuery:data.edges.node.UserId:userId")
            .AddField("data.edges.node.Email:email")
            .AddField("data.edges.node.Profile:profile.Name:name")
            .AddField("data.edges.node.Profile:profile.Age:age");

        // Act - preserve only the userId field
        var result = PreservationBuilder.Create(query)
            .Preserve("TestQuery.edges.node.userId")
            .Build();

        // Assert
        return result.Verify();
    }

    [Fact]
    public Task Preserve_MultipleFieldPaths_PreservesAll()
    {
        // Arrange
        var query = QueryBuilder
            .CreateDefaultBuilder("TestQuery")
            .AddField("TestQuery:data.edges.node.UserId:userId")
            .AddField("data.edges.node.Email:email")
            .AddField("data.edges.node.Profile:profile.Name:name")
            .AddField("data.edges.node.Profile:profile.Age:age");

        // Act - preserve userId and email
        var result = PreservationBuilder.Create(query)
            .Preserve("TestQuery.edges.node.userId", "TestQuery.edges.node.email")
            .Build();

        // Assert
        return result.Verify();
    }

    [Fact]
    public Task Preserve_NestedPath_PreservesFullPath()
    {
        // Arrange
        var query = QueryBuilder
            .CreateDefaultBuilder("TestQuery")
            .AddField("TestQuery:data.edges.node.UserId:userId")
            .AddField("data.edges.node.Profile:profile.Name:name")
            .AddField("data.edges.node.Profile:profile.Age:age")
            .AddField("data.edges.node.Profile:profile.Address:address.City:city")
            .AddField("data.edges.node.Profile:profile.Address:address.Street:street");

        // Act - preserve the nested profile.address path (should include all address children) + userId
        var result = PreservationBuilder.Create(query)
            .Preserve("TestQuery.edges.node.userId", "TestQuery.edges.node.profile.address")
            .Build();

        // Assert
        return result.Verify();
    }

    [Fact]
    public Task Preserve_ParentPath_PreservesAllChildren()
    {
        // Arrange
        var query = QueryBuilder
            .CreateDefaultBuilder("TestQuery")
            .AddField("TestQuery:data.edges.node.UserId:userId")
            .AddField("data.edges.node.Profile:profile.Name:name")
            .AddField("data.edges.node.Profile:profile.Age:age")
            .AddField("data.edges.node.Settings:settings.Theme:theme")
            .AddField("data.edges.node.Settings:settings.Language:language");

        // Act - preserve entire profile (should include name and age) + userId
        var result = PreservationBuilder.Create(query)
            .Preserve("TestQuery.edges.node.userId", "TestQuery.edges.node.profile")
            .Build();

        // Assert
        return result.Verify();
    }

    [Fact]
    public Task Preserve_NonExistentPath_ReturnsEmptyQuery()
    {
        // Arrange
        var query = QueryBuilder
            .CreateDefaultBuilder("TestQuery")
            .AddField("TestQuery:data.edges.node.UserId:userId")
            .AddField("data.edges.node.Email:email");

        // Act - try to preserve a field that doesn't exist
        var result = PreservationBuilder.Create(query)
            .Preserve("nonExistentField")
            .Build();

        // Assert
        return result.Verify();
    }

    [Fact]
    public Task Preserve_EmptyFieldPaths_ReturnsOriginalQuery()
    {
        // Arrange
        var query = QueryBuilder
            .CreateDefaultBuilder("TestQuery")
            .AddField("TestQuery:data.edges.node.UserId:userId")
            .AddField("data.edges.node.Email:email");

        // Act - preserve with no paths (should return original)
        var result = PreservationBuilder.Create(query)
            .Preserve()
            .Build();

        // Assert
        return result.Verify();
    }

    [Fact]
    public Task Preserve_WithAliases_MatchesByAlias()
    {
        // Arrange
        var query = QueryBuilder
            .CreateDefaultBuilder("TestQuery")
            .AddField("TestQuery:data.edges.node.UserId:userId")
            .AddField("data.edges.node.UserEmail:email") // alias different from field name
            .AddField("data.edges.node.Profile:profile.DisplayName:name"); // alias different from field name

        // Act - preserve using alias names + userId
        var result = PreservationBuilder.Create(query)
            .Preserve("TestQuery.edges.node.userId", "TestQuery.edges.node.email", "TestQuery.edges.node.profile.name")
            .Build();

        // Assert
        return result.Verify();
    }


    [Fact]
    public Task Preserve_AfterMerge_PreservesFromMergedStructure()
    {
        // Arrange
        var queryA = QueryBuilder
            .CreateDefaultBuilder("QueryA")
            .AddField("QueryA:data.edges.node.UserId:userId")
            .AddField("data.edges.node.Settings:settings.SettingA:settingA");

        var queryB = QueryBuilder
            .CreateDefaultBuilder("QueryB")
            .AddField("QueryB:data.edges.node.UserId:userId")
            .AddField("data.edges.node.Settings:settings.SettingB:settingB");

        var mergedQuery = QueryBuilder
            .CreateDefaultBuilder("MergedQuery", MergingStrategy.MergeByFieldPath)
            .Include(queryA)
            .Include(queryB);

        // Act - preserve userId and settingA from merged query
        var result = PreservationBuilder.Create(mergedQuery)
            .Preserve("QueryA.edges.node.userId", "QueryA.edges.node.settings.settingA")
            .Build();

        // Assert
        return result.Verify();
    }

    [Fact]
    public Task Preserve_CombinedMethods_AllWorkTogether()
    {
        // Arrange
        var query = QueryBuilder
            .CreateDefaultBuilder("TestQuery")
            .AddField("TestQuery:data.edges.node.UserId:userId")
            .AddField("data.edges.node.Email:email")
            .AddField("data.edges.node.Name:name")
            .AddField("data.edges.node.Profile:profile.Bio:bio")
            .AddField("data.edges.node.Profile:profile.Avatar:avatar")
            .AddField("data.edges.node.Settings:settings.Theme:theme");

        // Act - combine different preservation methods
        var result = PreservationBuilder.Create(query)
            .Preserve("TestQuery.edges.node.userId")
            .PreserveAtPath("email", "edges.node")
            .PreserveFromExpression(
                (TestDataModels.UserWithProfile user) => user.Profile.Bio != null,
                "edges.node")
            .Build();

        // Assert
        return result.Verify();
    }

    [Fact]
    public Task Preserve_OverlappingPaths_ParentAndChild_ChildWins()
    {
        // Arrange
        var query = QueryBuilder
            .CreateDefaultBuilder("TestQuery")
            .AddField("TestQuery:data.edges.node.UserId:userId")
            .AddField("data.edges.node.Profile:profile.Name:name")
            .AddField("data.edges.node.Profile:profile.Bio:bio")
            .AddField("data.edges.node.Profile:profile.Avatar:avatar")
            .AddField("data.edges.node.Email:email");

        // Act - preserve parent path then child (child should win) + userId
        var result = PreservationBuilder.Create(query)
            .Preserve("TestQuery.edges.node.userId")
            .Preserve("TestQuery.edges.node.profile")
            .Preserve("TestQuery.edges.node.profile.name")
            .Build();

        // Assert - should only preserve profile.name, not all of profile
        return result.Verify();
    }

    [Fact]
    public Task Preserve_WithVariables_IncludesVariablesInPreservedFields()
    {
        // Arrange
        var query = QueryBuilder
            .CreateDefaultBuilder("TestQuery")
            .AddField("TestQuery:data.edges.first:$limit.node.UserId:userId")
            .AddField("data.edges.first:$limit.node.Email:email");

        // Act - preserve field with variable
        var result = PreservationBuilder.Create(query)
            .Preserve("TestQuery.edges.first.node.userId")
            .Build();

        // Assert - should include variable
        return result.Verify();
    }

    [Fact]
    public Task Preserve_CaseInsensitiveMatching_WorksCorrectly()
    {
        // Arrange
        var query = QueryBuilder
            .CreateDefaultBuilder("TestQuery")
            .AddField("TestQuery:data.edges.node.UserId:userId")
            .AddField("data.edges.node.Email:email")
            .AddField("data.edges.node.Name:name");

        // Act - use different case for field names
        var result = PreservationBuilder.Create(query)
            .Preserve("TestQuery.edges.node.USERID", "TestQuery.edges.node.EmAiL")
            .Build();

        // Assert
        return result.Verify();
    }

    [Fact]
    public Task Preserve_ChainedPreserveCalls_Accumulates()
    {
        // Arrange
        var query = QueryBuilder
            .CreateDefaultBuilder("TestQuery")
            .AddField("TestQuery:data.edges.node.UserId:userId")
            .AddField("data.edges.node.Email:email")
            .AddField("data.edges.node.Name:name")
            .AddField("data.edges.node.Age:age");

        // Act - chain multiple Preserve calls
        var result = PreservationBuilder.Create(query)
            .Preserve("TestQuery.edges.node.userId")
            .Preserve("TestQuery.edges.node.email")
            .Preserve("TestQuery.edges.node.name")
            .Build();

        // Assert - should preserve all three fields
        return result.Verify();
    }

    [Fact]
    public Task Preserve_DuplicatePaths_NoDuplication()
    {
        // Arrange
        var query = QueryBuilder
            .CreateDefaultBuilder("TestQuery")
            .AddField("TestQuery:data.edges.node.UserId:userId")
            .AddField("data.edges.node.Email:email");

        // Act - preserve same path multiple times
        var result = PreservationBuilder.Create(query)
            .Preserve("TestQuery.edges.node.userId", "TestQuery.edges.node.userId", "TestQuery.edges.node.userId")
            .Build();

        // Assert - should only preserve once
        return result.Verify();
    }

    [Fact]
    public Task Preserve_VeryDeepNesting_PreservesCorrectly()
    {
        // Arrange
        var query = QueryBuilder
            .CreateDefaultBuilder("TestQuery")
            .AddField("TestQuery:data.edges.node.UserId:userId")
            .AddField("data.edges.node.Level1:level1.Level2:level2.Level3:level3.Level4:level4.Level5:level5.Value:value")
            .AddField("data.edges.node.Level1:level1.Level2:level2.OtherField:otherField");

        // Act - preserve very deep path + userId
        var result = PreservationBuilder.Create(query)
            .Preserve("TestQuery.edges.node.userId", "TestQuery.edges.node.level1.level2.level3.level4.level5.value")
            .Build();

        // Assert
        return result.Verify();
    }

    [Fact]
    public void Preserve_MixedArgumentsAndMetadata()
    {
        var query = QueryBuilder
            .CreateDefaultBuilder("TestQuery")
            .AddField("TestQuery:data.edges.node.UserId:userId", "Int", new Dictionary<string, object?> { { "id", 1 } })
            .AddField("data.edges.node.Email:email", "String", new Dictionary<string, object?> { { "verified", true } });

        // Act - preserve fields with arguments
        var result = PreservationBuilder.Create(query)
            .Preserve("TestQuery.edges.node.userId")
            .Preserve("TestQuery.edges.node.email")
            .Build();

        // Assert
        result.Should().NotBeNull();
        result.Definition.Should().NotBeNull();
    }

    [Fact]
    public void Preserve_WithTypeConversion()
    {
        var query = QueryBuilder
            .CreateDefaultBuilder("TestQuery")
            .AddField("data", "object")
            .AddField("data.edges", "object")
            .AddField("data.edges.node", "User")
            .AddField("data.edges.node.profile", "Profile")
            .AddField("data.edges.node.profile.name", "String");

        // Act
        var result = PreservationBuilder.Create(query)
            .Preserve("TestQuery.edges.node.profile.name")
            .Build();

        // Assert
        result.Should().NotBeNull();
        result.Definition.Should().NotBeNull();
    }

    [Fact]
    public void Preserve_EmptyPreserveList()
    {
        var query = QueryBuilder
            .CreateDefaultBuilder("TestQuery")
            .AddField("data.edges.node.userId");

        // Act - create builder but don't preserve anything
        var result = PreservationBuilder.Create(query)
            .Build();

        // Assert
        result.Should().NotBeNull();
        result.Definition.Should().NotBeNull();
    }
}
