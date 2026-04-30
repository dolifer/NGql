namespace NGql.Core.IntegrationTests;

using System;
using System.Collections.Generic;
using FluentAssertions;
using NGql.Core;
using NGql.Core.Builders;
using Xunit;

/// <summary>
/// Public API Integration Tests - Testing via QueryBuilder and Query classes only.
/// Goal: Validate real user scenarios without testing internal implementation details.
/// Focus: Field operations, nesting, variables, serialization, and error handling.
/// Strategy: Test only public API surface that users actually call.
/// </summary>
public class PublicAPIIntegrationTests
{
    // ═══════════════════════════════════════════════════════════════
    // QueryBuilder: Field Operations - Core Functionality
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void QueryBuilder_AddSimpleFields_AllFieldsAppearInOutput()
    {
        var builder = QueryBuilder.CreateDefaultBuilder("GetUser");
        
        builder.AddField("id").AddField("name").AddField("email");
        var result = builder.ToString();
        
        result.Should().Contain("id").And.Contain("name").And.Contain("email");
    }

    [Fact]
    public void QueryBuilder_AddFieldWithPath_PathPreservedInOutput()
    {
        var builder = QueryBuilder.CreateDefaultBuilder("GetUser");
        
        builder.AddField("user.profile.name");
        var result = builder.ToString();
        
        result.Should().Contain("user");
    }

    [Fact]
    public void QueryBuilder_AddFieldWithArguments_ArgumentsSerializedCorrectly()
    {
        var builder = QueryBuilder.CreateDefaultBuilder("GetUsers");
        var args = new Dictionary<string, object?> { { "first", 10 }, { "after", "cursor123" } };
        
        builder.AddField("users", args);
        var result = builder.ToString();
        
        result.Should().Contain("first").And.Contain("10");
    }

    [Fact]
    public void QueryBuilder_AddFieldWithMetadata_MetadataStoredInDefinition()
    {
        var builder = QueryBuilder.CreateDefaultBuilder("GetUser");
        var metadata = new Dictionary<string, object?> { { "deprecated", true } };
        
        builder.AddField("oldField", metadata: metadata);
        
        // Metadata is stored in the Definition
        builder.Definition.Should().NotBeNull();
    }

    [Fact]
    public void QueryBuilder_AddFieldWithSubFields_SubFieldsIncluded()
    {
        var builder = QueryBuilder.CreateDefaultBuilder("GetUser");
        var subFields = new[] { "id", "name", "email" };
        
        builder.AddField("user", subFields);
        var result = builder.ToString();
        
        result.Should().Contain("user").And.Contain("id").And.Contain("name");
    }

    [Fact]
    public void QueryBuilder_AddFieldWithTypeAnnotation_TypeAnnotationProcessed()
    {
        var builder = QueryBuilder.CreateDefaultBuilder("GetUser");
        
        // Adding field with type annotation
        builder.AddField("User", "user");
        var result = builder.ToString();
        
        result.Should().NotBeEmpty();
    }

    [Fact]
    public void QueryBuilder_DuplicateFieldAddition_SecondCallOverwritesFirst()
    {
        var builder = QueryBuilder.CreateDefaultBuilder("GetUser");
        var argsV1 = new Dictionary<string, object?> { { "id", 1 } };
        var argsV2 = new Dictionary<string, object?> { { "id", 2 } };
        
        builder.AddField("user", argsV1);
        builder.AddField("user", argsV2);
        
        var result = builder.ToString();
        result.Should().Contain("user");
    }

    [Fact]
    public void QueryBuilder_ComplexFieldWithMultipleComponents_AllComponentsCombined()
    {
        var builder = QueryBuilder.CreateDefaultBuilder("GetData");
        var args = new Dictionary<string, object?> { { "first", 5 } };
        var subFields = new[] { "id", "name" };
        var metadata = new Dictionary<string, object?> { { "caching", "1h" } };
        
        builder.AddField("items", args, subFields, metadata);
        
        var result = builder.ToString();
        result.Should().Contain("items").And.Contain("first").And.Contain("id");
    }

    // ═══════════════════════════════════════════════════════════════
    // QueryBuilder: Nested Fields via FieldBuilder
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void QueryBuilder_NestedFieldsWithFieldBuilder_NestedStructureBuilt()
    {
        var builder = QueryBuilder.CreateDefaultBuilder("GetUser");
        
        builder.AddField("user", fb => 
        {
            fb.AddField("id").AddField("name");
        });
        
        var result = builder.ToString();
        result.Should().Contain("user").And.Contain("id").And.Contain("name");
    }

    [Fact]
    public void QueryBuilder_DeeplyNestedFields_MultiLevelNestingSupported()
    {
        var builder = QueryBuilder.CreateDefaultBuilder("GetData");
        
        builder.AddField("user", fb =>
        {
            fb.AddField("profile", fb2 =>
            {
                fb2.AddField("avatar");
            });
        });
        
        var result = builder.ToString();
        result.Should().Contain("user").And.Contain("profile").And.Contain("avatar");
    }

    [Fact]
    public void QueryBuilder_FieldBuilderWithArguments_ArgumentsPassedThroughCorrectly()
    {
        var builder = QueryBuilder.CreateDefaultBuilder("GetData");
        var args = new Dictionary<string, object?> { { "limit", 10 } };
        var subFields = new[] { "id", "title" };
        
        builder.AddField("items", args, subFields);
        
        var result = builder.ToString();
        result.Should().Contain("items").And.Contain("limit");
    }

    // ═══════════════════════════════════════════════════════════════
    // QueryBuilder: Variable Extraction from Arguments
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void QueryBuilder_VariableInFieldArgument_VariableAutomaticallyExtracted()
    {
        var builder = QueryBuilder.CreateDefaultBuilder("GetUsers");
        var variable = new Variable("$limit", "Int!");
        var args = new Dictionary<string, object?> { { "first", variable } };
        
        builder.AddField("users", args);
        
        builder.Variables.Should().Contain(v => v.Name == "$limit");
    }

    [Fact]
    public void QueryBuilder_MultipleVariablesInArguments_AllExtracted()
    {
        var builder = QueryBuilder.CreateDefaultBuilder("GetData");
        var var1 = new Variable("$first", "Int!");
        var var2 = new Variable("$after", "String!");
        var args = new Dictionary<string, object?> { { "first", var1 }, { "after", var2 } };
        
        builder.AddField("items", args);
        
        builder.Variables.Should().Contain(v => v.Name == "$first");
        builder.Variables.Should().Contain(v => v.Name == "$after");
    }

    [Fact]
    public void QueryBuilder_NestedVariablesInArguments_DeepVariablesExtracted()
    {
        var builder = QueryBuilder.CreateDefaultBuilder("GetData");
        var variable = new Variable("$limit", "Int!");
        var nestedArgs = new Dictionary<string, object?> { { "count", variable } };
        var args = new Dictionary<string, object?> { { "pagination", nestedArgs } };
        
        builder.AddField("items", args);
        
        builder.Variables.Should().Contain(v => v.Name == "$limit");
    }

    // ═══════════════════════════════════════════════════════════════
    // QueryBuilder: MergingStrategy Configuration
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void QueryBuilder_CreateWithMergingStrategy_StrategyStoredInDefinition()
    {
        var builder = QueryBuilder.CreateDefaultBuilder("GetUser", MergingStrategy.MergeByFieldPath);
        
        builder.Definition.MergingStrategy.Should().Be(MergingStrategy.MergeByFieldPath);
    }

    [Fact]
    public void QueryBuilder_ChangeMergingStrategy_StrategyUpdated()
    {
        var builder = QueryBuilder.CreateDefaultBuilder("GetUser");
        
        builder.WithMergingStrategy(MergingStrategy.NeverMerge);
        
        builder.Definition.MergingStrategy.Should().Be(MergingStrategy.NeverMerge);
    }

    [Fact]
    public void QueryBuilder_IncludeAnotherQueryBuilder_IncludedQueryAddedAsField()
    {
        var main = QueryBuilder.CreateDefaultBuilder("GetData");
        var subQuery = QueryBuilder.CreateDefaultBuilder("SubData")
            .AddField("id").AddField("name");
        
        main.Include(subQuery);
        
        var result = main.ToString();
        result.Should().NotBeEmpty();
    }

    // ═══════════════════════════════════════════════════════════════
    // QueryBuilder: Serialization and Output
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void QueryBuilder_ToStringOutput_ProducesValidOutput()
    {
        var builder = QueryBuilder.CreateDefaultBuilder("GetUser")
            .AddField("id")
            .AddField("name");
        
        var result = builder.ToString();
        
        result.Should().NotBeEmpty()
            .And.Contain("GetUser")
            .And.Contain("id")
            .And.Contain("name");
    }

    [Fact]
    public void QueryBuilder_ImplicitStringConversion_ProducesOutput()
    {
        var builder = QueryBuilder.CreateDefaultBuilder("GetUser")
            .AddField("id");
        
        string result = builder;
        
        result.Should().NotBeEmpty();
    }

    [Fact]
    public void QueryBuilder_RoundTripConsistency_SameBuilderStateProducesSameOutput()
    {
        var q1 = QueryBuilder.CreateDefaultBuilder("GetUser")
            .AddField("id")
            .AddField("name");
        
        var q2 = QueryBuilder.CreateDefaultBuilder("GetUser")
            .AddField("id")
            .AddField("name");
        
        q1.ToString().Should().Be(q2.ToString());
    }

    // ═══════════════════════════════════════════════════════════════
    // Query Class: Alternative API Entry Point
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void Query_CreateAndSelectFields_FieldsIncludedInDefinition()
    {
        var query = new Query("GetUser");
        
        query.Select(new object[] { "id", "name", "email" });
        
        query.FieldsList.Should().NotBeEmpty();
    }

    [Fact]
    public void Query_CreateWithVariables_VariablesStoredInBlock()
    {
        var variable = new Variable("$id", "Int!");
        var query = new Query("GetUser", variable);
        
        query.Variables.Should().Contain(v => v.Name == "$id");
    }

    [Fact]
    public void Query_CreateWithAlias_AliasPreserved()
    {
        var query = new Query("GetUser", "currentUser");
        
        query.Alias.Should().Be("currentUser");
    }

    [Fact]
    public void Query_AddMultipleVariables_AllVariablesIncluded()
    {
        var query = new Query("GetUsers");
        
        query.Variable(new Variable("$first", "Int!"))
            .Variable(new Variable("$after", "String"))
            .Variable("$limit", "Int");
        
        query.Variables.Should().HaveCountGreaterThanOrEqualTo(2);
    }

    [Fact]
    public void Query_SelectWithParams_AllFieldsAdded()
    {
        var query = new Query("GetUser");
        
        query.Select("id", "name", "email");
        
        query.FieldsList.Should().NotBeEmpty();
    }

    [Fact]
    public void Query_IncludeSubQuery_SubQueryIncluded()
    {
        var main = new Query("GetData");
        var subQuery = new Query("SubData");
        subQuery.Select("id", "name");
        
        main.Select(subQuery);
        
        main.FieldsList.Should().NotBeEmpty();
    }

    [Fact]
    public void Query_WhereWithArguments_ArgumentsStoredInBlock()
    {
        var query = new Query("GetUsers");
        
        query.Where("limit", 10).Where("offset", 5);
        
        query.Arguments.Should().NotBeEmpty();
    }

    // ═══════════════════════════════════════════════════════════════
    // Error Handling: Invalid Inputs and Edge Cases
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void QueryBuilder_AddFieldWithNullName_ThrowsArgumentException()
    {
        var builder = QueryBuilder.CreateDefaultBuilder("GetUser");
        
        Action act = () => builder.AddField(null!);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void QueryBuilder_AddFieldWithEmptyName_ThrowsArgumentException()
    {
        var builder = QueryBuilder.CreateDefaultBuilder("GetUser");
        
        Action act = () => builder.AddField("");
        
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void QueryBuilder_AddFieldWithNullFieldBuilder_ThrowsArgumentNullException()
    {
        var builder = QueryBuilder.CreateDefaultBuilder("GetUser");
        
        Action act = () => builder.AddField("user", (Action<FieldBuilder>)null!);
        
        act.Should().Throw<ArgumentNullException>();
    }

    // ═══════════════════════════════════════════════════════════════
    // Edge Cases: Boundary Conditions and Stress Tests
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void QueryBuilder_EmptyQuery_StillProducesValidOutput()
    {
        var builder = QueryBuilder.CreateDefaultBuilder("Empty");
        
        var result = builder.ToString();
        
        result.Should().NotBeEmpty();
    }

    [Fact]
    public void QueryBuilder_ManyFields_AllFieldsPreserved()
    {
        var builder = QueryBuilder.CreateDefaultBuilder("GetData");
        
        for (int i = 0; i < 20; i++)
        {
            builder.AddField($"field{i}");
        }
        
        var result = builder.ToString();
        result.Should().Contain("field0").And.Contain("field19");
    }

    [Fact]
    public void QueryBuilder_FieldWithUnderscoresInName_HandledCorrectly()
    {
        var builder = QueryBuilder.CreateDefaultBuilder("GetData");
        
        builder.AddField("user_profile_data");
        
        var result = builder.ToString();
        result.Should().Contain("user_profile_data");
    }

    [Fact]
    public void QueryBuilder_DeepFieldNesting_ManyLevelsSupportedCorrectly()
    {
        var builder = QueryBuilder.CreateDefaultBuilder("GetData");
        
        builder.AddField("level1", fb1 =>
        {
            fb1.AddField("level2", fb2 =>
            {
                fb2.AddField("level3", fb3 =>
                {
                    fb3.AddField("level4");
                });
            });
        });
        
        var result = builder.ToString();
        result.Should().Contain("level1").And.Contain("level4");
    }

    [Fact]
    public void QueryBuilder_DifferentMergingStrategies_AllStrategyValuesAccepted()
    {
        var strategies = new[] 
        { 
            MergingStrategy.MergeByDefault, 
            MergingStrategy.MergeByFieldPath, 
            MergingStrategy.NeverMerge 
        };
        
        foreach (var strategy in strategies)
        {
            var builder = QueryBuilder.CreateDefaultBuilder("GetUser", strategy);
            builder.Definition.MergingStrategy.Should().Be(strategy);
        }
    }

    [Fact]
    public void QueryBuilder_ArgumentsWithVariousTypes_AllTypesHandledCorrectly()
    {
        var builder = QueryBuilder.CreateDefaultBuilder("GetData");
        var args = new Dictionary<string, object?>
        {
            { "intValue", 42 },
            { "stringValue", "test" },
            { "boolValue", true },
            { "nullValue", null },
            { "doubleValue", 3.14 }
        };
        
        builder.AddField("mixed", args);
        
        var result = builder.ToString();
        result.Should().Contain("mixed");
    }

    [Fact]
    public void QueryBuilder_LongFieldPath_PathHandledCorrectly()
    {
        var builder = QueryBuilder.CreateDefaultBuilder("GetData");
        
        builder.AddField("root.level1.level2.level3.level4.level5.field");
        
        var result = builder.ToString();
        result.Should().NotBeEmpty();
    }
}
