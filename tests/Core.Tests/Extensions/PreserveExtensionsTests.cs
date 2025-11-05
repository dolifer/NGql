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

    // Test models
    private class TestUser { public string? id { get; set; } public string? email { get; set; } }
    private class TestProfile { public string? firstName { get; set; } public string? lastName { get; set; } }
    private class TestDeposit
    {
        public TestDepositDetails? Deposit { get; set; }
    }

    private class TestDepositDetails
    {
        public DateTime? FirstDepositTime { get; set; }
        public DateTime? SecondDepositTime { get; set; }
        public DateTime? LastDepositTime { get; set; }
    }

    private class TestPlayerProfile
    {
        public string? UserId { get; set; }
        public TestProfileData? ProfileData { get; set; }
        public TestRegData? RegData { get; set; }
    }

    private class TestProfileData
    {
        public string? Currency { get; set; }
    }

    private class TestRegData
    {
        public string? Region { get; set; }
        public DateTime? registrationDate { get; set; }
        public string? registrationType { get; set; }
    }

    // Query factories - Keep it DRY
    private static QueryBuilder CreateSimpleQuery() => QueryBuilder
        .CreateDefaultBuilder("PreservedQuery")
        .AddField("PreservedQuery:data.edges.node.id")
        .AddField("data.edges.node.email");

    private static QueryBuilder CreateQueryWithAliases() => QueryBuilder
        .CreateDefaultBuilder("PreservedQuery")
        .AddField("UserAlias:user.ProfileAlias:profile.name")
        .AddField("UserAlias:user.ProfileAlias:profile.bio")
        .AddField("UserAlias:user.posts.title");

    private static QueryBuilder CreatePreservedQuery() => QueryBuilder
        .CreateDefaultBuilder("PreservedQuery")
        .AddField("PreservedQuery:data.edges.node.UserId:userId")
        .AddField("data.edges.node.RegData:regData.Region:region")
        .AddField("data.edges.node.RegData:regData.RegistrationDate:registrationDate")
        .AddField("data.edges.node.RegData:regData.RegistrationType:registrationType");

    private static QueryBuilder CreateQueryWithVariables() => QueryBuilder
        .CreateDefaultBuilder("PreservedQuery")
        .AddField("user", new Dictionary<string, object?> { {"id", "123"} })
        .AddField("user.name")
        .AddField("user.posts", new Dictionary<string, object?> { {"limit", SizeVariable} })
        .AddField("user.posts.title");

    private static QueryBuilder CreateFirstDepositQuery() => QueryBuilder
        .CreateDefaultBuilder("PreservedQuery")
        .WithMergingStrategy(MergingStrategy.NeverMerge)
        .AddField("PreservedQuery:data.edges.node.UserId:userId")
        .AddField("data.edges.node.Deposit:deposit.FirstDepositTime:firstDepositTime");

    private static QueryBuilder CreateSecondDepositQuery() => QueryBuilder
        .CreateDefaultBuilder("PreservedQuery")
        .WithMergingStrategy(MergingStrategy.NeverMerge)
        .AddField("PreservedQuery:data.edges.node.UserId:userId")
        .AddField("data.edges.node.Deposit:deposit.SecondDepositTime:secondDepositTime");

    private static QueryBuilder CreateLastDepositQuery() => QueryBuilder
        .CreateDefaultBuilder("PreservedQuery")
        .WithMergingStrategy(MergingStrategy.NeverMerge)
        .AddField("PreservedQuery:data.edges.node.UserId:userId")
        .AddField("data.edges.node.Deposit:deposit.LastDepositTime:lastDepositTime");

    private static QueryBuilder CreateProfileQuery() => QueryBuilder
        .CreateDefaultBuilder("PreservedQueryProfile")
        .AddField("PreservedQueryProfile:data.edges.node.UserId:userId")
        .AddField("data.edges.node.ProfileData:profileData.Currency:currency");

    private static QueryBuilder CreateMergedQuery() => QueryBuilder
        .CreateDefaultBuilder("PreservedQuery", MergingStrategy.MergeByFieldPath)
        .Include(CreateFirstDepositQuery())
        .Include(CreateSecondDepositQuery())
        .Include(CreateLastDepositQuery())
        .Include(CreateProfileQuery());

    // ===== BASIC PRESERVATION TESTS =====

    [Fact]
    public Task Preserve_ByDirectPath_PreservesField()
    {
        var query = CreateSimpleQuery();
        var result = PreservationBuilder.Create(query).Preserve("PreservedQuery.data.edges.node.id").Build();
        return result.Verify();
    }

    [Fact]
    public Task Preserve_MultiplePaths_PreservesBoth()
    {
        var query = CreateSimpleQuery();
        var result = PreservationBuilder.Create(query)
            .Preserve("PreservedQuery.data.edges.node.id", "PreservedQuery.data.edges.node.email")
            .Build();
        return result.Verify();
    }

    [Fact]
    public Task Preserve_ParentPath_PreservesAllChildren()
    {
        var query = CreatePreservedQuery();
        var result = PreservationBuilder.Create(query)
            .Preserve("PreservedQuery.data.edges.node.RegData")
            .Build();
        return result.Verify();
    }

    [Fact]
    public Task Preserve_ChildThenParent_ParentWins()
    {
        var query = CreatePreservedQuery();
        var result = PreservationBuilder.Create(query)
            .Preserve("PreservedQuery.data.edges.node.RegData.Region")
            .Preserve("PreservedQuery.data.edges.node.RegData")
            .Build();
        return result.Verify();
    }

    // ===== ALIAS TESTS =====

    [Fact]
    public Task Preserve_WithAliases_PreservesByAlias()
    {
        var query = CreateQueryWithAliases();
        var result = PreservationBuilder.Create(query)
            .Preserve("UserAlias.ProfileAlias")
            .Build();
        return result.Verify();
    }

    [Fact]
    public Task Preserve_WithAliases_PreservesByFieldName()
    {
        var query = CreateQueryWithAliases();
        var result = PreservationBuilder.Create(query)
            .Preserve("user.profile")
            .Build();
        return result.Verify();
    }

    // ===== METADATA & VARIABLES TESTS =====

    [Fact]
    public async Task Preserve_WithVariables_PreservesVariablesAndMetadata()
    {
        var query = CreateQueryWithVariables();
        query.Definition.Metadata["testKey"] = "testValue";
        query.Definition.MergingStrategy = MergingStrategy.NeverMerge;

        var result = PreservationBuilder.Create(query)
            .Preserve("user.posts")
            .Build();

        result.Definition.Name.Should().Be("PreservedQuery");
        result.Definition.MergingStrategy.Should().Be(MergingStrategy.NeverMerge);
        result.Definition.Metadata.Should().ContainKey("testKey").WhoseValue.Should().Be("testValue");
        result.Definition.Variables.Should().ContainSingle().Which.Should().Be(SizeVariable);

        await result.Verify();
    }

    // ===== PRESERVEATPATH TESTS =====

    [Fact]
    public Task PreserveAtPath_FindsFieldByAlias()
    {
        var query = CreatePreservedQuery();
        var result = PreservationBuilder.Create(query)
            .PreserveAtPath("UserId", "edges.node")
            .PreserveAtPath("region", "edges.node.RegData")
            .Build();
        return result.Verify();
    }

    [Fact]
    public Task PreserveAtPath_WithNestedObject_PreservesEntireObject()
    {
        var query = CreatePreservedQuery();
        var result = PreservationBuilder.Create(query)
            .PreserveAtPath("RegData", "edges.node")
            .Build();
        return result.Verify();
    }

    // ===== PRESERVEFROMEXPRESSION TESTS =====

    [Fact]
    public Task PreserveFromExpression_SingleField_PreservesOnlyThatField()
    {
        var query = CreateSimpleQuery();
        var result = PreservationBuilder.Create(query)
            .PreserveFromExpression((TestUser u) => u.id != null, "edges.node")
            .Build();
        return result.Verify();
    }

    [Fact]
    public Task PreserveFromExpression_MultipleFields_PreservesAll()
    {
        var query = CreateSimpleQuery();
        var result = PreservationBuilder.Create(query)
            .PreserveFromExpression((TestUser u) => u.id != null && u.email != null, "edges.node")
            .Build();
        return result.Verify();
    }

    [Fact]
    public Task PreserveFromExpression_WholeObject_PreservesAllFields()
    {
        var query = CreateSimpleQuery();
        var result = PreservationBuilder.Create(query)
            .PreserveFromExpression((TestUser u) => u, "edges.node")
            .Build();
        return result.Verify();
    }

    [Fact]
    public Task PreserveFromExpression_NestedObject_PreservesObject()
    {
        var query = CreatePreservedQuery();
        var result = PreservationBuilder.Create(query)
            .PreserveFromExpression((TestPlayerProfile p) => p.RegData != null, "edges.node")
            .Build();
        return result.Verify();
    }

    [Fact]
    public Task PreserveFromExpression_NestedField_PreservesOnlyField()
    {
        var query = CreatePreservedQuery();
        var localMap = new Dictionary<string, string[]>
        {
            { "p", new[] { "PreservedQuery" } }
        };
        var result = PreservationBuilder.Create(query)
            .PreserveAtPath("UserId", "edges.node")
            .PreserveFromExpression((TestPlayerProfile p) => p.RegData.Region != null, "edges.node", localMap)
            .Build();
        return result.Verify();
    }

    [Fact]
    public Task PreserveFromExpression_NullConditional_PreservesField()
    {
        var query = CreatePreservedQuery();
        var localMap = new Dictionary<string, string[]>
        {
            { "p", new[] { "PreservedQuery" } }
        };
        var result = PreservationBuilder.Create(query)
            .PreserveAtPath("UserId", "edges.node")
            .PreserveFromExpression((TestPlayerProfile p) =>
                (p == null ? null : p.RegData.Region) != null,
                "edges.node", localMap)
            .Build();
        return result.Verify();
    }

    [Fact]
    public Task PreserveFromExpression_MethodCallArgument_PreservesField()
    {
        var query = CreatePreservedQuery();
        var localMap = new Dictionary<string, string[]>
        {
            { "p", new[] { "PreservedQuery" } }
        };
        var result = PreservationBuilder.Create(query)
            .PreserveAtPath("UserId", "edges.node")
            .PreserveFromExpression((TestPlayerProfile p) =>
                !string.IsNullOrWhiteSpace(p.RegData.Region),
                "edges.node", localMap)
            .Build();
        return result.Verify();
    }

    [Fact]
    public Task PreserveFromExpression_ComplexBooleanLogic_MostSpecificWins()
    {
        var query = CreatePreservedQuery();
        var localMap = new Dictionary<string, string[]>
        {
            { "p", new[] { "PreservedQuery" } }
        };
        var result = PreservationBuilder.Create(query)
            .PreserveFromExpression((TestPlayerProfile p) =>
                (p.RegData != null && p.RegData.Region != null) || p.RegData.registrationDate != null,
                "edges.node", localMap)
            .Build();
        return result.Verify();
    }

    [Fact]
    public Task PreserveFromExpression_WholeObjectThenSpecific_SpecificWins()
    {
        var query = CreateSimpleQuery();
        var result = PreservationBuilder.Create(query)
            .PreserveFromExpression((TestUser u) => u, "edges.node")
            .PreserveFromExpression((TestUser u) => u.email, "edges.node")
            .Build();
        return result.Verify();
    }

    [Fact]
    public Task PreserveFromExpression_ChainedCalls_AccumulatesFields()
    {
        var query = CreateSimpleQuery();
        var result = PreservationBuilder.Create(query)
            .PreserveFromExpression((TestUser u) => u.id, "edges.node")
            .PreserveFromExpression((TestUser u) => u.email, "edges.node")
            .Build();
        return result.Verify();
    }

    // ===== MULTI-PARAMETER LAMBDA TESTS =====

    [Fact]
    public Task MultiParameterLambda_AllQueriesMerged_PreservesAllFields()
    {
        var mergedQuery = CreateMergedQuery();
        var localMap = new Dictionary<string, string[]>
        {
            { "playerFirstDeposit", new[] { "PreservedQuery" } },
            { "playerSecondDeposit", new[] { "PreservedQuery" } },
            { "playerLastDeposit", new[] { "PreservedQuery" } },
            { "playerProfile", new[] { "PreservedQueryProfile" } }
        };

        var result = PreservationBuilder.Create(mergedQuery)
            .PreserveFromExpression(
                (TestDeposit playerFirstDeposit, TestDeposit playerLastDeposit, TestPlayerProfile playerProfile, TestDeposit playerSecondDeposit) =>
                    playerFirstDeposit.Deposit.FirstDepositTime != null &&
                    playerLastDeposit.Deposit.LastDepositTime != null &&
                    playerSecondDeposit.Deposit.SecondDepositTime != null &&
                    playerProfile.ProfileData.Currency == "USD",
                "edges.node", localMap, "UserId")
            .Build();

        return result.Verify();
    }

    [Fact]
    public Task MultiParameterLambda_TwoParametersUsed_PreservesTwoQueries()
    {
        var mergedQuery = CreateMergedQuery();
        var localMap = new Dictionary<string, string[]>
        {
            { "playerFirstDeposit", new[] { "PreservedQuery" } },
            { "playerLastDeposit", new[] { "PreservedQuery" } }
        };

        var result = PreservationBuilder.Create(mergedQuery)
            .PreserveFromExpression(
                (TestDeposit playerFirstDeposit, TestDeposit playerLastDeposit) =>
                    playerFirstDeposit.Deposit.FirstDepositTime == playerLastDeposit.Deposit.LastDepositTime,
                "edges.node", localMap, "UserId")
            .Build();

        return result.Verify();
    }

    [Fact]
    public Task MultiParameterLambda_SameParametersMapToSamePath_PreservesAllFields()
    {
        var firstQuery = QueryBuilder
            .CreateDefaultBuilder("PreservedQueryFirst")
            .AddField("PreservedQueryFirst:data.edges.node.id")
            .AddField("PreservedQueryFirst:data.edges.node.Deposit:deposit.FirstDepositTime:firstDepositTime");

        var secondQuery = QueryBuilder
            .CreateDefaultBuilder("PreservedQuerySecond")
            .WithMergingStrategy(MergingStrategy.NeverMerge)
            .AddField("PreservedQuerySecond:data.edges.node.firstName")
            .AddField("PreservedQuerySecond:data.edges.node.lastName");

        var mergedQuery = QueryBuilder
            .CreateDefaultBuilder("PreservedQuery", MergingStrategy.MergeByFieldPath)
            .Include(firstQuery)
            .Include(secondQuery);

        var localMap = new Dictionary<string, string[]>
        {
            { "first", new[] { "PreservedQueryFirst" } },
            { "last", new[] { "PreservedQueryFirst" } },
            { "profile", new[] { "PreservedQuerySecond" } }
        };

        var result = PreservationBuilder.Create(mergedQuery)
            .PreserveFromExpression((TestDeposit first, TestDeposit last, TestProfile profile) =>
                first.Deposit.FirstDepositTime != null &&
                last.Deposit.LastDepositTime != null &&
                profile.firstName == "Test",
                "edges.node", localMap)
            .Build();

        return result.Verify();
    }
}
