using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using NGql.Core.Builders;
using Xunit;

namespace NGql.Core.Tests.Extensions;

public class PreserveMultipleLambdaMergeTests
{
    private class TestDeposit 
    { 
        public TestDepositDetails? Deposit { get; }
    }
    
    private class TestDepositDetails
    {
        public DateTime? Date { get; }
    }
    
    private class TestProfile
    {
        public TestProfileData? ProfileData { get; }
    }
    
    private class TestProfileData
    {
        public string? Currency { get; }
    }

    [Fact]
    public async Task MultipleLambdaParameters_MergedToSamePath_PreservesAllFields()
    {
        // Simulate the real-world scenario from the issue
        var firstDepositQuery = QueryBuilder
            .CreateDefaultBuilder("PlayerFirstDepositBatchQuery")
            .AddField("PlayerFirstDepositBatchQuery:data.edges.node.PlayerId:playerId")
            .AddField("data.edges.node.Deposit:deposit.Date:date");

        var secondDepositQuery = QueryBuilder
            .CreateDefaultBuilder("PlayerSecondDepositBatchQuery")
            .AddField("PlayerSecondDepositBatchQuery:data.edges.node.PlayerId:playerId")
            .AddField("data.edges.node.Deposit:deposit.Date:date");

        var lastDepositQuery = QueryBuilder
            .CreateDefaultBuilder("PlayerLastDepositBatchQuery")
            .AddField("PlayerLastDepositBatchQuery:data.edges.node.PlayerId:playerId")
            .AddField("data.edges.node.Deposit:deposit.Date:date");

        var profileQuery = QueryBuilder
            .CreateDefaultBuilder("PlayerProfileBatchQuery")
            .AddField("PlayerProfileBatchQuery:data.edges.node.PlayerId:playerId")
            .AddField("data.edges.node.ProfileData:profileData.Currency:currency");

        // Merge with MergeByFieldPath - all will merge
        var mergedQuery = QueryBuilder
            .CreateDefaultBuilder("MergedQuery", MergingStrategy.MergeByFieldPath)
            .Include(firstDepositQuery)
            .Include(secondDepositQuery)
            .Include(lastDepositQuery)
            .Include(profileQuery);

        // After merge, localMap reflects the actual structure
        // ALL queries merged into PlayerFirstDepositBatchQuery
        var localMap = new Dictionary<string, string[]>
        {
            { "playerFirstDeposit", new[] { "PlayerFirstDepositBatchQuery" } },
            { "playerSecondDeposit", new[] { "PlayerFirstDepositBatchQuery" } }, // MERGED!
            { "playerLastDeposit", new[] { "PlayerFirstDepositBatchQuery" } }, // MERGED!
            { "playerProfile", new[] { "PlayerFirstDepositBatchQuery" } } // MERGED!
        };

        // Expression from the issue - references nested properties
        var result = PreservationBuilder
            .Create(mergedQuery)
            .PreserveAtPath("PlayerId", "edges.node")
            .PreserveFromExpression((TestDeposit playerFirstDeposit, TestDeposit playerLastDeposit, TestProfile playerProfile) => 
                (playerFirstDeposit.Deposit.Date != null && 
                 playerFirstDeposit.Deposit.Date == playerLastDeposit.Deposit.Date &&
                 playerProfile.ProfileData.Currency == "EUR"),
                "edges.node", localMap)
            .Build();

        // Should preserve:
        // - PlayerId from all queries
        // - Deposit.Date from playerFirstDeposit (maps to PlayerFirstDepositBatchQuery)
        // - Deposit.Date from playerLastDeposit (ALSO maps to PlayerFirstDepositBatchQuery)
        // - ProfileData.Currency from playerProfile (maps to PlayerFirstDepositBatchQuery)
        
        await result.Verify();
    }
}
