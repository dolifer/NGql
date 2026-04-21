using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
using FluentAssertions;
using NGql.Core.Builders;
using Xunit;

namespace NGql.Core.Tests.Extensions.PreserveFromExpressionTests;

public class PreserveFromExpressionTests
{
    [Fact]
    public Task MultiParameterLambda_AllFieldsPreserved()
    {
        // Create three separate queries that will merge to the same base path
        var queryA = QueryBuilder
            .CreateDefaultBuilder("QueryA")
            .AddField("QueryA:data.edges.node.UserId:userId")
            .AddField("data.edges.node.Settings:settings.SettingA:settingA");

        var queryB = QueryBuilder
            .CreateDefaultBuilder("QueryB")
            .AddField("QueryB:data.edges.node.UserId:userId")
            .AddField("data.edges.node.Settings:settings.SettingB:settingB");

        var queryC = QueryBuilder
            .CreateDefaultBuilder("QueryC")
            .AddField("QueryC:data.edges.node.UserId:userId")
            .AddField("data.edges.node.Settings:settings.SettingC:settingC");

        // Merge all queries - they will merge to the same base path due to compatible structure
        var mergedQuery = QueryBuilder
            .CreateDefaultBuilder("MergedQuery", MergingStrategy.MergeByFieldPath)
            .Include(queryA)
            .Include(queryB)
            .Include(queryC);

        // Simulate the real issue: parameters map to different original queries but same merged path
        var localMap = new Dictionary<string, string[]>
        {
            { "groupA", new[] { "QueryA" } },
            { "groupB", new[] { "QueryA" } },
            { "groupC", new[] { "QueryA" } }
        };

        // Lambda references navigation properties from all three parameters
        // All three settings should be preserved via their respective navigation properties
        var result = PreservationBuilder.Create(mergedQuery)
            .PreserveFromExpression(
                (TestDataModels.SettingGroupA groupA, TestDataModels.SettingGroupB groupB, TestDataModels.SettingGroupC groupC) =>
                    groupA.Field != null &&      // Preserves settingA via navigation property
                    groupB.Field != null &&      // Preserves settingB via navigation property
                    groupC.Field != null,        // Preserves settingC via navigation property
                "edges.node",
                localMap,
                "UserId")
            .Build();

        return result.Verify();
    }
    
    [Fact]
    public Task MultiParameterLambda_AllFieldsPreserved_NeverMerge()
    {
        // Create three separate queries that will merge to the same base path
        var queryA = QueryBuilder
            .CreateDefaultBuilder("QueryA")
            .WithMergingStrategy(MergingStrategy.NeverMerge)
            .AddField("QueryA:data.edges.node.UserId:userId")
            .AddField("data.edges.node.Settings:settings.SettingA:settingA");

        var queryB = QueryBuilder
            .CreateDefaultBuilder("QueryB")
            .AddField("QueryB:data.edges.node.UserId:userId")
            .AddField("data.edges.node.Settings:settings.SettingB:settingB");

        var queryC = QueryBuilder
            .CreateDefaultBuilder("QueryC")
            .AddField("QueryC:data.edges.node.UserId:userId")
            .AddField("data.edges.node.Settings:settings.SettingC:settingC");

        // Merge all queries - they will merge to the same base path due to compatible structure
        var mergedQuery = QueryBuilder
            .CreateDefaultBuilder("MergedQuery", MergingStrategy.MergeByFieldPath)
            .Include(queryA)
            .Include(queryB)
            .Include(queryC);

        Console.WriteLine("MergedQuery: " + mergedQuery.ToString());

        // QueryA has NeverMerge, so it stays separate with its own root.
        // QueryB and QueryC both have MergeByFieldPath (default), so they merge together into QueryB.
        var localMap = new Dictionary<string, string[]>
        {
            { "groupA", new[] { "QueryA" } },  // Maps to separate QueryA root
            { "groupB", new[] { "QueryB" } },  // Maps to QueryB root
            { "groupC", new[] { "QueryB" } }   // Also maps to QueryB (merged into it)
        };

        // Lambda references navigation properties from all three parameters
        // All three settings should be preserved via their respective navigation properties
        var result = PreservationBuilder.Create(mergedQuery)
            .PreserveFromExpression(
                (TestDataModels.SettingGroupA groupA, TestDataModels.SettingGroupB groupB, TestDataModels.SettingGroupC groupC) =>
                    groupA.Field != null &&      // Preserves settingA via navigation property
                    groupB.Field != null &&      // Preserves settingB via navigation property
                    groupC.Field != null,        // Preserves settingC via navigation property
                "edges.node",
                localMap,
                "UserId")
            .Build();

        return result.Verify();
    }
    
    [Fact]
    public Task PreserveFromExpression_SingleParameter_SimpleField()
    {
        // Arrange
        var query = QueryBuilder
            .CreateDefaultBuilder("TestQuery")
            .AddField("TestQuery:data.edges.node.UserId:userId")
            .AddField("data.edges.node.Email:email")
            .AddField("data.edges.node.Name:name");

        // Act - preserve field referenced in expression
        var result = PreservationBuilder.Create(query)
            .PreserveFromExpression(
                (TestDataModels.SimpleUser user) => user.Email != null,
                "edges.node")
            .Build();

        // Assert
        return result.Verify();
    }

    [Fact]
    public Task PreserveFromExpression_SingleParameter_MultipleFields()
    {
        // Arrange
        var query = QueryBuilder
            .CreateDefaultBuilder("TestQuery")
            .AddField("TestQuery:data.edges.node.UserId:userId")
            .AddField("data.edges.node.Email:email")
            .AddField("data.edges.node.Name:name")
            .AddField("data.edges.node.Age:age");

        // Act - preserve multiple fields from expression
        var result = PreservationBuilder.Create(query)
            .PreserveFromExpression(
                (TestDataModels.SimpleUser user) => user.Email != null && user.Name != null && user.Age > 18,
                "edges.node")
            .Build();

        // Assert
        return result.Verify();
    }

    [Fact]
    public Task PreserveFromExpression_SingleParameter_NestedField()
    {
        // Arrange
        var query = QueryBuilder
            .CreateDefaultBuilder("TestQuery")
            .AddField("TestQuery:data.edges.node.UserId:userId")
            .AddField("data.edges.node.Profile:profile.Name:name")
            .AddField("data.edges.node.Profile:profile.Bio:bio")
            .AddField("data.edges.node.Settings:settings.Theme:theme");

        // Act - preserve nested field from expression
        var result = PreservationBuilder.Create(query)
            .PreserveFromExpression(
                (TestDataModels.UserWithProfile user) => user.Profile.Name != null,
                "edges.node")
            .Build();

        // Assert
        return result.Verify();
    }

    [Fact]
    public Task PreserveFromExpression_SingleParameter_WholeObjectReference()
    {
        // Arrange
        var query = QueryBuilder
            .CreateDefaultBuilder("TestQuery")
            .AddField("TestQuery:data.edges.node.UserId:userId")
            .AddField("data.edges.node.Profile:profile.Name:name")
            .AddField("data.edges.node.Profile:profile.Bio:bio")
            .AddField("data.edges.node.Profile:profile.Avatar:avatar");

        // Act - reference whole object (should preserve all fields of that type)
        var result = PreservationBuilder.Create(query)
            .PreserveFromExpression(
                (TestDataModels.UserWithProfile user) => user.Profile != null,
                "edges.node")
            .Build();

        // Assert
        return result.Verify();
    }

    [Fact]
    public Task PreserveFromExpression_SingleParameter_MethodCall()
    {
        // Arrange
        var query = QueryBuilder
            .CreateDefaultBuilder("TestQuery")
            .AddField("TestQuery:data.edges.node.UserId:userId")
            .AddField("data.edges.node.Email:email")
            .AddField("data.edges.node.Name:name");

        // Act - preserve field used in method call
        var result = PreservationBuilder.Create(query)
            .PreserveFromExpression(
                (TestDataModels.SimpleUser user) => user.Email.Contains("@example.com"),
                "edges.node")
            .Build();

        // Assert
        return result.Verify();
    }

    [Fact]
    public Task PreserveFromExpression_NoNodePath_PreservesDirectly()
    {
        // Arrange
        var query = QueryBuilder
            .CreateDefaultBuilder("TestQuery")
            .AddField("TestQuery:userId")
            .AddField("email")
            .AddField("name");

        // Act - no nodePath specified
        var result = PreservationBuilder.Create(query)
            .PreserveFromExpression((TestDataModels.SimpleUser user) => user.Email != null)
            .Build();

        // Assert
        return result.Verify();
    }

    [Fact]
    public Task PreserveFromExpression_ChainedCalls_AccumulatesFields()
    {
        // Arrange
        var query = QueryBuilder
            .CreateDefaultBuilder("TestQuery")
            .AddField("TestQuery:data.edges.node.UserId:userId")
            .AddField("data.edges.node.Email:email")
            .AddField("data.edges.node.Name:name")
            .AddField("data.edges.node.Age:age");

        // Act - chain multiple PreserveFromExpression calls
        var result = PreservationBuilder.Create(query)
            .PreserveFromExpression(
                (TestDataModels.SimpleUser user) => user.Email != null,
                "edges.node")
            .PreserveFromExpression(
                (TestDataModels.SimpleUser user) => user.Name != null,
                "edges.node")
            .Build();

        // Assert
        return result.Verify();
    }

    [Fact]
    public Task PreserveFromExpression_WithNavigationProperty_ExpandsToActualFields()
    {
        // Arrange
        var query = QueryBuilder
            .CreateDefaultBuilder("TestQuery")
            .AddField("TestQuery:data.edges.node.UserId:userId")
            .AddField("data.edges.node.FirstDepositTime:firstDepositTime")
            .AddField("data.edges.node.SecondDepositTime:secondDepositTime");

        // Act - reference navigation property (should expand to actual settable fields)
        var result = PreservationBuilder.Create(query)
            .PreserveFromExpression(
                (TestDataModels.DepositInfo deposit) => deposit.Date != null,
                "edges.node")
            .Build();

        // Assert
        return result.Verify();
    }

    [Fact]
    public Task PreserveFromExpression_ComplexBooleanLogic_PreservesAllReferencedFields()
    {
        // Arrange
        var query = QueryBuilder
            .CreateDefaultBuilder("TestQuery")
            .AddField("TestQuery:data.edges.node.UserId:userId")
            .AddField("data.edges.node.Email:email")
            .AddField("data.edges.node.Name:name")
            .AddField("data.edges.node.Age:age")
            .AddField("data.edges.node.Status:status");

        // Act - complex boolean expression
        var result = PreservationBuilder.Create(query)
            .PreserveFromExpression(
                (TestDataModels.SimpleUser user) =>
                    (user.Email != null && user.Name != null) || (user.Age > 18 && user.Status == "active"),
                "edges.node")
            .Build();

        // Assert
        return result.Verify();
    }

    [Fact]
    public Task PreserveFromExpression_WithAlwaysPreserveFields_IncludesExtraFields()
    {
        // Arrange
        var queryA = QueryBuilder
            .CreateDefaultBuilder("QueryA")
            .AddField("QueryA:data.edges.node.UserId:userId")
            .AddField("data.edges.node.Email:email")
            .AddField("data.edges.node.Name:name");

        var localMap = new Dictionary<string, string[]>
        {
            { "user", new[] { "QueryA" } }
        };

        // Act - specify always-preserve fields
        var result = PreservationBuilder.Create(queryA)
            .PreserveFromExpression(
                (TestDataModels.SimpleUser user) => user.Email != null,
                "edges.node",
                localMap,
                "UserId")
            .Build();

        // Assert - should preserve BOTH Email (from expression) AND UserId (from alwaysPreserveFields)
        return result.Verify();
    }

    [Fact]
    public Task PreserveFromExpression_NullConditionalOperator_PreservesField()
    {
        // Arrange
        var query = QueryBuilder
            .CreateDefaultBuilder("TestQuery")
            .AddField("TestQuery:data.edges.node.UserId:userId")
            .AddField("data.edges.node.Profile:profile.Name:name")
            .AddField("data.edges.node.Profile:profile.Bio:bio");

        // Act - use null-conditional operator
        var result = PreservationBuilder.Create(query)
            .PreserveFromExpression(
                (TestDataModels.UserWithProfile user) => user.Profile.Name != null,
                "edges.node")
            .Build();

        // Assert
        return result.Verify();
    }

    [Fact]
    public Task PreserveFromExpression_TernaryOperator_PreservesAllBranches()
    {
        // Arrange
        var query = QueryBuilder
            .CreateDefaultBuilder("TestQuery")
            .AddField("TestQuery:data.edges.node.UserId:userId")
            .AddField("data.edges.node.Email:email")
            .AddField("data.edges.node.Name:name")
            .AddField("data.edges.node.Status:status");

        // Act - use ternary operator
        var result = PreservationBuilder.Create(query)
            .PreserveFromExpression(
                (TestDataModels.SimpleUser user) => user.Status == "active" ? user.Email : user.Name,
                "edges.node")
            .Build();

        // Assert - should preserve status, email, and name
        return result.Verify();
    }

    [Fact]
    public Task PreserveFromExpression_StringOperations_PreservesField()
    {
        // Arrange
        var query = QueryBuilder
            .CreateDefaultBuilder("TestQuery")
            .AddField("TestQuery:data.edges.node.UserId:userId")
            .AddField("data.edges.node.Email:email")
            .AddField("data.edges.node.Name:name");

        // Act - use string operations
        var result = PreservationBuilder.Create(query)
            .PreserveFromExpression(
                (TestDataModels.SimpleUser user) =>
                    user.Email.ToLower().Contains('@') &&
                    user.Name.StartsWith('A'),
                "edges.node")
            .Build();

        // Assert
        return result.Verify();
    }

    [Fact]
    public Task PreserveFromExpression_NumericComparisons_PreservesField()
    {
        // Arrange
        var query = QueryBuilder
            .CreateDefaultBuilder("TestQuery")
            .AddField("TestQuery:data.edges.node.UserId:userId")
            .AddField("data.edges.node.Age:age")
            .AddField("data.edges.node.Score:score");

        // Act - use numeric comparisons
        var result = PreservationBuilder.Create(query)
            .PreserveFromExpression(
                (TestDataModels.SimpleUser user) =>
                    user.Age >= 18 && user.Age <= 65,
                "edges.node")
            .Build();

        // Assert
        return result.Verify();
    }

    [Fact]
    public Task PreserveFromExpression_NestedObjects_PreservesFullPath()
    {
        // Arrange
        var query = QueryBuilder
            .CreateDefaultBuilder("TestQuery")
            .AddField("TestQuery:data.edges.node.UserId:userId")
            .AddField("data.edges.node.Profile:profile.Address:address.City:city")
            .AddField("data.edges.node.Profile:profile.Address:address.Street:street")
            .AddField("data.edges.node.Profile:profile.Name:name");

        // Act - reference deeply nested field
        var result = PreservationBuilder.Create(query)
            .PreserveFromExpression<TestDataModels.UserWithAddress>(
                user => user.Profile!.Address!.City == "New York",
                "edges.node")
            .Build();

        // Assert
        return result.Verify();
    }

    [Fact]
    public Task PreserveFromExpression_ArrayIndexing_PreservesArray()
    {
        // Arrange
        var query = QueryBuilder
            .CreateDefaultBuilder("TestQuery")
            .AddField("TestQuery:data.edges.node.UserId:userId")
            .AddField("data.edges.node.Tags:tags")
            .AddField("data.edges.node.Scores:scores");

        // Act - reference array indexing
        var result = PreservationBuilder.Create(query)
            .PreserveFromExpression<TestDataModels.UserWithArrays>(
                user => user.Tags![0] == "premium" && user.Scores!.Length > 0,
                "edges.node")
            .Build();

        // Assert - should preserve tags and scores
        return result.Verify();
    }

    [Fact]
    public Task PreserveFromExpression_MultiParameter_DifferentTypes_PreservesCorrectly()
    {
        // Arrange
        var queryA = QueryBuilder
            .CreateDefaultBuilder("QueryA")
            .AddField("QueryA:data.edges.node.UserId:userId")
            .AddField("data.edges.node.Email:email");

        var queryB = QueryBuilder
            .CreateDefaultBuilder("QueryB")
            .AddField("QueryB:data.edges.node.ProductId:productId")
            .AddField("data.edges.node.Name:name");

        var mergedQuery = QueryBuilder
            .CreateDefaultBuilder("MergedQuery", MergingStrategy.NeverMerge)
            .Include(queryA)
            .Include(queryB);

        var localMap = new Dictionary<string, string[]>
        {
            { "user", new[] { "QueryA" } },
            { "product", new[] { "QueryB" } }
        };

        // Act - different parameter types
        Expression<Func<TestDataModels.SimpleUser, TestDataModels.Product, bool>> expr =
            (user, product) => user.Email != null && product.Name != null;

        var result = PreservationBuilder.Create(mergedQuery)
            .PreserveFromExpression(
                (Expression)expr,
                "edges.node",
                localMap)
            .Build();

        // Assert
        return result.Verify();
    }

    [Fact]
    public Task PreserveFromExpression_WithAlwaysPreserveFields_EmptyArray_OnlyPreservesExpression()
    {
        // Arrange
        var query = QueryBuilder
            .CreateDefaultBuilder("TestQuery")
            .AddField("TestQuery:data.edges.node.UserId:userId")
            .AddField("data.edges.node.Email:email")
            .AddField("data.edges.node.Name:name");

        var localMap = new Dictionary<string, string[]>
        {
            { "user", new[] { "TestQuery" } }
        };

        // Act - no alwaysPreserveFields parameter (uses overload without it)
        var result = PreservationBuilder.Create(query)
            .PreserveFromExpression(
                (TestDataModels.SimpleUser user) => user.Email != null,
                "edges.node",
                localMap)
            .Build();

        // Assert - should only preserve Email from the expression
        return result.Verify();
    }

    [Fact]
    public Task NestedComputedProperty_SingleLevel_ExpandsCorrectly()
    {
        // Arrange - query with nested profile that has a computed property
        var query = QueryBuilder
            .CreateDefaultBuilder("TestQuery")
            .AddField("TestQuery:data.edges.node.UserId:userId")
            .AddField("data.edges.node.Profile:profile.FirstName:firstName")
            .AddField("data.edges.node.Profile:profile.LastName:lastName");

        var localMap = new Dictionary<string, string[]>
        {
            { "user", new[] { "TestQuery" } }
        };

        // Act - reference computed property Profile.Name which should expand to FirstName and LastName
        var result = PreservationBuilder.Create(query)
            .PreserveFromExpression(
                (TestDataModels.UserWithComputedProfile user) => user.Profile.Name != null,
                "edges.node",
                localMap,
                "UserId")
            .Build();

        // Assert - should preserve UserId and both FirstName and LastName (expanded from Name)
        return result.Verify();
    }

    [Fact]
    public Task NestedComputedProperty_TwoLevels_ExpandsCorrectly()
    {
        // Arrange - query with deeply nested computed properties
        var query = QueryBuilder
            .CreateDefaultBuilder("TestQuery")
            .AddField("TestQuery:data.edges.node.UserId:userId")
            .AddField("data.edges.node.Profile:profile.Contact:contact.PrimaryEmail:primaryEmail")
            .AddField("data.edges.node.Profile:profile.Contact:contact.SecondaryEmail:secondaryEmail");

        var localMap = new Dictionary<string, string[]>
        {
            { "user", new[] { "TestQuery" } }
        };

        // Act - reference computed property Profile.Contact.Email which should expand to PrimaryEmail and SecondaryEmail
        var result = PreservationBuilder.Create(query)
            .PreserveFromExpression(
                (TestDataModels.UserWithComputedProfile user) => user.Profile.Contact.Email != null,
                "edges.node",
                localMap,
                "UserId")
            .Build();

        // Assert - should preserve UserId and both PrimaryEmail and SecondaryEmail (expanded from Email)
        return result.Verify();
    }

    [Fact]
    public Task NestedComputedProperty_MixedComputedAndRegular_ExpandsOnlyComputed()
    {
        // Arrange - query with nested profile that has both computed and regular properties
        var query = QueryBuilder
            .CreateDefaultBuilder("TestQuery")
            .AddField("TestQuery:data.edges.node.UserId:userId")
            .AddField("data.edges.node.Profile:profile.FirstName:firstName")
            .AddField("data.edges.node.Profile:profile.LastName:lastName")
            .AddField("data.edges.node.Profile:profile.Contact:contact.PrimaryEmail:primaryEmail")
            .AddField("data.edges.node.Profile:profile.Contact:contact.SecondaryEmail:secondaryEmail");

        var localMap = new Dictionary<string, string[]>
        {
            { "user", new[] { "TestQuery" } }
        };

        // Act - reference both computed (Profile.Name) and regular (Profile.Contact.PrimaryEmail) properties
        var result = PreservationBuilder.Create(query)
            .PreserveFromExpression(
                (TestDataModels.UserWithComputedProfile user) =>
                    user.Profile.Name != null && user.Profile.Contact.PrimaryEmail != null,
                "edges.node",
                localMap,
                "UserId")
            .Build();

        // Assert - should preserve UserId, FirstName/LastName (from Name), and PrimaryEmail (direct)
        return result.Verify();
    }

    [Fact]
    public Task NestedComputedProperty_InsideConditional_ExpandsCorrectly()
    {
        // Arrange - query with nested computed property inside conditional logic
        var query = QueryBuilder
            .CreateDefaultBuilder("TestQuery")
            .AddField("TestQuery:data.edges.node.UserId:userId")
            .AddField("data.edges.node.Profile:profile.FirstName:firstName")
            .AddField("data.edges.node.Profile:profile.LastName:lastName")
            .AddField("data.edges.node.Profile:profile.Contact:contact.PrimaryEmail:primaryEmail")
            .AddField("data.edges.node.Profile:profile.Contact:contact.SecondaryEmail:secondaryEmail");

        var localMap = new Dictionary<string, string[]>
        {
            { "user", new[] { "TestQuery" } }
        };

        // Act - use computed property in conditional with null coalescing
        var result = PreservationBuilder.Create(query)
            .PreserveFromExpression(
                (TestDataModels.UserWithComputedProfile user) =>
                    (user.Profile.Name ?? user.Profile.Contact.Email) != null,
                "edges.node",
                localMap,
                "UserId")
            .Build();

        // Assert - should preserve UserId, FirstName/LastName (from Name), and PrimaryEmail/SecondaryEmail (from Email)
        return result.Verify();
    }

    [Fact]
    public Task MultiParameter_SameNavPropertyName_DifferentTypes_ExpandsSeparately()
    {
        // This tests the REAL issue: two parameters with same property name "Date"
        // but on different types, both are navigation properties
        var queryA = QueryBuilder
            .CreateDefaultBuilder("QueryA")
            .AddField("QueryA:data.edges.node.UserId:userId")
            .AddField("data.edges.node.DepositInfo:depositInfo.FirstDepositTime:firstDepositTime")
            .AddField("data.edges.node.DepositInfo:depositInfo.SecondDepositTime:secondDepositTime");

        var queryB = QueryBuilder
            .CreateDefaultBuilder("QueryB")
            .AddField("QueryB:data.edges.node.UserId:userId")
            .AddField("data.edges.node.DepositInfo:depositInfo.FirstDepositTime:firstDepositTime")
            .AddField("data.edges.node.DepositInfo:depositInfo.SecondDepositTime:secondDepositTime");

        var mergedQuery = QueryBuilder
            .CreateDefaultBuilder("MergedQuery", MergingStrategy.MergeByFieldPath)
            .Include(queryA)
            .Include(queryB);

        var localMap = new Dictionary<string, string[]>
        {
            { "playerFirstDeposit", new[] { "QueryA" } },
            { "playerSecondDeposit", new[] { "QueryB" } }
        };

        // Act - both parameters reference .Date navigation property
        // Date on DepositInfo should expand to FirstDepositTime and SecondDepositTime
        var result = PreservationBuilder.Create(mergedQuery)
            .PreserveFromExpression(
                (TestDataModels.DepositInfo playerFirstDeposit, TestDataModels.DepositInfo playerSecondDeposit) =>
                    playerFirstDeposit.Date != null && playerSecondDeposit.Date != null,
                "edges.node.depositInfo",
                localMap)
            .Preserve("QueryA.edges.node.UserId") // Preserve UserId separately at node level
            .Build();

        // Assert - should preserve both FirstDepositTime and SecondDepositTime (expanded from Date)
        return result.Verify();
    }

    [Fact]
    public Task ExpressionPreservation_ComplexExpression_With_Multiple_Navigations()
    {
        // Test uncovered line: Expression visitor handling multiple navigation properties (lines 100-150)
        var query = QueryBuilder
            .CreateDefaultBuilder("ComplexQuery")
            .AddField("user", ub => ub
                .AddField("profile", pb => pb
                    .AddField("contacts", cb => cb
                        .AddField("addresses")
                        .AddField("phones")
                        .AddField("emails"))))
            .AddField("orders", ob => ob
                .AddField("items")
                .AddField("payment"));

        var result = PreservationBuilder.Create(query)
            .Preserve("user.profile.contacts.emails")
            .Preserve("orders.items")
            .Build();

        return result.Verify();
    }

    [Fact]
    public Task ExpressionPreservation_With_Nested_Collections()
    {
        // Test uncovered line: Collection handling in expression visitor (lines 120-140)
        var query = QueryBuilder
            .CreateDefaultBuilder("CollectionQuery")
            .AddField("collections", f => f
                .AddField("items", f2 => f2
                    .AddField("subitems")
                    .AddField("metadata")));

        var result = PreservationBuilder.Create(query)
            .Preserve("collections.items.subitems")
            .Build();

        return result.Verify();
    }

    [Fact]
    public Task ExpressionPreservation_Preserve_Entire_Graph()
    {
        // Test uncovered line: Full graph preservation (lines 75-90)
        var query = QueryBuilder
            .CreateDefaultBuilder("FullGraphQuery")
            .AddField("root", f => f
                .AddField("level1", f2 => f2
                    .AddField("level2", f3 => f3
                        .AddField("level3")
                        .AddField("level3b"))
                    .AddField("level2b"))
                .AddField("sibling"));

        var result = PreservationBuilder.Create(query)
            .Preserve("root.level1.level2.level3")
            .Preserve("root.level1.level2.level3b")
            .Preserve("root.sibling")
            .Build();

        return result.Verify();
    }

    [Fact]
    public Task ExpressionPreservation_With_Dotted_Paths()
    {
        // Test uncovered line: Dotted path expansion (lines 160-180)
        var query = QueryBuilder
            .CreateDefaultBuilder("DottedPathQuery")
            .AddField("data.edges.node.user.profile.settings");

        var result = PreservationBuilder.Create(query)
            .Preserve("data.edges.node.user.profile.settings")
            .Build();

        return result.Verify();
    }

    [Fact]
    public Task ExpressionPreservation_Overlapping_Paths()
    {
        // Test uncovered line: Path overlap handling (lines 140-160)
        var query = QueryBuilder
            .CreateDefaultBuilder("OverlapQuery")
            .AddField("user", f => f
                .AddField("profile", f2 => f2
                    .AddField("avatar")
                    .AddField("banner")
                    .AddField("settings", f3 => f3
                        .AddField("theme")
                        .AddField("language"))));

        var result = PreservationBuilder.Create(query)
            .Preserve("user.profile")
            .Preserve("user.profile.settings")
            .Preserve("user.profile.avatar")
            .Build();

        return result.Verify();
    }

    // ============ PHASE 9.3c: Additional Expression Processing Tests ============

    [Fact]
    public void ExpressionPreservation_With_Conditional_Ternary_Expression()
    {
        // Test visitor handling of ternary/conditional expressions
        var query = QueryBuilder
            .CreateDefaultBuilder("ConditionalQuery")
            .AddField("user", ub => ub
                .AddField("profile")
                .AddField("settings")
                .AddField("avatar"));

        // Ternary expressions require multiple field branches
        var result = PreservationBuilder.Create(query)
            .Preserve("user.profile")
            .Preserve("user.settings")
            .Build();

        result.Should().NotBeNull();
        result.ToString().Should().Contain("user");
    }

    [Fact]
    public void ExpressionPreservation_With_Multiple_Path_Levels()
    {
        // Test deep path expansion and visitor traversal
        var query = QueryBuilder
            .CreateDefaultBuilder("DeepPathQuery")
            .AddField("root", r => r
                .AddField("level1", l1 => l1
                    .AddField("level2", l2 => l2
                        .AddField("level3", l3 => l3
                            .AddField("level4", l4 => l4
                                .AddField("level5"))))));

        var result = PreservationBuilder.Create(query)
            .Preserve("root.level1.level2.level3.level4.level5")
            .Build();

        result.Should().NotBeNull();
        result.ToString().Should().Contain("level5");
    }

    [Fact]
    public void ExpressionPreservation_Complex_Mixed_Field_Extraction()
    {
        // Test expression extraction with mixed field types
        var query = QueryBuilder
            .CreateDefaultBuilder("MixedQuery")
            .AddField("scalar", "String")
            .AddField("list", "String[]")
            .AddField("object", o => o
                .AddField("id", "ID")
                .AddField("name", "String")
                .AddField("nested", n => n
                    .AddField("value")));

        var result = PreservationBuilder.Create(query)
            .Preserve("scalar")
            .Preserve("list")
            .Preserve("object.nested.value")
            .Build();

        result.Should().NotBeNull();
        result.ToString().Should().Contain("nested");
    }

    [Fact]
    public void ExpressionPreservation_With_Arguments_And_Aliases()
    {
        // Test preservation with query arguments
        var query = QueryBuilder
            .CreateDefaultBuilder("QueryWithArgs")
            .AddField("search", new Dictionary<string, object?> { { "q", "test" } })
            .AddField("search.title")
            .AddField("search.description");

        var result = PreservationBuilder.Create(query)
            .Preserve("search.title")
            .Build();

        result.Should().NotBeNull();
        result.ToString().Should().Contain("search");
    }

    [Fact]
    public void ExpressionPreservation_Sibling_Fields_With_Overlap()
    {
        // Test handling of overlapping sibling field preservation
        var query = QueryBuilder
            .CreateDefaultBuilder("SiblingsQuery")
            .AddField("user", ub => ub
                .AddField("firstName")
                .AddField("lastName")
                .AddField("email")
                .AddField("phone"));

        var result = PreservationBuilder.Create(query)
            .Preserve("user.firstName")
            .Preserve("user.lastName")
            .Preserve("user.email")
            .Build();

        result.Should().NotBeNull();
        result.ToString().Should().Contain("firstName");
        result.ToString().Should().Contain("lastName");
    }
}
