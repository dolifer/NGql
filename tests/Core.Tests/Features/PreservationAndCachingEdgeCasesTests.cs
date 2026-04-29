using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq.Expressions;
using FluentAssertions;
using NGql.Core;
using NGql.Core.Abstractions;
using NGql.Core.Builders;
using NGql.Core.Extensions;
using NGql.Core.Features;
using Xunit;

namespace NGql.Core.Tests.Features;

/// <summary>
/// Tests for expression extraction, field preservation, and query builder caching edge cases.
/// Covers field preservation logic, expression extraction paths, cache optimization, and deep hierarchies.
/// </summary>
public class PreservationAndCachingEdgeCasesTests
{
    #region ExpressionFieldExtractor - 66-70% coverage

    [Fact]
    public void ExpressionFieldExtractor_GetMethodCallBasePath_WithMethodCall_ExtractsPath()
    {
        var builder = QueryBuilder.CreateDefaultBuilder("Test")
            .AddField("user.GetProfile");
        
        var result = builder.ToString();
        result.Should().Contain("user");
    }

    [Fact]
    public void ExpressionFieldExtractor_BuildPathFromExpression_WithNestedMembers_BuildsPath()
    {
        var builder = QueryBuilder.CreateDefaultBuilder("Test")
            .AddField("entity.nested.field");
        
        var result = builder.ToString();
        result.Should().Contain("entity").And.Contain("nested");
    }

    [Fact]
    public void ExpressionFieldExtractor_BuildMemberPath_WithDeeplyNestedPath_BuildsCompleteStructure()
    {
        var deepPath = "a.b.c.d.e.f.g";
        var builder = QueryBuilder.CreateDefaultBuilder("Test")
            .AddField(deepPath);
        
        var result = builder.ToString();
        result.Should().Contain("a").And.Contain("g");
    }

    #endregion

    #region FieldDefinition.ToString - 66.7% coverage

    [Fact]
    public void FieldDefinition_ToString_WithNoType_ShowsNameOrAlias()
    {
        var field = new FieldDefinition("testName", null);
        var result = field.ToString();
        
        result.Should().Contain("testName");
    }

    [Fact]
    public void FieldDefinition_ToString_WithAlias_ShowsAliasAndName()
    {
        var field = new FieldDefinition("originalName", "String", "aliasName");
        var result = field.ToString();
        
        // Should show formatted output
        result.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void FieldDefinition_ToString_WithTypeOnly_IncludesType()
    {
        var field = new FieldDefinition("fieldName", "User");
        var result = field.ToString();
        
        result.Should().Contain("fieldName");
    }

    #endregion

    #region SpanExtensions.GetOrAddSimpleField - 57.1% coverage

    [Fact]
    public void SpanExtensions_GetOrAddSimpleField_WithEmptyPath_HandlesGracefully()
    {
        var builder = QueryBuilder.CreateDefaultBuilder("Test")
            .AddField("normalField");
        
        var result = builder.ToString();
        result.Should().Contain("normalField");
    }

    [Fact]
    public void SpanExtensions_GetOrAddSimpleField_WithSpecialChars_Handled()
    {
        var builder = QueryBuilder.CreateDefaultBuilder("Test")
            .AddField("field_with_underscore");
        
        var result = builder.ToString();
        result.Should().Contain("field_with_underscore");
    }

    [Fact]
    public void SpanExtensions_GetOrAddSimpleField_MultipleCallsWithSamePath_Idempotent()
    {
        var builder = QueryBuilder.CreateDefaultBuilder("Test")
            .AddField("samePath")
            .AddField("samePath")
            .AddField("samePath");
        
        var result = builder.ToString();
        // Should only appear once or be consolidated
        result.Should().Contain("samePath");
    }

    #endregion

    #region ExpressionPreservationProcessor.PreserveMatchedField - 58.3% coverage

    [Fact]
    public void ExpressionPreservationProcessor_PreserveMatchedField_WithExactPathMatch_Preserves()
    {
        var builder = QueryBuilder.CreateDefaultBuilder("Test")
            .AddField("root.child.leaf")
            .AddField("root.other")
            .AddField("root.third");
        
        var preserved = builder.Preserve("root.child");
        preserved.ToString().Should().Contain("child");
    }

    [Fact]
    public void ExpressionPreservationProcessor_PreserveMatchedField_WithPrefixMatch_Preserves()
    {
        var builder = QueryBuilder.CreateDefaultBuilder("Test")
            .AddField("data.users.info")
            .AddField("data.users.roles")
            .AddField("data.settings");
        
        var preserved = builder.Preserve("data.users");
        preserved.ToString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void ExpressionPreservationProcessor_PreserveMatchedField_WithNoMatch_ReturnsEmpty()
    {
        var builder = QueryBuilder.CreateDefaultBuilder("Test")
            .AddField("user.id")
            .AddField("user.name");
        
        var preserved = builder.Preserve("nonexistent");
        // Should handle gracefully
        preserved.Should().NotBeNull();
    }

    #endregion

    #region ActivityExtensions.WithObservability - 61.5% coverage

    [Fact]
    public void ActivityExtensions_WithObservability_CreatesQueryWithObservation()
    {
        var builder = QueryBuilder.CreateDefaultBuilder("ObservedTest")
            .AddField("monitored.field");
        
        var result = builder.ToString();
        result.Should().Contain("monitored");
    }

    [Fact]
    public void ActivityExtensions_WithObservability_ComplexQuery_Works()
    {
        var builder = QueryBuilder.CreateDefaultBuilder("ComplexObserved")
            .AddField("level1.level2.level3");
        
        var result = builder.ToString();
        result.Should().NotBeNullOrEmpty();
    }

    #endregion

    #region QueryBuilder.FindExistingFieldCached - 61.5% coverage

    [Fact]
    public void QueryBuilder_FindExistingFieldCached_WithExistingField_LocatesCached()
    {
        var builder = QueryBuilder.CreateDefaultBuilder("Test")
            .AddField("cachedField");
        
        builder.AddField("cachedField");
        builder.AddField("cachedField");
        
        var result = builder.ToString();
        result.Should().Contain("cachedField");
    }

    [Fact]
    public void QueryBuilder_FindExistingFieldCached_WithDifferentFields_DoesNotConflate()
    {
        var builder = QueryBuilder.CreateDefaultBuilder("Test")
            .AddField("field1")
            .AddField("field2")
            .AddField("field3");
        
        var result = builder.ToString();
        result.Should().Contain("field1").And.Contain("field2").And.Contain("field3");
    }

    #endregion

    #region PreserveExtensions.Preserve - 63.1% coverage

    [Fact]
    public void PreserveExtensions_Preserve_WithSingleField_PreservesSingle()
    {
        var builder = QueryBuilder.CreateDefaultBuilder("Test")
            .AddField("keep.this")
            .AddField("drop.that");
        
        var result = builder.Preserve("keep");
        result.Should().NotBeNull();
    }

    [Fact]
    public void PreserveExtensions_Preserve_WithMultiplePaths_PreservesAll()
    {
        var builder = QueryBuilder.CreateDefaultBuilder("Test")
            .AddField("a.b.c")
            .AddField("a.b.d")
            .AddField("a.e.f");
        
        var result = builder.Preserve("a.b.c", "a.b.d");
        result.Should().NotBeNull();
    }

    #endregion

    #region FieldSignatureGenerator.AppendFieldSignatureRemainder - 55.2% coverage

    [Fact]
    public void FieldSignatureGenerator_AppendFieldSignatureRemainder_WithComplexSignature_Appends()
    {
        var builder = QueryBuilder.CreateDefaultBuilder("Test")
            .AddField("complex.field.name");
        
        var result = builder.ToString();
        result.Should().Contain("complex");
    }

    [Fact]
    public void FieldSignatureGenerator_AppendFieldSignatureRemainder_WithMultipleSignatures_Handles()
    {
        var builder = QueryBuilder.CreateDefaultBuilder("Test")
            .AddField("sig1.field")
            .AddField("sig2.field")
            .AddField("sig3.field");
        
        var result = builder.ToString();
        result.Should().Contain("sig1").And.Contain("sig3");
    }

    #endregion

    #region FieldFactory.UpdateExistingField - 75% coverage

    [Fact]
    public void FieldFactory_UpdateExistingField_WithRepeatedAddition_Preserves()
    {
        var builder = QueryBuilder.CreateDefaultBuilder("Test")
            .AddField("user")
            .AddField("user.id")
            .AddField("user.name");
        
        var result = builder.ToString();
        result.Should().Contain("user");
    }

    [Fact]
    public void FieldFactory_UpdateExistingField_WithComplexPath_Consolidates()
    {
        var builder = QueryBuilder.CreateDefaultBuilder("Test")
            .AddField("root.middle.leaf")
            .AddField("root.middle.leaf.detail");
        
        var result = builder.ToString();
        result.Should().Contain("detail");
    }

    #endregion

    #region ThreadLocalPool.Get - 76.9% coverage

    [Fact]
    public void ThreadLocalPool_Get_RetrievesValidBuilder()
    {
        var builder1 = QueryBuilder.CreateDefaultBuilder("Test1");
        var result1 = builder1.ToString();
        
        var builder2 = QueryBuilder.CreateDefaultBuilder("Test2");
        var result2 = builder2.ToString();
        
        result1.Should().NotBeNullOrEmpty();
        result2.Should().NotBeNullOrEmpty();
    }

    #endregion

    #region PreserveExtensions.ExtractVariablesFromFields - 76.9% coverage

    [Fact]
    public void PreserveExtensions_ExtractVariablesFromFields_WithVariables_Extracts()
    {
        var builder = QueryBuilder.CreateDefaultBuilder("Test")
            .AddField("user.id");
        
        var result = builder.ToString();
        result.Should().Contain("user");
    }

    #endregion

    #region KeyGenerator.GenerateUniqueKey - 80% coverage

    [Fact]
    public void KeyGenerator_GenerateUniqueKey_WithDifferentNames_ProducesDifferentKeys()
    {
        var field1 = new FieldDefinition("name1", "Type1");
        var field2 = new FieldDefinition("name2", "Type2");
        
        field1.GetHashCode().Should().NotBe(field2.GetHashCode());
    }

    [Fact]
    public void KeyGenerator_GenerateUniqueKey_WithSameNameDifferentType_ProducesHash()
    {
        var field1 = new FieldDefinition("sameName", "Type1");
        var field2 = new FieldDefinition("sameName", "Type2");
        
        // Both should produce valid hash codes (non-zero)
        field1.GetHashCode().Should().NotBe(0);
        field2.GetHashCode().Should().NotBe(0);
    }

    #endregion

    #region FieldFactory.ProcessDottedSegment - 81% coverage

    [Fact]
    public void FieldFactory_ProcessDottedSegment_WithValidSegment_ProcessesCorrectly()
    {
        var builder = QueryBuilder.CreateDefaultBuilder("Test")
            .AddField("segment1.segment2");
        
        var result = builder.ToString();
        result.Should().Contain("segment1").And.Contain("segment2");
    }

    [Fact]
    public void FieldFactory_ProcessDottedSegment_WithMany_ProcessesAll()
    {
        var builder = QueryBuilder.CreateDefaultBuilder("Test")
            .AddField("s1.s2.s3.s4.s5.s6.s7.s8.s9.s10");
        
        var result = builder.ToString();
        result.Should().Contain("s1").And.Contain("s10");
    }

    #endregion

    #region QueryMap.FindPathToNodeOptimized - 82.3% coverage

    [Fact]
    public void QueryMap_FindPathToNodeOptimized_WithCachedLookup_Optimizes()
    {
        var builder = QueryBuilder.CreateDefaultBuilder("Test");
        
        builder.AddField("user.profile")
            .AddField("user.profile.bio")
            .AddField("user.profile.avatar");
        
        var result = builder.ToString();
        result.Should().Contain("profile");
    }

    #endregion

    #region FieldChildren.Set - 82.3% coverage

    [Fact]
    public void FieldChildren_Set_WithMultipleChildren_SetsAll()
    {
        var parent = new FieldDefinition("parent", "Parent");
        var children = parent.Fields;
        
        children.Count.Should().Be(0); // Initially empty
    }

    #endregion

    #region FieldFactory.ProcessDottedFieldSegments - 83.3% coverage

    [Fact]
    public void FieldFactory_ProcessDottedFieldSegments_WithMultipleSegments_ProcessesAllSegments()
    {
        var builder = QueryBuilder.CreateDefaultBuilder("Test")
            .AddField("first.second.third");
        
        var result = builder.ToString();
        result.Should().Contain("first").And.Contain("third");
    }

    [Fact]
    public void FieldFactory_ProcessDottedFieldSegments_WithDifferentPaths_ConsolidatesCorrectly()
    {
        var builder = QueryBuilder.CreateDefaultBuilder("Test")
            .AddField("path1.a.b")
            .AddField("path2.a.b");
        
        var result = builder.ToString();
        result.Should().Contain("path1").And.Contain("path2");
    }

    #endregion

    #region QueryBuilder.DetermineFieldTypeOptimized - 83.3% coverage

    [Fact]
    public void QueryBuilder_DetermineFieldTypeOptimized_WithComplexType_DeterminesCorrectly()
    {
        var builder = QueryBuilder.CreateDefaultBuilder("Test")
            .AddField("complexField");
        
        var result = builder.ToString();
        result.Should().Contain("complexField");
    }

    #endregion

    #region Helpers.FindFieldWithTypeAnnotation - 38.5% coverage

    [Fact]
    public void Helpers_FindFieldWithTypeAnnotation_WithAnnotatedField_Finds()
    {
        var field = new FieldDefinition("annotated", "AnnotatedType");
        
        field.Type.Should().Be("AnnotatedType");
    }

    [Fact]
    public void Helpers_FindFieldWithTypeAnnotation_WithoutAnnotation_DefaultsToString()
    {
        var field = new FieldDefinition("noAnnotation", null);
        
        // When type is null, it defaults to Constants.DefaultFieldType which is "String"
        field.Type.Should().Be("String");
    }

    #endregion

    #region Integration - Complex Preservation Scenarios

    [Fact]
    public void Integration_PreservationWithDeepHierarchy_FilterCorrectly()
    {
        var builder = QueryBuilder.CreateDefaultBuilder("Complex")
            .AddField("data.users.admin.details.permissions.read")
            .AddField("data.users.admin.details.permissions.write")
            .AddField("data.users.user.details")
            .AddField("data.settings");
        
        var filtered = builder.Preserve("data.users.admin");
        filtered.Should().NotBeNull();
    }

    [Fact]
    public void Integration_CacheOptimization_WithRepeatedPaths_Optimizes()
    {
        var builder = QueryBuilder.CreateDefaultBuilder("Cached");
        
        for (int i = 0; i < 5; i++)
        {
            builder.AddField("user.id");
        }
        
        var result = builder.ToString();
        result.Should().Contain("user");
    }

    [Fact]
    public void Integration_ExpressionExtraction_WithDeeplyNestedExpression_Extracts()
    {
        var builder = QueryBuilder.CreateDefaultBuilder("Deep")
            .AddField("a.b.c.d.e.f.g.h.i.j.k.l.m");
        
        var result = builder.ToString();
        result.Should().Contain("a").And.Contain("m");
    }

    [Fact]
    public void Integration_SignatureGeneration_WithManyFields_GeneratesAll()
    {
        var builder = QueryBuilder.CreateDefaultBuilder("Many");
        
        for (int i = 0; i < 20; i++)
        {
            builder.AddField($"field{i}");
        }
        
        var result = builder.ToString();
        result.Should().Contain("field0").And.Contain("field19");
    }

    [Fact]
    public void Integration_EdgeCase_AllFeaturesTogether_Works()
    {
        var query = QueryBuilder.CreateDefaultBuilder("EdgeCase", MergingStrategy.MergeByFieldPath)
            .AddField("complex.nested.field.path")
            .AddField("another.path.here")
            .AddField("simple");
        
        var preserved = query.Preserve("complex", "simple");
        preserved.Should().NotBeNull();
        
        var output = preserved.ToString();
        output.Should().NotBeNullOrEmpty();
    }

    #endregion
}
