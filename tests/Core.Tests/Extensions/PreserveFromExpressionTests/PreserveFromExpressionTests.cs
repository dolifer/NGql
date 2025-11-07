using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
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
                    user.Email.ToLower().Contains("@") &&
                    user.Name.StartsWith("A"),
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
                new[] { "UserId" })
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
                new[] { "UserId" })
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
                new[] { "UserId" })
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
                new[] { "UserId" })
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
    public Task MultiParameter_UnprefixedNavProperty_BothTypesExpanded()
    {
        // This tests the EXACT issue reported: expression extracts ["playerFirstDeposit", "playerSecondDeposit"]
        // WITHOUT the .Date suffix because Date property is in a different namespace (IL-generated)
        // and gets excluded by ShouldExcludeProperty
        // This triggers useNoPrefixMode = true
        var queryA = QueryBuilder
            .CreateDefaultBuilder("QueryA")
            .AddField("QueryA:data.edges.node.FirstDepositTime:firstDepositTime")
            .AddField("data.edges.node.SecondDepositTime:secondDepositTime");

        var queryB = QueryBuilder
            .CreateDefaultBuilder("QueryB")
            .AddField("QueryB:data.edges.node.FirstDepositTime:firstDepositTime")
            .AddField("data.edges.node.SecondDepositTime:secondDepositTime");

        var mergedQuery = QueryBuilder
            .CreateDefaultBuilder("MergedQuery", MergingStrategy.MergeByFieldPath)
            .Include(queryA)
            .Include(queryB);

        // Both parameters map to same merged node (same base path)
        var localMap = new Dictionary<string, string[]>
        {
            { "playerFirstDeposit", new[] { "QueryA" } },
            { "playerSecondDeposit", new[] { "QueryB" } }
        };

        // Act - Date property is defined in ExternalFramework.Generated namespace (different from parameter type)
        // ShouldExcludeProperty will exclude it, so extracted paths = ["playerFirstDeposit", "playerSecondDeposit"]
        // This triggers useNoPrefixMode = true (multiple params, no prefixes on extracted paths)
        var result = PreservationBuilder.Create(mergedQuery)
            .PreserveFromExpression(
                (ExternalFramework.Generated.PlayerDepositQuery playerFirstDeposit,
                 ExternalFramework.Generated.PlayerDepositQuery playerSecondDeposit) =>
                    playerFirstDeposit.Date != null && playerSecondDeposit.Date != null,
                "edges.node",
                localMap)
            .Build();

        // Assert - Even though Date is excluded from extraction, the greedy fallback should preserve all fields
        return result.Verify();
    }
}
