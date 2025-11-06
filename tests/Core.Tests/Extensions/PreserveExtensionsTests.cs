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

    // Separate deposit types with Date navigation properties
    private class FirstDepositDetails
    {
        public DateTime? FirstDepositTime { get; set; }
        public DateTime? Date => FirstDepositTime;
    }

    private class SecondDepositDetails
    {
        public DateTime? SecondDepositTime { get; set; }
        public DateTime? Date => SecondDepositTime;
    }

    private class LastDepositDetails
    {
        public DateTime? LastDepositTime { get; set; }
        public DateTime? Date => LastDepositTime;
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
        .AddField("PreservedQuery:data.edges.node.UserId:userId")
        .AddField("data.edges.node.Deposit:deposit.FirstDepositTime:firstDepositTime");

    private static QueryBuilder CreateSecondDepositQuery() => QueryBuilder
        .CreateDefaultBuilder("PreservedQuery")
        .AddField("PreservedQuery:data.edges.node.UserId:userId")
        .AddField("data.edges.node.Deposit:deposit.SecondDepositTime:secondDepositTime");

    private static QueryBuilder CreateLastDepositQuery() => QueryBuilder
        .CreateDefaultBuilder("PreservedQuery")
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
            .AddField("PreservedQueryFirst:data.edges.node.UserId:userId")
            .AddField("PreservedQueryFirst:data.edges.node.Deposit:deposit.FirstDepositTime:firstDepositTime");

        var secondQuery = QueryBuilder
            .CreateDefaultBuilder("PreservedQuerySecond")
            .WithMergingStrategy(MergingStrategy.NeverMerge)
            .AddField("PreservedQuerySecond:data.edges.node.UserId:userId")
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
                "edges.node", localMap, "UserId")
            .Build();

        return result.Verify();
    }

    // ===== EDGE CASE TESTS =====

    [Fact]
    public Task EdgeCase_ParameterNameSubstringCollision_PreservesCorrectly()
    {
        var query = QueryBuilder
            .CreateDefaultBuilder("PreservedQuery")
            .AddField("PreservedQuery:data.edges.node.UserId:userId")
            .AddField("data.edges.node.Name:name")
            .AddField("data.edges.node.Profile:profile.Currency:currency");

        var localMap = new Dictionary<string, string[]>
        {
            { "user", new[] { "PreservedQuery" } },
            { "userProfile", new[] { "PreservedQuery" } }
        };

        // Ensure "user" doesn't accidentally match "userProfile" prefix
        var result = PreservationBuilder.Create(query)
            .PreserveFromExpression(
                (TestUser user, TestProfile userProfile) =>
                    user.id != null && userProfile.firstName != null,
                "edges.node", localMap)
            .Build();

        return result.Verify();
    }

    [Fact]
    public Task EdgeCase_MissingLocalMapEntry_SkipsUnmappedParameter()
    {
        var query = CreateSimpleQuery();
        var localMap = new Dictionary<string, string[]>
        {
            { "user", new[] { "PreservedQuery" } }
            // Missing "profile" entry
        };

        // Should only preserve fields from mapped parameters
        var result = PreservationBuilder.Create(query)
            .PreserveFromExpression(
                (TestUser user, TestProfile profile) =>
                    user.id != null && profile.firstName != null,
                "edges.node", localMap)
            .Build();

        return result.Verify();
    }

    [Fact]
    public Task EdgeCase_ConstantExpression_PreservesAllTypeFields()
    {
        var query = CreateSimpleQuery();
        var localMap = new Dictionary<string, string[]>
        {
            { "user", new[] { "PreservedQuery" } }
        };

        // Expression that doesn't access fields - should preserve all type fields
        var result = PreservationBuilder.Create(query)
            .PreserveFromExpression((TestUser user) => true, "edges.node", localMap)
            .Build();

        return result.Verify();
    }

    [Fact]
    public Task EdgeCase_SequentialPreserveFromExpression_AccumulatesFields()
    {
        var query = CreateSimpleQuery();
        var localMap = new Dictionary<string, string[]>
        {
            { "user", new[] { "PreservedQuery" } }
        };

        // Multiple calls should accumulate, not override
        var result = PreservationBuilder.Create(query)
            .PreserveFromExpression((TestUser u) => u.id, "edges.node", localMap)
            .PreserveFromExpression((TestUser u) => u.email, "edges.node", localMap)
            .Build();

        return result.Verify();
    }

    [Fact]
    public Task EdgeCase_SameFieldNameDifferentContexts_PreservesBoth()
    {
        var firstQuery = QueryBuilder
            .CreateDefaultBuilder("PlayerQuery")
            .AddField("PlayerQuery:data.player.Name:name")
            .AddField("data.player.Score:score");

        var secondQuery = QueryBuilder
            .CreateDefaultBuilder("TeamQuery")
            .AddField("TeamQuery:data.team.Name:name")
            .AddField("data.team.Rating:rating");

        var mergedQuery = QueryBuilder
            .CreateDefaultBuilder("Query", MergingStrategy.MergeByFieldPath)
            .Include(firstQuery)
            .Include(secondQuery);

        var localMap = new Dictionary<string, string[]>
        {
            { "player", new[] { "PlayerQuery" } },
            { "team", new[] { "TeamQuery" } }
        };

        // Both have "Name" field in different contexts
        var result = PreservationBuilder.Create(mergedQuery)
            .PreserveFromExpression(
                (TestUser player, TestUser team) =>
                    player.id != null && team.id != null,
                "data", localMap)
            .Build();

        return result.Verify();
    }

    [Fact]
    public Task EdgeCase_MixedPreservationMethods_AllWorkTogether()
    {
        var query = CreatePreservedQuery();

        var localMap = new Dictionary<string, string[]>
        {
            { "player", new[] { "PreservedQuery" } }
        };

        // Mix direct paths, PreserveAtPath, and PreserveFromExpression
        var result = PreservationBuilder.Create(query)
            .Preserve("PreservedQuery.data.edges.node.UserId")
            .PreserveAtPath("RegData", "edges.node")
            .PreserveFromExpression((TestPlayerProfile player) => player.UserId != null, "edges.node", localMap)
            .Build();

        return result.Verify();
    }

    [Fact]
    public Task EdgeCase_AlwaysPreserveFields_EmptyArray()
    {
        var mergedQuery = CreateMergedQuery();
        var localMap = new Dictionary<string, string[]>
        {
            { "playerFirstDeposit", new[] { "PreservedQuery" } }
        };

        // Empty alwaysPreserveFields array
        var result = PreservationBuilder.Create(mergedQuery)
            .PreserveFromExpression(
                (TestDeposit playerFirstDeposit) =>
                    playerFirstDeposit.Deposit.FirstDepositTime != null,
                "edges.node", localMap, Array.Empty<string>())
            .Build();

        return result.Verify();
    }

    [Fact]
    public Task EdgeCase_AlwaysPreserveFields_MultipleFields()
    {
        var mergedQuery = CreateMergedQuery();
        var localMap = new Dictionary<string, string[]>
        {
            { "playerFirstDeposit", new[] { "PreservedQuery" } }
        };

        // Multiple alwaysPreserveFields
        var result = PreservationBuilder.Create(mergedQuery)
            .PreserveFromExpression(
                (TestDeposit playerFirstDeposit) =>
                    playerFirstDeposit.Deposit.FirstDepositTime != null,
                "edges.node", localMap, "UserId", "ProfileData")
            .Build();

        return result.Verify();
    }

    [Fact]
    public Task EdgeCase_VeryDeepNesting_FiveLevels()
    {
        var query = QueryBuilder
            .CreateDefaultBuilder("PreservedQuery")
            .AddField("PreservedQuery:data.edges.node.Profile:profile.Settings:settings.Preferences:preferences.Display:display.Theme:theme.Color:color");

        var result = PreservationBuilder.Create(query)
            .Preserve("PreservedQuery.data.edges.node.profile.settings.preferences.display.theme")
            .Build();

        return result.Verify();
    }

    [Fact]
    public Task EdgeCase_ThreeParametersDifferentRoots_AllPreserved()
    {
        var depositQuery = QueryBuilder
            .CreateDefaultBuilder("DepositQuery")
            .AddField("DepositQuery:data.deposits.FirstDepositTime:firstDepositTime");

        var profileQuery = QueryBuilder
            .CreateDefaultBuilder("ProfileQuery")
            .AddField("ProfileQuery:data.profiles.Currency:currency");

        var statsQuery = QueryBuilder
            .CreateDefaultBuilder("StatsQuery")
            .AddField("StatsQuery:data.stats.TotalBets:totalBets");

        var mergedQuery = QueryBuilder
            .CreateDefaultBuilder("Query", MergingStrategy.MergeByFieldPath)
            .Include(depositQuery)
            .Include(profileQuery)
            .Include(statsQuery);

        var localMap = new Dictionary<string, string[]>
        {
            { "deposit", new[] { "DepositQuery" } },
            { "profile", new[] { "ProfileQuery" } },
            { "stats", new[] { "StatsQuery" } }
        };

        var result = PreservationBuilder.Create(mergedQuery)
            .PreserveFromExpression(
                (TestDeposit deposit, TestProfile profile, TestUser stats) =>
                    deposit.Deposit.FirstDepositTime != null &&
                    profile.firstName != null &&
                    stats.id != null,
                "data", localMap)
            .Build();

        return result.Verify();
    }

    // ===== NEVERMERGE CORNER CASE TESTS =====

    [Fact]
    public Task NeverMerge_QueriesStaySeparate_PreservesAtTopLevel()
    {
        var firstQuery = QueryBuilder
            .CreateDefaultBuilder("PreservedQuery")
            .WithMergingStrategy(MergingStrategy.NeverMerge)
            .AddField("PreservedQuery:data.edges.node.UserId:userId")
            .AddField("data.edges.node.Deposit:deposit.FirstDepositTime:firstDepositTime");

        var secondQuery = QueryBuilder
            .CreateDefaultBuilder("PreservedQuery")
            .WithMergingStrategy(MergingStrategy.NeverMerge)
            .AddField("PreservedQuery:data.edges.node.UserId:userId")
            .AddField("data.edges.node.Deposit:deposit.SecondDepositTime:secondDepositTime");

        var mergedQuery = QueryBuilder
            .CreateDefaultBuilder("PreservedQuery", MergingStrategy.MergeByFieldPath)
            .Include(firstQuery)
            .Include(secondQuery);

        // With NeverMerge, queries stay at separate root levels (PreservedQuery, PreservedQuery_1)
        // We preserve at parent level to keep both separate query trees
        var result = PreservationBuilder.Create(mergedQuery)
            .Preserve("PreservedQuery.data")
            .Preserve("PreservedQuery_1.data")
            .Build();

        return result.Verify();
    }

    [Fact]
    public Task NeverMerge_SingleQuery_WithPreservation()
    {
        var query = QueryBuilder
            .CreateDefaultBuilder("PreservedQuery")
            .WithMergingStrategy(MergingStrategy.NeverMerge)
            .AddField("PreservedQuery:data.edges.node.UserId:userId")
            .AddField("data.edges.node.Deposit:deposit.FirstDepositTime:firstDepositTime")
            .AddField("data.edges.node.Deposit:deposit.SecondDepositTime:secondDepositTime");

        // Preserve at the top level for a single NeverMerge query
        var result = PreservationBuilder.Create(query)
            .Preserve("PreservedQuery.data")
            .Build();

        return result.Verify();
    }

    [Fact]
    public Task NeverMerge_MixedWithMergeByFieldPath_BothStrategiesWork()
    {
        var firstQuery = QueryBuilder
            .CreateDefaultBuilder("PreservedQuery")
            .WithMergingStrategy(MergingStrategy.NeverMerge)
            .AddField("PreservedQuery:data.edges.node.UserId:userId")
            .AddField("data.edges.node.Deposit:deposit.FirstDepositTime:firstDepositTime");

        var secondQuery = QueryBuilder
            .CreateDefaultBuilder("PreservedQuery")
            .WithMergingStrategy(MergingStrategy.NeverMerge)
            .AddField("PreservedQuery:data.edges.node.UserId:userId")
            .AddField("data.edges.node.RegData:regData.Region:region");

        var mergedQuery = QueryBuilder
            .CreateDefaultBuilder("PreservedQuery", MergingStrategy.MergeByFieldPath)
            .Include(firstQuery)
            .Include(secondQuery);

        // Preserve both separate query trees created by NeverMerge
        var result = PreservationBuilder.Create(mergedQuery)
            .Preserve("PreservedQuery.data")
            .Preserve("PreservedQuery_1.data")
            .Build();

        return result.Verify();
    }

    // ===== ORIGINAL ISSUE REGRESSION TEST =====
    // This test recreates the scenario from 01_task.txt to ensure the type drift bug is fixed

    [Fact]
    public Task OriginalIssue_FourQueriesMerged_MultiParameterLambda_AllFieldsPreserved()
    {
        // Simulate the original issue scenario with 4 queries that merge:
        // PlayerFirstDepositBatchQuery, PlayerLastDepositBatchQuery,
        // PlayerSecondDepositBatchQuery, PlayerProfileBatchQuery

        var firstDepositQuery = QueryBuilder
            .CreateDefaultBuilder("FirstDepositQuery")
            .WithMergingStrategy(MergingStrategy.NeverMerge)
            .AddField("FirstDepositQuery:businessObjects.playerProfile.edges.node.PlayerId:playerId")
            .AddField("businessObjects.playerProfile.edges.node.Metrics:metrics.RealTime:realTime.Deposit:deposit.FirstDepositTime:firstDepositTime");

        var lastDepositQuery = QueryBuilder
            .CreateDefaultBuilder("LastDepositQuery")
            .WithMergingStrategy(MergingStrategy.NeverMerge)
            .AddField("LastDepositQuery:businessObjects.playerProfile.edges.node.PlayerId:playerId")
            .AddField("businessObjects.playerProfile.edges.node.Metrics:metrics.RealTime:realTime.Deposit:deposit.LastDepositTime:lastDepositTime");

        var secondDepositQuery = QueryBuilder
            .CreateDefaultBuilder("SecondDepositQuery")
            .WithMergingStrategy(MergingStrategy.NeverMerge)
            .AddField("SecondDepositQuery:businessObjects.playerProfile.edges.node.PlayerId:playerId")
            .AddField("businessObjects.playerProfile.edges.node.Metrics:metrics.RealTime:realTime.Deposit:deposit.SecondDepositTime:secondDepositTime");

        var profileQuery = QueryBuilder
            .CreateDefaultBuilder("ProfileQuery")
            .WithMergingStrategy(MergingStrategy.NeverMerge)
            .AddField("ProfileQuery:businessObjects.playerProfile.edges.node.PlayerId:playerId")
            .AddField("businessObjects.playerProfile.edges.node.ProfileData:profileData.Currency:currency");

        // Merge all queries
        var mergedQuery = QueryBuilder
            .CreateDefaultBuilder("PreservedQuery", MergingStrategy.MergeByFieldPath)
            .Include(firstDepositQuery)
            .Include(lastDepositQuery)
            .Include(secondDepositQuery)
            .Include(profileQuery);

        // Create localMap for 4-parameter lambda
        var localMap = new Dictionary<string, string[]>
        {
            { "playerFirstDeposit", new[] { "FirstDepositQuery" } },
            { "playerLastDeposit", new[] { "LastDepositQuery" } },
            { "playerSecondDeposit", new[] { "SecondDepositQuery" } },
            { "playerProfile", new[] { "ProfileQuery" } }
        };

        // Apply preservation with multi-parameter lambda
        // Before fix: only fields from first parameter were preserved
        // After fix: fields from ALL parameters should be preserved
        var result = PreservationBuilder.Create(mergedQuery)
            .PreserveFromExpression(
                (TestDepositDetails playerFirstDeposit, TestDepositDetails playerLastDeposit,
                 TestDepositDetails playerSecondDeposit, TestPlayerProfile playerProfile) =>
                    playerFirstDeposit.FirstDepositTime != null &&
                    playerLastDeposit.LastDepositTime != null &&
                    playerSecondDeposit.SecondDepositTime != null &&
                    playerProfile.ProfileData!.Currency != null,
                "businessObjects.playerProfile.edges.node",
                localMap,
                "PlayerId")
            .Build();

        return result.Verify();
    }

    // ===== NAVIGATION PROPERTY TESTS =====

    [Fact]
    public Task NavigationProperty_Simple_ResolvesToActualField()
    {
        // Simple test: navigation property at same level as query field
        var query = QueryBuilder
            .CreateDefaultBuilder("PreservedQuery")
            .AddField("PreservedQuery:data.user.FirstDepositTime:firstDepositTime");

        var localMap = new Dictionary<string, string[]>
        {
            { "deposit", new[] { "PreservedQuery" } }
        };

        // Lambda references .Date which is a navigation property => FirstDepositTime
        var result = PreservationBuilder.Create(query)
            .PreserveFromExpression(
                (FirstDepositDetails deposit) => deposit.Date != null,
                "data.user",
                localMap)
            .Build();

        return result.Verify();
    }

    // ===== ISSUE 04: Navigation Property Pattern =====
    // Lambda references .Date navigation property which maps to different fields per parameter

    [Fact]
    public Task Issue04_NavigationProperty_DateMapsToSpecificDepositTimes()
    {
        // This reproduces the exact scenario from 04_issue where:
        // - Lambda references playerX.Date (navigation property)
        // - playerFirstDeposit.Date => FirstDepositTime
        // - playerSecondDeposit.Date => SecondDepositTime
        // - playerLastDeposit.Date => LastDepositTime
        // - Two parameters map to the same merged root

        var firstDepositQuery = QueryBuilder
            .CreateDefaultBuilder("PlayerFirstDepositBatchQuery")
            .AddField("PlayerFirstDepositBatchQuery:businessObjects.playerProfile.edges.node.PlayerId:playerId")
            .AddField("businessObjects.playerProfile.edges.node.Metrics:metrics.RealTime:realTime.Deposit:deposit.FirstDepositTime:firstDepositTime");

        var secondDepositQuery = QueryBuilder
            .CreateDefaultBuilder("PlayerSecondDepositBatchQuery")
            .AddField("PlayerSecondDepositBatchQuery:businessObjects.playerProfile.edges.node.PlayerId:playerId")
            .AddField("businessObjects.playerProfile.edges.node.Metrics:metrics.RealTime:realTime.Deposit:deposit.SecondDepositTime:secondDepositTime");

        var lastDepositQuery = QueryBuilder
            .CreateDefaultBuilder("PlayerLastDepositBatchQuery")
            .AddField("PlayerLastDepositBatchQuery:businessObjects.playerProfile.edges.node.PlayerId:playerId")
            .AddField("businessObjects.playerProfile.edges.node.Metrics:metrics.RealTime:realTime.Deposit:deposit.LastDepositTime:lastDepositTime");

        // Merge with MergeByFieldPath - First and Second will merge together
        var mergedQuery = QueryBuilder
            .CreateDefaultBuilder("DataApiSourceBatchQuery", MergingStrategy.MergeByFieldPath)
            .Include(firstDepositQuery)
            .Include(secondDepositQuery)
            .Include(lastDepositQuery);

        // LocalMap reflects the merge: All three queries merge into the same root
        var localMap = new Dictionary<string, string[]>
        {
            { "playerFirstDeposit", new[] { "PlayerFirstDepositBatchQuery", "playerProfile" } },
            { "playerSecondDeposit", new[] { "PlayerFirstDepositBatchQuery", "playerProfile" } },
            { "playerLastDeposit", new[] { "PlayerFirstDepositBatchQuery", "playerProfile" } } // All map to same merged root!
        };

        // Lambda references .Date navigation property
        // The expression extractor will extract "Date" from all three parameters
        //
        // IMPORTANT: Real code uses GetPathTo which returns path TO "edges" but not including it
        // So localMap has ["PlayerFirstDepositBatchQuery", "playerProfile"]
        // And nodePath is "edges.node" (not the full path to Deposit)
        var result = PreservationBuilder.Create(mergedQuery)
            .PreserveFromExpression(
                (FirstDepositDetails playerFirstDeposit, SecondDepositDetails playerSecondDeposit, LastDepositDetails playerLastDeposit) =>
                    playerFirstDeposit.Date != null &&
                    playerSecondDeposit.Date != null &&
                    playerLastDeposit.Date != null,
                "edges.node", // This is what the real code uses, not the full path!
                localMap,
                "PlayerId")
            .Build();

        return result.Verify();
    }
}
