using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using NGql.Core.Builders;
using Xunit;

namespace NGql.Core.Tests.Extensions;

public class PreserveExtensionsTestsSimplified
{
    // Test models
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
    
    private class TestProfile
    {
        public TestProfileData? ProfileData { get; set; }
    }
    
    private class TestProfileData
    {
        public string? Currency { get; set; }
    }

    // Query factories
    private static QueryBuilder CreateSingleQuery() => QueryBuilder
        .CreateDefaultBuilder("TestQuery")
        .AddField("TestQuery:data.edges.node.UserId:userId")
        .AddField("data.edges.node.RegData:regData.Region:region")
        .AddField("data.edges.node.RegData:regData.RegistrationDate:registrationDate");

    private static QueryBuilder CreateFirstDepositQuery() => QueryBuilder
        .CreateDefaultBuilder("PlayerFirstDepositBatchQuery")
        .WithMergingStrategy(MergingStrategy.NeverMerge)
        .AddField("PlayerFirstDepositBatchQuery:data.edges.node.UserId:userId")
        .AddField("data.edges.node.Deposit:deposit.FirstDepositTime:firstDepositTime");

    private static QueryBuilder CreateSecondDepositQuery() => QueryBuilder
        .CreateDefaultBuilder("PlayerSecondDepositBatchQuery")
        .WithMergingStrategy(MergingStrategy.NeverMerge)
        .AddField("PlayerSecondDepositBatchQuery:data.edges.node.UserId:userId")
        .AddField("data.edges.node.Deposit:deposit.SecondDepositTime:secondDepositTime");

    private static QueryBuilder CreateLastDepositQuery() => QueryBuilder
        .CreateDefaultBuilder("PlayerLastDepositBatchQuery")
        .WithMergingStrategy(MergingStrategy.NeverMerge)
        .AddField("PlayerLastDepositBatchQuery:data.edges.node.UserId:userId")
        .AddField("data.edges.node.Deposit:deposit.LastDepositTime:lastDepositTime");

    private static QueryBuilder CreateProfileQuery() => QueryBuilder
        .CreateDefaultBuilder("PlayerProfileBatchQuery")
        .AddField("PlayerProfileBatchQuery:data.edges.node.UserId:userId")
        .AddField("data.edges.node.ProfileData:profileData.Currency:currency");

    private static QueryBuilder CreateMergedQuery() => QueryBuilder
        .CreateDefaultBuilder("DataApiSourceBatchQuery", MergingStrategy.MergeByFieldPath)
        .Include(CreateFirstDepositQuery())
        .Include(CreateSecondDepositQuery())
        .Include(CreateLastDepositQuery())
        .Include(CreateProfileQuery());

    [Fact]
    public async Task MultiParameterLambda_AllQueriesMerged_PreservesAllFields()
    {
        var mergedQuery = CreateMergedQuery();

        var localMap = new Dictionary<string, string[]>
        {
            { "playerFirstDeposit", new[] { "PlayerFirstDepositBatchQuery" } },
            { "playerSecondDeposit", new[] { "PlayerSecondDepositBatchQuery" } },
            { "playerLastDeposit", new[] { "PlayerLastDepositBatchQuery" } },
            { "playerProfile", new[] { "PlayerProfileBatchQuery" } }
        };

        var result = PreservationBuilder
            .Create(mergedQuery)
            .PreserveFromExpression(
                (TestDeposit playerFirstDeposit, TestDeposit playerLastDeposit, TestProfile playerProfile, TestDeposit playerSecondDeposit) => 
                    playerFirstDeposit.Deposit.FirstDepositTime != null && 
                    playerLastDeposit.Deposit.LastDepositTime != null &&
                    playerSecondDeposit.Deposit.SecondDepositTime != null &&
                    playerProfile.ProfileData.Currency == "USD",
                "edges.node", localMap, "UserId")
            .Build();

        await result.Verify();
    }

    [Fact]
    public async Task MultiParameterLambda_TwoParametersUsed_PreservesTwoQueries()
    {
        var mergedQuery = CreateMergedQuery();

        var localMap = new Dictionary<string, string[]>
        {
            { "playerFirstDeposit", new[] { "PlayerFirstDepositBatchQuery" } },
            { "playerLastDeposit", new[] { "PlayerLastDepositBatchQuery" } }
        };

        var result = PreservationBuilder
            .Create(mergedQuery)
            .PreserveFromExpression(
                (TestDeposit playerFirstDeposit, TestDeposit playerLastDeposit) => 
                    playerFirstDeposit.Deposit.FirstDepositTime == playerLastDeposit.Deposit.LastDepositTime,
                "edges.node", localMap, "UserId")
            .Build();

        await result.Verify();
    }

    [Fact]
    public async Task SingleParameterLambda_PreservesSpecificFields()
    {
        var query = CreateSingleQuery();

        var localMap = new Dictionary<string, string[]>
        {
            { "p", new[] { "TestQuery" } }
        };

        var result = PreservationBuilder
            .Create(query)
            .PreserveFromExpression(
                (TestProfile p) => p.ProfileData.Currency == "EUR",
                "edges.node", localMap, "UserId")
            .Build();

        await result.Verify();
    }

    [Fact]
    public async Task Preserve_DirectPath_PreservesField()
    {
        var query = CreateSingleQuery();

        var result = PreservationBuilder
            .Create(query)
            .PreserveAtPath("UserId", "edges.node")
            .Preserve("TestQuery.data.edges.node.RegData")
            .Build();

        await result.Verify();
    }

    [Fact]
    public async Task PreserveAtPath_FindsFieldByAlias()
    {
        var query = CreateSingleQuery();

        var result = PreservationBuilder
            .Create(query)
            .PreserveAtPath("UserId", "edges.node")
            .PreserveAtPath("region", "edges.node.RegData")
            .Build();

        await result.Verify();
    }
}
