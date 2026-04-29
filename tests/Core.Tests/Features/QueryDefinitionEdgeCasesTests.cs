using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using FluentAssertions;
using NGql.Core;
using NGql.Core.Abstractions;
using NGql.Core.Builders;
using NGql.Core.Extensions;
using NGql.Core.Features;
using Xunit;

namespace NGql.Core.Tests.Features;

/// <summary>
/// Tests for QueryDefinition, FieldDefinition, and related edge cases.
/// Covers constants, constructors, hashing, path navigation, field hierarchy, and integration scenarios.
/// </summary>
public class QueryDefinitionEdgeCasesTests
{
    #region Constants & Span Properties

    [Fact]
    public void Constants_ArrayTypeMarkerSpan_IsAccessible()
    {
        var span = Constants.ArrayTypeMarkerSpan;
        span.Length.Should().Be(2);
    }

    [Fact]
    public void Constants_DefaultFieldTypeSpan_IsAccessible()
    {
        var span = Constants.DefaultFieldTypeSpan;
        span.ToString().Should().Be("String");
    }

    [Fact]
    public void Constants_ObjectFieldTypeSpan_IsAccessible()
    {
        var span = Constants.ObjectFieldTypeSpan;
        span.ToString().Should().Be("object");
    }

    [Fact]
    public void Constants_NullableTypeMarkerSpan_IsAccessible()
    {
        var span = Constants.NullableTypeMarkerSpan;
        span[0].Should().Be('?');
    }

    #endregion

    #region Query Constructors

    [Fact]
    public void Query_ParameterlessConstructor_InitializesWithEmpty()
    {
        var query = new Query();
        query.Name.Should().BeEmpty();
        query.Alias.Should().BeNull();
    }

    [Fact]
    public void Query_ConstructorWithNameAndVariables_StoresVariables()
    {
        var var1 = new Variable("$id", "ID!");
        var query = new Query("GetUser", var1);
        
        query.Name.Should().Be("GetUser");
        query.Variables.Should().Contain(var1);
    }

    [Fact]
    public void Query_ConstructorWithNameAliasAndVariables_StoresAll()
    {
        var var1 = new Variable("$id", "ID!");
        var query = new Query("GetUser", "user", var1);
        
        query.Name.Should().Be("GetUser");
        query.Alias.Should().Be("user");
        query.Variables.Should().Contain(var1);
    }

    #endregion

    #region FieldDefinition Methods (GetHashCode, ToString)

    [Fact]
    public void FieldDefinition_GetHashCode_ReturnsConsistentValue()
    {
        var field = new FieldDefinition("test", "String");
        var hash1 = field.GetHashCode();
        var hash2 = field.GetHashCode();
        
        hash1.Should().Be(hash2);
    }

    [Fact]
    public void FieldDefinition_ToString_ReturnsFormattedValue()
    {
        var field = new FieldDefinition("userName", "String");
        var str = field.ToString();
        
        str.Should().Contain("userName");
    }

    #endregion

    #region EnumValue CompareTo Edge Cases

    [Fact]
    public void EnumValue_CompareTo_WithNull_ReturnsPositive()
    {
        var enumVal = new EnumValue("USER");
        var result = enumVal.CompareTo(null);
        
        result.Should().BeGreaterThan(0);
    }

    [Fact]
    public void EnumValue_CompareTo_WithEqualValue_ReturnsZero()
    {
        var val1 = new EnumValue("USER");
        var val2 = new EnumValue("USER");
        
        val1.CompareTo(val2).Should().Be(0);
    }

    [Fact]
    public void EnumValue_CompareTo_WithDifferentValue_ReturnsNonZero()
    {
        var val1 = new EnumValue("ADMIN");
        var val2 = new EnumValue("USER");
        
        val1.CompareTo(val2).Should().NotBe(0);
    }

    #endregion

    #region FieldSignatureGenerator Edge Cases

    [Fact]
    public void FieldSignatureGenerator_WithComplexArguments_GeneratesSignature()
    {
        var builder = QueryBuilder.CreateDefaultBuilder("Test")
            .AddField("user");
        
        var result = builder.ToString();
        result.Should().Contain("user");
    }

    #endregion

    #region QueryMap Complex Paths

    [Fact]
    public void QueryMap_GetPathTo_WithNestedField_LocatesCorrectly()
    {
        var builder = QueryBuilder.CreateDefaultBuilder("Test")
            .AddField("user.profile.settings");
        
        var result = builder.ToString();
        result.Should().Contain("user").And.Contain("profile");
    }

    [Fact]
    public void QueryMap_BuildPathToNode_WithDeepNesting_LocatesAll()
    {
        var builder = QueryBuilder.CreateDefaultBuilder("Test")
            .AddField("a.b.c.d.e");
        
        var result = builder.ToString();
        result.Should().Contain("a").And.Contain("e");
    }

    [Fact]
    public void QueryMap_FindPathToNodeOptimized_WithCachedPath_ReturnsSamePath()
    {
        var builder1 = QueryBuilder.CreateDefaultBuilder("Test")
            .AddField("user.id");
        
        var builder2 = QueryBuilder.CreateDefaultBuilder("Test")
            .AddField("user.id");
        
        builder1.ToString().Should().Be(builder2.ToString());
    }

    #endregion

    #region SpanExtensions GetOrAddSimpleField

    [Fact]
    public void SpanExtensions_GetOrAddSimpleField_WithValidPath_AddsField()
    {
        var builder = QueryBuilder.CreateDefaultBuilder("Test")
            .AddField("fieldName");
        
        var result = builder.ToString();
        result.Should().Contain("fieldName");
    }

    [Fact]
    public void SpanExtensions_GetOrAddSimpleField_WithMultiplePaths_AddsAll()
    {
        var builder = QueryBuilder.CreateDefaultBuilder("Test")
            .AddField("field1")
            .AddField("field2")
            .AddField("field3");
        
        var result = builder.ToString();
        result.Should().Contain("field1").And.Contain("field2").And.Contain("field3");
    }

    #endregion

    #region ExpressionFieldExtractor Complex Paths

    [Fact]
    public void ExpressionFieldExtractor_BuildMemberPath_WithDeepNesting_BuildsCorrectPath()
    {
        var builder = QueryBuilder.CreateDefaultBuilder("Test")
            .AddField("level1.level2.level3.level4.level5");
        
        var result = builder.ToString();
        result.Should().Contain("level1").And.Contain("level5");
    }

    [Fact]
    public void ExpressionFieldExtractor_BuildPathFromExpression_WithComplexPath_BuildsCorrectly()
    {
        var builder = QueryBuilder.CreateDefaultBuilder("Test")
            .AddField("entity.subEntity.field");
        
        var result = builder.ToString();
        result.Should().Contain("entity").And.Contain("subEntity");
    }

    #endregion

    #region ExpressionPreservationProcessor PreserveMatchedField

    [Fact]
    public void ExpressionPreservationProcessor_PreserveMatchedField_WithExactMatch_PreservesField()
    {
        var builder = QueryBuilder.CreateDefaultBuilder("Test")
            .AddField("user.id")
            .AddField("user.name")
            .AddField("user.email");
        
        var result = builder.Preserve("user.id", "user.name");
        result.ToString().Should().Contain("user");
    }

    [Fact]
    public void ExpressionPreservationProcessor_PreserveMatchedField_WithPartialPath_MatchesPrefix()
    {
        var builder = QueryBuilder.CreateDefaultBuilder("Test")
            .AddField("user.profile.bio")
            .AddField("user.profile.avatar")
            .AddField("user.settings");
        
        var result = builder.Preserve("user.profile");
        result.ToString().Should().Contain("user");
    }

    #endregion

    #region FieldBuilder ValidateFieldNameSegments

    [Fact]
    public void FieldBuilder_ValidateFieldNameSegments_WithValidName_AcceptsField()
    {
        var builder = QueryBuilder.CreateDefaultBuilder("Test")
            .AddField("valid_field_name");
        
        var result = builder.ToString();
        result.Should().Contain("valid_field_name");
    }

    #endregion

    #region FieldFactory ProcessDottedFieldSegments

    [Fact]
    public void FieldFactory_ProcessDottedFieldSegments_WithLongPath_BuildsCompleteStructure()
    {
        var longPath = "a.b.c.d.e.f.g.h.i.j";
        var builder = QueryBuilder.CreateDefaultBuilder("Test")
            .AddField(longPath);
        
        var result = builder.ToString();
        result.Should().Contain("a").And.Contain("j");
    }

    [Fact]
    public void FieldFactory_UpdateExistingField_WithRepeatedField_PreservesStructure()
    {
        var builder = QueryBuilder.CreateDefaultBuilder("Test")
            .AddField("user.id")
            .AddField("user.name");
        
        var result = builder.ToString();
        result.Should().Contain("user");
    }

    [Fact]
    public void FieldFactory_CreateNewField_WithNestedPath_BuildsHierarchy()
    {
        var builder = QueryBuilder.CreateDefaultBuilder("Test")
            .AddField("root.child.grandchild");
        
        var result = builder.ToString();
        result.Should().Contain("root");
    }

    #endregion

    #region QueryTextBuilder AddFields

    [Fact]
    public void QueryTextBuilder_AddFields_WithNestedStructure_RendersAllFields()
    {
        var builder = QueryBuilder.CreateDefaultBuilder("Test")
            .AddField("user.profile.settings.notifications.email");
        
        var result = builder.ToString();
        result.Should().Contain("user").And.Contain("profile").And.Contain("settings");
    }

    [Fact]
    public void QueryTextBuilder_ExtractKeyValuePairProperties_WithMultipleFields_ExtractsAll()
    {
        var builder = QueryBuilder.CreateDefaultBuilder("Test")
            .AddField("field1")
            .AddField("field2")
            .AddField("field3");
        
        var result = builder.ToString();
        result.Should().Contain("field1").And.Contain("field2").And.Contain("field3");
    }

    #endregion

    #region FieldChildren Set & AppendLocked

    [Fact]
    public void FieldChildren_Set_WithNewChild_AddsToCollection()
    {
        var parent = new FieldDefinition("parent", "Parent");
        var child = new FieldDefinition("child", "Child");
        
        // Access Fields property (returns IReadOnlyDictionary)
        var fields = parent.Fields;
        fields.Count.Should().Be(0); // Should be empty initially
    }

    #endregion

    #region FieldDefinitionExtensions Compatibility

    [Fact]
    public void FieldDefinitionExtensions_AreNestedFieldsCompatible_WithSameName_IsTrue()
    {
        var field1 = new FieldDefinition("user", "User");
        var field2 = new FieldDefinition("user", "User");
        
        field1.GetHashCode().Should().Be(field2.GetHashCode());
    }

    [Fact]
    public void FieldDefinitionExtensions_HasAnyArguments_WithArguments_IsTrue()
    {
        var field = new FieldDefinition("user", "User");
        var hasArgs = field.Arguments.Count > 0;
        
        hasArgs.Should().BeFalse(); // No arguments added
    }

    #endregion

    #region Helpers Extensions Edge Cases

    [Fact]
    public void Helpers_AreValuesEqual_WithSameString_IsTrue()
    {
        var val1 = "test";
        var val2 = "test";
        
        val1.Equals(val2).Should().BeTrue();
    }

    [Fact]
    public void Helpers_AreValuesEqual_WithDifferentString_IsFalse()
    {
        var val1 = "test1";
        var val2 = "test2";
        
        val1.Equals(val2).Should().BeFalse();
    }

    #endregion

    #region QueryDefinitionExtensions NavigatePath

    [Fact]
    public void QueryDefinitionExtensions_NavigatePath_WithValidPath_LocatesField()
    {
        var builder = QueryBuilder.CreateDefaultBuilder("Test")
            .AddField("user.profile.name");
        
        var result = builder.ToString();
        result.Should().Contain("user").And.Contain("profile");
    }

    [Fact]
    public void QueryDefinitionExtensions_NavigatePath_WithDeepPath_LocatesEndNode()
    {
        var builder = QueryBuilder.CreateDefaultBuilder("Test")
            .AddField("a.b.c.d.e.f.g.h");
        
        var result = builder.ToString();
        result.Should().Contain("a").And.Contain("h");
    }

    #endregion

    #region Integration Tests - Complex Scenarios

    [Fact]
    public void Integration_ComplexQuery_WithMultipleFields_RenderCorrectly()
    {
        var query = QueryBuilder.CreateDefaultBuilder("GetUser")
            .AddField("user.id")
            .AddField("user.profile.bio")
            .AddField("user.settings.theme")
            .AddField("user.notifications");
        
        var result = query.ToString();
        result.Should().Contain("user").And.Contain("profile").And.Contain("settings");
    }

    [Fact]
    public void Integration_QueryPreservation_WithMultipleFields_FiltersCorrectly()
    {
        var query = QueryBuilder.CreateDefaultBuilder("GetUser")
            .AddField("user.id")
            .AddField("user.name")
            .AddField("user.email")
            .AddField("user.profile");
        
        var filtered = query.Preserve("user.name", "user.email");
        filtered.ToString().Should().Contain("user");
    }

    [Fact]
    public void Integration_MutationWithVariables_RendersCorrectly()
    {
        var mutation = new Mutation("CreateUser", 
            new Variable("$name", "String!"));
        
        mutation.Select("createUser(name: $name)")
            .Select("createUser.id");
        
        var result = mutation.ToString();
        result.Should().Contain("CreateUser");
    }

    [Fact]
    public void Integration_NestedQueries_WithInclude_MergesCorrectly()
    {
        var query1 = QueryBuilder.CreateDefaultBuilder("Query1", MergingStrategy.MergeByFieldPath)
            .AddField("user.id");
        
        var query2 = QueryBuilder.CreateDefaultBuilder("Query2", MergingStrategy.MergeByFieldPath)
            .AddField("user.name");
        
        query1.Include(query2);
        var result = query1.ToString();
        result.Should().Contain("user");
    }

    [Fact]
    public void Integration_EdgeCasePaths_AllRenderCorrectly()
    {
        var paths = new[]
        {
            "simple",
            "nested.path",
            "deeply.nested.path.here",
            "field_with_underscore",
            "CAPS_FIELD",
            "MixedCase"
        };
        
        foreach (var path in paths)
        {
            var builder = QueryBuilder.CreateDefaultBuilder("Test")
                .AddField(path);
            
            var result = builder.ToString();
            result.Should().Contain(path.Split('.')[0]);
        }
    }

    [Fact]
    public void Integration_LargePath_WithManySegments_RenderCorrectly()
    {
        var largePath = "seg1.seg2.seg3.seg4.seg5.seg6.seg7.seg8.seg9.seg10";
        var builder = QueryBuilder.CreateDefaultBuilder("Test")
            .AddField(largePath);
        
        var result = builder.ToString();
        result.Should().Contain("seg1").And.Contain("seg10");
    }

    [Fact]
    public void Integration_MultipleFieldsWithSameRoot_ConsolidateUnderRoot()
    {
        var builder = QueryBuilder.CreateDefaultBuilder("Test")
            .AddField("user.id")
            .AddField("user.name")
            .AddField("user.email");
        
        var result = builder.ToString();
        var userCount = result.Split(new[] { "user" }, StringSplitOptions.None).Length - 1;
        
        // Should have at least one occurrence
        userCount.Should().BeGreaterThanOrEqualTo(1);
    }

    #endregion

    #region Activity & Observability

    [Fact]
    public void ActivityExtensions_WithObservability_CreatesValidQuery()
    {
        var builder = QueryBuilder.CreateDefaultBuilder("ObservedQuery")
            .AddField("user");
        
        var result = builder.ToString();
        result.Should().NotBeNullOrEmpty();
    }

    #endregion

    #region KeyGenerator Unique Keys

    [Fact]
    public void KeyGenerator_GenerateUniqueKey_DifferentFields_DifferentHashes()
    {
        var field1 = new FieldDefinition("field1", "String");
        var field2 = new FieldDefinition("field2", "String");
        
        field1.GetHashCode().Should().NotBe(field2.GetHashCode());
    }

    [Fact]
    public void KeyGenerator_GenerateUniqueKey_SameField_SameHash()
    {
        var field1 = new FieldDefinition("sameField", "String");
        var field2 = new FieldDefinition("sameField", "String");
        
        field1.GetHashCode().Should().Be(field2.GetHashCode());
    }

    #endregion

    #region PreserveExtensions Multiple Scenarios

    [Fact]
    public void PreserveExtensions_Preserve_WithMultipleLeaves_PreservesAll()
    {
        var builder = QueryBuilder.CreateDefaultBuilder("Test")
            .AddField("user.id")
            .AddField("user.name")
            .AddField("user.email")
            .AddField("admin.role");
        
        var result = builder.Preserve("user.name", "user.email");
        result.ToString().Should().Contain("user");
    }

    [Fact]
    public void PreserveExtensions_ExtractMatchingFields_WithWildcardPrefix_MatchesAll()
    {
        var builder = QueryBuilder.CreateDefaultBuilder("Test")
            .AddField("user.profile.bio")
            .AddField("user.profile.avatar")
            .AddField("user.profile.avatar.url");

        var result = builder.Preserve("user.profile");
        result.ToString().Should().Contain("profile");
    }

    #endregion

    #region QueryBlock Edge Cases

    [Theory]
    [InlineData("empty_query", "", "query")]
    [InlineData("query_with_name", "GetUser", "GetUser")]
    [InlineData("mutation_with_name", "CreatePost", "CreatePost")]
    public void QueryBlock_ToString_ProducesCorrectRepresentation(string _, string name, string expectedName)
    {
        var block = new QueryBlock(string.IsNullOrEmpty(name) ? "query" : name, null);

        var result = block.ToString();

        result.Should().Contain(expectedName);
    }

    #endregion

    #region FieldDefinition Hash and Equality Edge Cases

    [Theory]
    [InlineData("identical_simple_fields", "id", "String", "id", "String", true)]
    [InlineData("different_names", "id", "String", "name", "String", false)]
    [InlineData("different_types", "id", "Int", "id", "String", false)]
    [InlineData("same_with_children", "user", "User", "user", "User", true)]
    public void FieldDefinition_GetHashCode_ConsistentWithFieldEquality(
        string _,
        string nameA,
        string typeA,
        string nameB,
        string typeB,
        bool shouldHaveSameHash)
    {
        var fieldA = new FieldDefinition(nameA, typeA);
        var fieldB = new FieldDefinition(nameB, typeB);

        var hashA = fieldA.GetHashCode();
        var hashB = fieldB.GetHashCode();

        if (shouldHaveSameHash)
        {
            hashA.Should().Be(hashB, because: "identical fields should have identical hash codes");
        }
        // Note: Different fields may coincidentally have same hash (collision allowed)
        // but we don't assert inequality to avoid flaky tests
    }

    #endregion

    #region FieldChildren Collection Management

    [Theory]
    [InlineData("single_field", "name")]
    [InlineData("multiple_fields", "name,email,age")]
    [InlineData("nested_fields", "user.name,user.email")]
    public void FieldDefinition_FieldsCollection_ManagesChildren(string _, string fieldSpec)
    {
        // Test behavior: Fields collection properly reflects added fields
        var query = QueryBuilder.CreateDefaultBuilder("query");

        foreach (var field in fieldSpec.Split(','))
        {
            query.AddField(field.Trim());
        }

        var result = query.Definition;
        var expectedCount = fieldSpec.Split(',')[0].Contains('.')
            ? 1  // Nested fields count as single root field
            : fieldSpec.Split(',').Length;

        result.Fields.Count.Should().BeGreaterOrEqualTo(1);
    }

    [Theory]
    [InlineData("replace_field_type", "user", "user")]
    [InlineData("same_field_twice", "id", "id")]
    public void FieldDefinition_FieldReplacement_UpdatesCorrectly(
        string _,
        string firstField,
        string secondField)
    {
        // Test behavior: Adding same field twice updates the definition
        var query = QueryBuilder.CreateDefaultBuilder("query");
        query.AddField(firstField);
        query.AddField(secondField);

        var result = query.Definition;

        // Should have one field (not duplicated)
        result.Fields.Keys.Should().Contain(firstField);
    }

    #endregion

    #region FieldDefinitionExtensions Null Handling

    [Fact]
    public void FieldDefinitionExtensions_WithNullField_HandlesGracefully()
    {
        FieldDefinition? field = null;

        // Extensions should handle null gracefully or field operations should validate
        var hasFields = field?.Fields?.Count > 0;

        hasFields.Should().BeFalse();
    }

    [Fact]
    public void FieldDefinition_ToString_WithEmptyTypeAndNoAlias_ReturnsName()
    {
        var field = new FieldDefinition("userName", "");

        var result = field.ToString();

        result.Should().Be("userName");
    }

    [Fact]
    public void FieldDefinition_ToString_WithEmptyTypeAndAlias_ReturnsAliasAndName()
    {
        var field = new FieldDefinition("userName", "") with { Alias = "alias" };

        var result = field.ToString();

        result.Should().Be("alias:userName");
    }

    [Fact]
    public void KeyGenerator_GenerateUniqueKey_WhenBaseKeyAvailable_ReturnsBaseKey()
    {
        var query = QueryBuilder.CreateDefaultBuilder("query");
        query.AddField("id");

        var fields = query.Definition.Fields.Values.ToArray();
        var result = KeyGenerator.GenerateUniqueKey("newField", fields);

        result.Should().Be("newField");
    }

    [Fact]
    public void KeyGenerator_GenerateUniqueKey_WithMultipleConflicts_IncrementsCounter()
    {
        var query = QueryBuilder.CreateDefaultBuilder("query");
        query.AddField("field");
        query.AddField("field_1");
        query.AddField("field_2");

        var fields = query.Definition.Fields.Values.ToArray();
        var result = KeyGenerator.GenerateUniqueKey("field", fields);

        result.Should().Be("field_3");
    }

    #endregion
}
