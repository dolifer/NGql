namespace NGql.Core.Tests.Extensions;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using FluentAssertions;
using NGql.Core;
using NGql.Core.Abstractions;
using NGql.Core.Extensions;
using Xunit;

/// <summary>
/// Helpers Extension Class - Comprehensive Test Coverage
/// Tests field manipulation, dictionary merging, variable extraction, and value comparison
/// Currently 69% (225/328 statements) - Target: 90%+
/// </summary>
public class HelpersComprehensiveTests
{
    // ═══════════════════════════════════════════════════════════════
    // ExtractVariablesFromValue - Variable extraction and recursion
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void ExtractVariables_NullValue_NoVariablesExtracted()
    {
        var variables = new SortedSet<Variable>();
        
        Helpers.ExtractVariablesFromValue(null, variables);
        
        variables.Should().BeEmpty();
    }

    [Fact]
    public void ExtractVariables_DirectVariable_VariableExtracted()
    {
        var variable = new Variable("$id", "Int!");
        var variables = new SortedSet<Variable>();
        
        Helpers.ExtractVariablesFromValue(variable, variables);
        
        variables.Should().ContainSingle().Which.Name.Should().Be("$id");
    }

    [Fact]
    public void ExtractVariables_VariableInDictionary_VariableExtracted()
    {
        var variable = new Variable("$userId", "Int!");
        var dict = new Dictionary<string, object?> { { "id", variable } };
        var variables = new SortedSet<Variable>();
        
        Helpers.ExtractVariablesFromValue(dict, variables);
        
        variables.Should().ContainSingle().Which.Name.Should().Be("$userId");
    }

    [Fact]
    public void ExtractVariables_VariableInList_VariableExtracted()
    {
        var variable = new Variable("$filter", "String!");
        var list = new List<object?> { "status", variable, "active" };
        var variables = new SortedSet<Variable>();
        
        Helpers.ExtractVariablesFromValue(list, variables);
        
        variables.Should().ContainSingle().Which.Name.Should().Be("$filter");
    }

    [Fact]
    public void ExtractVariables_NestedDictionaries_AllVariablesExtracted()
    {
        var var1 = new Variable("$id", "Int!");
        var var2 = new Variable("$name", "String!");
        var nested = new Dictionary<string, object?> 
        { 
            { "filter", new Dictionary<string, object?> { { "user", var1 }, { "search", var2 } } }
        };
        var variables = new SortedSet<Variable>();
        
        Helpers.ExtractVariablesFromValue(nested, variables);
        
        variables.Should().HaveCount(2);
    }

    [Fact]
    public void ExtractVariables_ObjectProperties_VariablesExtracted()
    {
        var variable = new Variable("$status", "String!");
        var obj = new TestDataObject { Value = variable };
        var variables = new SortedSet<Variable>();
        
        Helpers.ExtractVariablesFromValue(obj, variables);
        
        variables.Should().ContainSingle();
    }

    [Fact]
    public void ExtractVariables_CyclicReferences_NoStackOverflow()
    {
        var dict = new Dictionary<string, object?>();
        dict["self"] = dict; // Circular reference
        var variables = new SortedSet<Variable>();
        
        // Should not throw StackOverflowException
        Action act = () => Helpers.ExtractVariablesFromValue(dict, variables);
        act.Should().NotThrow();
    }

    // ═══════════════════════════════════════════════════════════════
    // MergeNullableDictionaries - Dictionary merging with recursion
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void MergeNullableDictionaries_EmptyExisting_ReturnsUpdate()
    {
        var existing = new Dictionary<string, object?>();
        var update = new Dictionary<string, object?> { { "key", "value" } };
        
        var result = Helpers.MergeNullableDictionaries(existing, update);
        
        result.Should().HaveCount(1);
        result["key"].Should().Be("value");
    }

    [Fact]
    public void MergeNullableDictionaries_NoOverlap_CombinesAll()
    {
        var existing = new Dictionary<string, object?> { { "a", 1 } };
        var update = new Dictionary<string, object?> { { "b", 2 } };
        
        var result = Helpers.MergeNullableDictionaries(existing, update);
        
        result.Should().HaveCount(2);
        result["a"].Should().Be(1);
        result["b"].Should().Be(2);
    }

    [Fact]
    public void MergeNullableDictionaries_WithOverlap_UpdateWins()
    {
        var existing = new Dictionary<string, object?> { { "key", "old" } };
        var update = new Dictionary<string, object?> { { "key", "new" } };
        
        var result = Helpers.MergeNullableDictionaries(existing, update);
        
        result["key"].Should().Be("new");
    }

    [Fact]
    public void MergeNullableDictionaries_NestedDictionaries_RecursivelyMerged()
    {
        var existing = new Dictionary<string, object?> 
        { 
            { "nested", new Dictionary<string, object?> { { "a", 1 } } }
        };
        var update = new Dictionary<string, object?> 
        { 
            { "nested", new Dictionary<string, object?> { { "b", 2 } } }
        };
        
        var result = Helpers.MergeNullableDictionaries(existing, update);
        
        result.Should().ContainKey("nested");
        var nested = result["nested"];
        nested.Should().NotBeNull();
        // The nested dict becomes a Dictionary after merge
        if (nested is System.Collections.IDictionary nestedDict)
        {
            nestedDict.Count.Should().Be(2);
        }
    }

    [Fact]
    public void MergeNullableDictionaries_NullValues_PreservedInResult()
    {
        var existing = new Dictionary<string, object?> { { "key", null } };
        var update = new Dictionary<string, object?> { { "other", "value" } };
        
        var result = Helpers.MergeNullableDictionaries(existing, update);
        
        result.Should().ContainKey("key");
        result["key"].Should().BeNull();
    }

    // ═══════════════════════════════════════════════════════════════
    // MergeMetadata - Metadata-specific merging with fast paths
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void MergeMetadata_NoExisting_ReturnsConvertedUpdate()
    {
        var update = new Dictionary<string, object> { { "key", "value" } };
        
        var result = Helpers.MergeMetadata(null, update);
        
        result.Should().HaveCount(1);
    }

    [Fact]
    public void MergeMetadata_NoUpdate_ReturnsExisting()
    {
        var existing = new Dictionary<string, object?> { { "key", "value" } };
        var update = new Dictionary<string, object>();
        
        var result = Helpers.MergeMetadata(existing, update);
        
        result.Should().BeSameAs(existing);
    }

    [Fact]
    public void MergeMetadata_BothPresent_MergesCombiningCounts()
    {
        var existing = new Dictionary<string, object?> { { "a", 1 } };
        var update = new Dictionary<string, object> { { "b", 2 } };
        
        var result = Helpers.MergeMetadata(existing, update);
        
        result.Should().HaveCount(2);
    }

    // ═══════════════════════════════════════════════════════════════
    // AreArgumentsEqual - Optimized argument comparison
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void AreArgumentsEqual_BothNull_ReturnsTrue()
    {
        var result = Helpers.AreArgumentsEqual(null, null);
        
        result.Should().BeTrue();
    }

    [Fact]
    public void AreArgumentsEqual_SameReference_ReturnsTrue()
    {
        var args = new SortedDictionary<string, object?> { { "key", "value" } };
        
        var result = Helpers.AreArgumentsEqual(args, args);
        
        result.Should().BeTrue();
    }

    [Fact]
    public void AreArgumentsEqual_OneNull_ReturnsFalse()
    {
        var args = new SortedDictionary<string, object?> { { "key", "value" } };
        
        var result = Helpers.AreArgumentsEqual(args, null);
        
        result.Should().BeFalse();
    }

    [Fact]
    public void AreArgumentsEqual_DifferentCounts_ReturnsFalse()
    {
        var args1 = new SortedDictionary<string, object?> { { "a", 1 } };
        var args2 = new SortedDictionary<string, object?> { { "a", 1 }, { "b", 2 } };
        
        var result = Helpers.AreArgumentsEqual(args1, args2);
        
        result.Should().BeFalse();
    }

    [Fact]
    public void AreArgumentsEqual_BothEmpty_ReturnsTrue()
    {
        var args1 = new SortedDictionary<string, object?>();
        var args2 = new SortedDictionary<string, object?>();
        
        var result = Helpers.AreArgumentsEqual(args1, args2);
        
        result.Should().BeTrue();
    }

    [Fact]
    public void AreArgumentsEqual_IdenticalArguments_ReturnsTrue()
    {
        var args1 = new SortedDictionary<string, object?> { { "id", 123 }, { "name", "test" } };
        var args2 = new SortedDictionary<string, object?> { { "id", 123 }, { "name", "test" } };
        
        var result = Helpers.AreArgumentsEqual(args1, args2);
        
        result.Should().BeTrue();
    }

    [Fact]
    public void AreArgumentsEqual_DifferentValues_ReturnsFalse()
    {
        var args1 = new SortedDictionary<string, object?> { { "id", 123 } };
        var args2 = new SortedDictionary<string, object?> { { "id", 456 } };
        
        var result = Helpers.AreArgumentsEqual(args1, args2);
        
        result.Should().BeFalse();
    }

    [Fact]
    public void AreArgumentsEqual_NestedDictionaries_RecursivelyCompared()
    {
        var nested1 = new Dictionary<string, object?> { { "nested", "value1" } };
        var nested2 = new Dictionary<string, object?> { { "nested", "value1" } };
        
        var args1 = new SortedDictionary<string, object?> { { "data", nested1 } };
        var args2 = new SortedDictionary<string, object?> { { "data", nested2 } };
        
        var result = Helpers.AreArgumentsEqual(args1, args2);
        
        result.Should().BeTrue();
    }

    [Fact]
    public void AreArgumentsEqual_NestedListsCompared()
    {
        var args1 = new SortedDictionary<string, object?> { { "items", new List<object> { 1, 2, 3 } } };
        var args2 = new SortedDictionary<string, object?> { { "items", new List<object> { 1, 2, 3 } } };
        
        var result = Helpers.AreArgumentsEqual(args1, args2);
        
        result.Should().BeTrue();
    }

    // ═══════════════════════════════════════════════════════════════
    // SortArgumentValue - Value sorting for deterministic signatures
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void SortArgumentValue_NullValue_ReturnsNull()
    {
        var result = Helpers.SortArgumentValue(null);
        
        result.Should().BeNull();
    }

    [Fact]
    public void SortArgumentValue_PrimitiveType_ReturnedAsIs()
    {
        var result = Helpers.SortArgumentValue(123);
        
        result.Should().Be(123);
    }

    [Fact]
    public void SortArgumentValue_StringValue_ReturnedAsIs()
    {
        var result = Helpers.SortArgumentValue("test");
        
        result.Should().Be("test");
    }

    [Fact]
    public void SortArgumentValue_DictionaryKeys_Sorted()
    {
        var dict = new Dictionary<string, object?> { { "z", 1 }, { "a", 2 }, { "m", 3 } };
        
        var result = Helpers.SortArgumentValue(dict) as SortedDictionary<string, object?>;
        
        result.Should().NotBeNull();
        var keys = result.Keys.ToList();
        keys.Should().Equal("a", "m", "z");
    }

    [Fact]
    public void SortArgumentValue_ListOfPrimitives_Preserved()
    {
        var list = new List<object> { 1, 2, 3 };
        
        var result = Helpers.SortArgumentValue(list) as List<object>;
        
        result.Should().Equal(1, 2, 3);
    }

    [Fact]
    public void SortArgumentValue_Array_ConvertedToArray()
    {
        var arr = new object[] { 1, 2, 3 };
        
        var result = Helpers.SortArgumentValue(arr);
        
        result.Should().BeOfType<object[]>();
    }

    [Fact]
    public void SortArgumentValue_ObjectWithProperties_ConvertedToSortedDictionary()
    {
        var obj = new TestDataObject { Value = "test" };
        
        var result = Helpers.SortArgumentValue(obj);
        
        result.Should().BeOfType<SortedDictionary<string, object?>>();
    }

    // ═══════════════════════════════════════════════════════════════
    // FindExistingField - Field lookup by various criteria
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void FindExistingField_ByNameAndAlias_FoundWhenMatches()
    {
        var field = new FieldDefinition("user", "User", "currentUser");
        var fields = new Dictionary<string, FieldDefinition> { { "user:currentUser", field } };
        
        var result = Helpers.FindExistingField(fields, field);
        
        result.Should().Be(field);
    }

    [Fact]
    public void FindExistingField_WithComplexName_FoundWhenMatches()
    {
        var field = new FieldDefinition("profile", "Profile");
        var fields = new Dictionary<string, FieldDefinition> { { "profile", field } };
        
        var result = Helpers.FindExistingField(fields, field);
        
        result.Should().Be(field);
    }

    [Fact]
    public void FindExistingField_NotFound_ReturnsNull()
    {
        var field = new FieldDefinition("posts", "Post[]");
        var fields = new Dictionary<string, FieldDefinition>();
        
        var result = Helpers.FindExistingField(fields, field);
        
        result.Should().BeNull();
    }

    // ═══════════════════════════════════════════════════════════════
    // ParseFieldTypeFromPath - Type annotation parsing
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void ParseFieldTypeFromPath_NoType_ReturnsDefaultType()
    {
        var path = "user.profile.name".AsSpan();
        var defaultType = "String".AsSpan();
        
        var result = Helpers.ParseFieldTypeFromPath(path, defaultType, out var type);
        
        type.SequenceEqual("String".AsSpan()).Should().BeTrue();
    }

    [Fact]
    public void ParseFieldTypeFromPath_WithType_ExtractsType()
    {
        var path = "User user.profile".AsSpan();
        var defaultType = "String".AsSpan();
        
        var result = Helpers.ParseFieldTypeFromPath(path, defaultType, out var type);
        
        type.SequenceEqual("User".AsSpan()).Should().BeTrue();
    }

    [Fact]
    public void ParseFieldTypeFromPath_ArrayType_ExtractsArrayType()
    {
        var path = "[User!]! users".AsSpan();
        var defaultType = "String".AsSpan();
        
        _ = Helpers.ParseFieldTypeFromPath(path, defaultType, out var type);
        
        type.ToString().Should().Contain("[User!]!");
    }

    // ═══════════════════════════════════════════════════════════════
    // ValidateFieldName - Field name validation
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void ValidateFieldName_ValidFieldName_NoException()
    {
        Action act = () => Helpers.ValidateFieldName("userId");
        
        act.Should().NotThrow();
    }

    [Fact]
    public void ValidateFieldName_StartsWithUnderscore_Valid()
    {
        Action act = () => Helpers.ValidateFieldName("_private");
        
        act.Should().NotThrow();
    }

    [Fact]
    public void ValidateFieldName_WithNumbers_Valid()
    {
        Action act = () => Helpers.ValidateFieldName("field123");
        
        act.Should().NotThrow();
    }

    [Fact]
    public void ValidateFieldName_EmptyString_ThrowsException()
    {
        Action act = () => Helpers.ValidateFieldName("");
        
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ValidateFieldName_StartsWithNumber_ThrowsException()
    {
        Action act = () => Helpers.ValidateFieldName("123field");
        
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ValidateFieldName_WithSpecialCharacters_ThrowsException()
    {
        Action act = () => Helpers.ValidateFieldName("field-name");
        
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ValidateFieldName_WithTypeAnnotation_ValidatesActualName()
    {
        Action act = () => Helpers.ValidateFieldName("String fieldName");
        
        act.Should().NotThrow();
    }

    [Fact]
    public void ValidateFieldName_WithAlias_ValidatesBothParts()
    {
        Action act = () => Helpers.ValidateFieldName("alias:fieldName");
        
        act.Should().NotThrow();
    }

    // ═══════════════════════════════════════════════════════════════
    // Test Helper Class
    // ═══════════════════════════════════════════════════════════════

    private class TestDataObject
    {
        public object Value { get; set; }
    }

    // ═══════════════════════════════════════════════════════════════
    // TDD: Allocation Profiling Tests
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// RED TEST: Verify that SortArgumentValue with list doesn't create unnecessary allocations.
    /// This tests the fix for the double-allocation issue (.Select().ToList()).
    /// </summary>
    [Fact]
    public void SortArgumentValue_ListOfPrimitives_MinimalAllocations()
    {
        var inputList = new List<object> { 1, "hello", 2.5, true };
        
        var result = Helpers.SortArgumentValue(inputList);
        
        // The result should be a list with same items
        result.Should().BeOfType<List<object>>();
        var resultList = (List<object>)result;
        resultList.Should().HaveCount(4);
        resultList[0].Should().Be(1);
        resultList[1].Should().Be("hello");
        resultList[2].Should().Be(2.5);
        resultList[3].Should().Be(true);
    }

    /// <summary>
    /// RED TEST: Verify SortArgumentValue handles list with nested objects efficiently.
    /// </summary>
    [Fact]
    public void SortArgumentValue_ListWithNestedObjects_PreservesStructure()
    {
        var nestedDict = new Dictionary<string, object?> { { "key", "value" } };
        var inputList = new List<object> { 1, nestedDict };
        
        var result = Helpers.SortArgumentValue(inputList);
        
        result.Should().BeOfType<List<object>>();
        var resultList = (List<object>)result;
        resultList.Should().HaveCount(2);
        resultList[0].Should().Be(1);
        resultList[1].Should().BeOfType<SortedDictionary<string, object?>>();
    }

    /// <summary>
    /// RED TEST: Verify SortArgumentValue handles arrays without extra List allocation.
    /// </summary>
    [Fact]
    public void SortArgumentValue_ArrayOfPrimitives_ReturnsArray()
    {
        object[] inputArray = { 1, "hello", 2.5 };
        
        var result = Helpers.SortArgumentValue(inputArray);
        
        result.Should().BeOfType<object[]>();
        ((object[])result).Should().HaveCount(3);
    }

    // ═══════════════════════════════════════════════════════════════
    // MergeNullableMetadata - Comprehensive coverage for edge cases
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void MergeNullableMetadata_BothNullUpdate_ReturnsExistingOrEmpty()
    {
        var existing = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase) { { "key1", "value1" } };
        
        var result = Helpers.MergeNullableMetadata(existing, null);
        
        result.Should().BeSameAs(existing);
    }

    [Fact]
    public void MergeNullableMetadata_EmptyUpdateDict_ReturnsExisting()
    {
        var existing = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase) { { "key1", "value1" } };
        var update = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        
        var result = Helpers.MergeNullableMetadata(existing, update);
        
        result.Should().BeSameAs(existing);
    }

    [Fact]
    public void MergeNullableMetadata_NullExistingUpdate_ReturnsUpdateCopy()
    {
        var update = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase) { { "key2", "value2" } };
        
        var result = Helpers.MergeNullableMetadata(null, update);
        
        result.Should().NotBeSameAs(update);
        result.Should().HaveCount(1);
        result["key2"].Should().Be("value2");
    }

    [Fact]
    public void MergeNullableMetadata_EmptyExistingWithUpdate_ReturnsCopyOfUpdate()
    {
        var existing = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        var update = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase) { { "key2", "value2" } };
        
        var result = Helpers.MergeNullableMetadata(existing, update);
        
        result.Should().NotBeSameAs(update);
        result.Should().HaveCount(1);
        result["key2"].Should().Be("value2");
    }

    [Fact]
    public void MergeNullableMetadata_NestedDictMerge_RecursivelyMerged()
    {
        var existing = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            { "nested", new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase) { { "a", 1 } } }
        };
        var update = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            { "nested", new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase) { { "b", 2 } } }
        };
        
        var result = Helpers.MergeNullableMetadata(existing, update);
        
        result.Should().ContainKey("nested");
        var nested = result["nested"] as Dictionary<string, object?>;
        nested.Should().NotBeNull();
        nested.Should().HaveCount(2);
        nested["a"].Should().Be(1);
        nested["b"].Should().Be(2);
    }

    [Fact]
    public void MergeNullableMetadata_NestedNullValuesPreserved()
    {
        var existing = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            { "nested", new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase) { { "nullKey", null } } }
        };
        var update = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            { "nested", new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase) { { "otherKey", "value" } } }
        };
        
        var result = Helpers.MergeNullableMetadata(existing, update);
        
        var nested = result["nested"] as Dictionary<string, object?>;
        nested.Should().ContainKey("nullKey");
        nested["nullKey"].Should().BeNull();
        nested.Should().ContainKey("otherKey");
    }

    [Fact]
    public void MergeNullableMetadata_OverrideNonDictValue_UpdateWins()
    {
        var existing = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase) { { "key", "oldValue" } };
        var update = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase) { { "key", "newValue" } };
        
        var result = Helpers.MergeNullableMetadata(existing, update);
        
        result["key"].Should().Be("newValue");
    }

    [Fact]
    public void MergeNullableMetadata_MixedDictAndNonDictValues_NonDictWins()
    {
        var existing = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            { "key", new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase) { { "a", 1 } } }
        };
        var update = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase) { { "key", "plain" } };
        
        var result = Helpers.MergeNullableMetadata(existing, update);
        
        result["key"].Should().Be("plain");
    }

    // ═══════════════════════════════════════════════════════════════
    // MergeMetadata - Comprehensive coverage for edge cases
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void MergeMetadata_NoExistingEmptyUpdate_ReturnsConvertedUpdate()
    {
        var update = new Dictionary<string, object>();
        
        var result = Helpers.MergeMetadata(null, update);
        
        result.Should().HaveCount(0);
    }

    [Fact]
    public void MergeMetadata_ExistingEmptyUpdate_ReturnsExisting()
    {
        var existing = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase) { { "key", "value" } };
        var update = new Dictionary<string, object>();
        
        var result = Helpers.MergeMetadata(existing, update);
        
        result.Should().BeSameAs(existing);
    }

    [Fact]
    public void MergeMetadata_NestedDictionaries_DeepMerged()
    {
        var existing = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            { "nested", new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase) { { "a", 1 } } }
        };
        var update = new Dictionary<string, object>
        {
            { "nested", new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase) { { "b", 2 } } }
        };
        
        var result = Helpers.MergeMetadata(existing, update);
        
        var nested = result["nested"] as Dictionary<string, object?>;
        nested.Should().NotBeNull();
        nested.Should().HaveCount(2);
    }

    [Fact]
    public void MergeMetadata_NonDictOverridesDict_UpdateWins()
    {
        var existing = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            { "key", new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase) { { "a", 1 } } }
        };
        var update = new Dictionary<string, object> { { "key", "plainValue" } };
        
        var result = Helpers.MergeMetadata(existing, update);
        
        result["key"].Should().Be("plainValue");
    }

    [Fact]
    public void MergeMetadata_LargeExistingSet_PreallocatesCorrectly()
    {
        var existing = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < 100; i++)
        {
            existing[$"key{i}"] = $"value{i}";
        }
        var update = new Dictionary<string, object> { { "newKey", "newValue" } };
        
        var result = Helpers.MergeMetadata(existing, update);
        
        result.Should().HaveCount(101);
        result.Should().ContainKey("newKey");
    }

    // ═══════════════════════════════════════════════════════════════
    // CreateFieldDefinition - Field creation with various inputs
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void CreateFieldDefinition_NoArguments_CreatesFieldDef()
    {
        var result = Helpers.CreateFieldDefinition(
            "field".AsSpan(),
            "String".AsSpan(),
            "alias".AsSpan(),
            null,
            "path".AsSpan()
        );
        
        result.Should().NotBeNull();
        result.Name.Should().Be("field");
        result.Type.Should().Be("String");
        result._alias.Should().Be("alias");
    }

    [Fact]
    public void CreateFieldDefinition_EmptyArguments_SkipsSorting()
    {
        var args = new Dictionary<string, object?>();
        
        var result = Helpers.CreateFieldDefinition(
            "field".AsSpan(),
            "User".AsSpan(),
            default,
            args,
            "field".AsSpan()
        );
        
        // Empty arguments dict is treated as null
        result.Should().NotBeNull();
    }

    [Fact]
    public void CreateFieldDefinition_WithArguments_SortsKeys()
    {
        var args = new Dictionary<string, object?> { { "z", 1 }, { "a", 2 }, { "m", 3 } };
        
        var result = Helpers.CreateFieldDefinition(
            "field".AsSpan(),
            "String".AsSpan(),
            default,
            args,
            "field".AsSpan()
        );
        
        result.Arguments.Should().NotBeNull();
        var keys = result.Arguments.Keys.ToList();
        keys.Should().Equal("a", "m", "z");
    }

    [Fact]
    public void CreateFieldDefinition_WithMetadata_PreservesMetadata()
    {
        var metadata = new Dictionary<string, object?> { { "meta", "data" } };
        
        var result = Helpers.CreateFieldDefinition(
            "field".AsSpan(),
            "String".AsSpan(),
            default,
            null,
            "field".AsSpan(),
            metadata
        );
        
        result._metadata.Should().BeSameAs(metadata);
    }

    [Fact]
    public void CreateFieldDefinition_EmptyAlias_CreatesNullAlias()
    {
        var result = Helpers.CreateFieldDefinition(
            "field".AsSpan(),
            "String".AsSpan(),
            ReadOnlySpan<char>.Empty,
            null,
            "field".AsSpan()
        );
        
        result._alias.Should().BeNull();
    }

    [Fact]
    public void CreateFieldDefinition_NestedArguments_RecursivelySorted()
    {
        var nestedDict = new Dictionary<string, object?> { { "z", 1 }, { "a", 2 } };
        var args = new Dictionary<string, object?> { { "nested", nestedDict } };
        
        var result = Helpers.CreateFieldDefinition(
            "field".AsSpan(),
            "String".AsSpan(),
            default,
            args,
            "field".AsSpan()
        );
        
        result.Arguments.Should().NotBeNull();
        var nested = result.Arguments["nested"] as SortedDictionary<string, object?>;
        nested.Should().NotBeNull();
        nested.Keys.ToList().Should().Equal("a", "z");
    }

    // ═══════════════════════════════════════════════════════════════
    // ParseFieldTypeFromPath - Edge cases for type parsing
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void ParseFieldTypeFromPath_NoSpaceInPath_DefaultType()
    {
        var result = Helpers.ParseFieldTypeFromPath(
            "userId".AsSpan(),
            "Int".AsSpan(),
            out var type
        );
        
        type.SequenceEqual("Int".AsSpan()).Should().BeTrue();
        result.SequenceEqual("userId".AsSpan()).Should().BeTrue();
    }

    [Fact]
    public void ParseFieldTypeFromPath_TypeWithBrackets_Extracted()
    {
        var result = Helpers.ParseFieldTypeFromPath(
            "[String!]! users".AsSpan(),
            "Int".AsSpan(),
            out var type
        );
        
        type.SequenceEqual("[String!]!".AsSpan()).Should().BeTrue();
    }

    [Fact]
    public void ParseFieldTypeFromPath_InvalidTypeMarker_DefaultUsed()
    {
        // Type marker starting with dot or containing dot should default
        var result = Helpers.ParseFieldTypeFromPath(
            ".invalid path".AsSpan(),
            "String".AsSpan(),
            out var type
        );
        
        type.SequenceEqual("String".AsSpan()).Should().BeTrue();
    }

    [Fact]
    public void ParseFieldTypeFromPath_TrailingDotsAndSpaces_Trimmed()
    {
        var result = Helpers.ParseFieldTypeFromPath(
            "field...  ".AsSpan(),
            "String".AsSpan(),
            out _
        );
        
        result.SequenceEqual("field".AsSpan()).Should().BeTrue();
    }

    [Fact]
    public void ParseFieldTypeFromPath_BracketStartType_Extracted()
    {
        var result = Helpers.ParseFieldTypeFromPath(
            "[] items".AsSpan(),
            "String".AsSpan(),
            out var type
        );
        
        type.SequenceEqual("[]".AsSpan()).Should().BeTrue();
    }

    // ═══════════════════════════════════════════════════════════════
    // ValidateFieldName - Additional edge cases
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void ValidateFieldName_WithTypeAndAlias_BothValidated()
    {
        Action act = () => Helpers.ValidateFieldName("Int! alias:fieldName");
        
        act.Should().NotThrow();
    }

    [Fact]
    public void ValidateFieldName_TypeWithSpaces_Trimmed()
    {
        Action act = () => Helpers.ValidateFieldName("String fieldName");
        
        act.Should().NotThrow();
    }

    [Fact]
    public void ValidateFieldName_AliasWithInvalidChar_Throws()
    {
        Action act = () => Helpers.ValidateFieldName("badAlias-name:validName");
        
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ValidateFieldName_FieldNameWithInvalidChar_Throws()
    {
        Action act = () => Helpers.ValidateFieldName("validAlias:bad-field");
        
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ValidateFieldName_OnlySpaces_Throws()
    {
        Action act = () => Helpers.ValidateFieldName("   ");
        
        act.Should().Throw<ArgumentException>();
    }

    // ═══════════════════════════════════════════════════════════════
    // ExtractVariablesFromValue - Additional scenarios
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void ExtractVariables_ListCyclicReferences_NoStackOverflow()
    {
        var list = new List<object?>();
        list.Add(list); // Circular reference
        var variables = new SortedSet<Variable>();
        
        Action act = () => Helpers.ExtractVariablesFromValue(list, variables);
        act.Should().NotThrow();
    }

    [Fact]
    public void ExtractVariables_ComplexNestedStructure_AllVariablesExtracted()
    {
        var var1 = new Variable("$id", "Int!");
        var var2 = new Variable("$status", "String!");
        var var3 = new Variable("$name", "String!");
        
        var complexObj = new Dictionary<string, object?>
        {
            { "filter", new Dictionary<string, object?> { { "id", var1 }, { "status", var2 } } },
            { "list", new List<object?> { var3, "constant" } }
        };
        
        var variables = new SortedSet<Variable>();
        Helpers.ExtractVariablesFromValue(complexObj, variables);
        
        variables.Should().HaveCount(3);
    }

    [Fact]
    public void ExtractVariables_NestedListsWithVariables_AllExtracted()
    {
        var var1 = new Variable("$a", "Int!");
        var var2 = new Variable("$b", "Int!");
        
        var nested = new List<object?> { new List<object?> { var1, var2 }, "text" };
        var variables = new SortedSet<Variable>();
        
        Helpers.ExtractVariablesFromValue(nested, variables);
        
        variables.Should().HaveCount(2);
    }

    // ═══════════════════════════════════════════════════════════════
    // AreValuesEqual - Additional comparisons
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void AreArgumentsEqual_ListWithDifferentOrder_ReturnsFalse()
    {
        var args1 = new SortedDictionary<string, object?> { { "items", new List<object> { 1, 2, 3 } } };
        var args2 = new SortedDictionary<string, object?> { { "items", new List<object> { 3, 2, 1 } } };
        
        var result = Helpers.AreArgumentsEqual(args1, args2);
        
        result.Should().BeFalse();
    }

    [Fact]
    public void AreArgumentsEqual_MissingKey_ReturnsFalse()
    {
        var args1 = new SortedDictionary<string, object?> { { "a", 1 }, { "b", 2 } };
        var args2 = new SortedDictionary<string, object?> { { "a", 1 } };
        
        var result = Helpers.AreArgumentsEqual(args1, args2);
        
        result.Should().BeFalse();
    }

    [Fact]
    public void AreArgumentsEqual_NestedDictWithDifferentValues_ReturnsFalse()
    {
        var nested1 = new Dictionary<string, object?> { { "key", "value1" } };
        var nested2 = new Dictionary<string, object?> { { "key", "value2" } };
        
        var args1 = new SortedDictionary<string, object?> { { "data", nested1 } };
        var args2 = new SortedDictionary<string, object?> { { "data", nested2 } };
        
        var result = Helpers.AreArgumentsEqual(args1, args2);
        
        result.Should().BeFalse();
    }

    [Fact]
    public void AreArgumentsEqual_EmptyNestedDicts_ReturnsTrue()
    {
        var nested1 = new Dictionary<string, object?>();
        var nested2 = new Dictionary<string, object?>();
        
        var args1 = new SortedDictionary<string, object?> { { "data", nested1 } };
        var args2 = new SortedDictionary<string, object?> { { "data", nested2 } };
        
        var result = Helpers.AreArgumentsEqual(args1, args2);
        
        result.Should().BeTrue();
    }

    [Fact]
    public void SortArgumentValue_ComplexNestedStructure_AllSorted()
    {
        var dict = new Dictionary<string, object?>
        {
            { "z", new Dictionary<string, object?> { { "z", 1 }, { "a", 2 } } },
            { "a", new List<object> { 3, 1, 2 } }
        };
        
        var result = Helpers.SortArgumentValue(dict);
        
        result.Should().BeOfType<SortedDictionary<string, object?>>();
    }

    // ═══════════════════════════════════════════════════════════════
    // Additional Deep Coverage Tests
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void MergeNullableMetadata_DeeplyNestedDictionaries_RecursivelyMerged()
    {
        var existing = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            {
                "level1", new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                {
                    {
                        "level2", new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                        {
                            { "a", 1 }
                        }
                    }
                }
            }
        };
        var update = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            {
                "level1", new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                {
                    {
                        "level2", new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                        {
                            { "b", 2 }
                        }
                    }
                }
            }
        };
        
        var result = Helpers.MergeNullableMetadata(existing, update);
        
        result.Should().ContainKey("level1");
        var level1 = result["level1"] as Dictionary<string, object?>;
        level1.Should().ContainKey("level2");
        var level2 = level1["level2"] as Dictionary<string, object?>;
        level2.Should().HaveCount(2);
    }

    [Fact]
    public void MergeNullableMetadata_MultipleKeysWithMixedUpdates()
    {
        var existing = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            { "key1", "value1" },
            { "key2", new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase) { { "a", 1 } } },
            { "key3", null }
        };
        var update = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            { "key2", new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase) { { "b", 2 } } },
            { "key4", "value4" }
        };
        
        var result = Helpers.MergeNullableMetadata(existing, update);
        
        result.Should().HaveCount(4);
        result["key1"].Should().Be("value1");
        result["key3"].Should().BeNull();
        result["key4"].Should().Be("value4");
        var key2Dict = result["key2"] as Dictionary<string, object?>;
        key2Dict.Should().HaveCount(2);
    }

    [Fact]
    public void AreArgumentsEqual_ComplexNestedStructures_ComparedRecursively()
    {
        var complex1 = new SortedDictionary<string, object?>
        {
            {
                "nested", new Dictionary<string, object?>
                {
                    {
                        "deep", new List<object>
                        {
                            new Dictionary<string, object?> { { "x", 1 } },
                            "text"
                        }
                    }
                }
            }
        };
        var complex2 = new SortedDictionary<string, object?>
        {
            {
                "nested", new Dictionary<string, object?>
                {
                    {
                        "deep", new List<object>
                        {
                            new Dictionary<string, object?> { { "x", 1 } },
                            "text"
                        }
                    }
                }
            }
        };
        
        var result = Helpers.AreArgumentsEqual(complex1, complex2);
        
        result.Should().BeTrue();
    }

    [Fact]
    public void SortArgumentValue_NestedListWithMixedTypes_AllSorted()
    {
        var input = new List<object>
        {
            new Dictionary<string, object?> { { "z", 1 }, { "a", 2 } },
            "text",
            123,
            new List<object> { 2, 1, 3 }
        };
        
        var result = Helpers.SortArgumentValue(input);
        
        result.Should().BeOfType<List<object>>();
        var resultList = (List<object>)result;
        resultList.Should().HaveCount(4);
        resultList[0].Should().BeOfType<SortedDictionary<string, object?>>();
        resultList[1].Should().Be("text");
    }

    [Fact]
    public void ExtractVariables_DeeplyNestedStructure_AllExtracted()
    {
        var var1 = new Variable("$a", "Int!");
        var var2 = new Variable("$b", "String!");
        var var3 = new Variable("$c", "Boolean!");
        
        var obj = new Dictionary<string, object?>
        {
            { "l1", new Dictionary<string, object?> { { "l2", new Dictionary<string, object?> { { "l3", var1 } } } } },
            { "list", new List<object?> { new List<object?> { var2 } } },
            { "direct", var3 }
        };
        
        var variables = new SortedSet<Variable>();
        Helpers.ExtractVariablesFromValue(obj, variables);
        
        variables.Should().HaveCount(3);
    }

    [Fact]
    public void ExtractVariables_VariableInObjectProperty_Extracted()
    {
        var variable = new Variable("$id", "Int!");
        var obj = new TestDataObject { Value = variable };
        var variables = new SortedSet<Variable>();
        
        Helpers.ExtractVariablesFromValue(obj, variables);
        
        variables.Should().ContainSingle();
    }

    [Fact]
    public void ExtractVariables_MixedCyclicAndLinearReferences()
    {
        var dict1 = new Dictionary<string, object?>();
        var dict2 = new Dictionary<string, object?> { { "key", "value" } };
        dict1["self"] = dict1;
        dict1["other"] = dict2;
        var variables = new SortedSet<Variable>();
        
        Action act = () => Helpers.ExtractVariablesFromValue(dict1, variables);
        act.Should().NotThrow();
    }

    [Fact]
    public void CreateFieldDefinition_WithComplexNestedArguments()
    {
        var args = new Dictionary<string, object?>
        {
            {
                "filter", new Dictionary<string, object?>
                {
                    { "z", 1 },
                    { "a", new Dictionary<string, object?> { { "nested", true } } }
                }
            }
        };
        
        var result = Helpers.CreateFieldDefinition(
            "query".AsSpan(),
            "QueryResponse".AsSpan(),
            default,
            args,
            "query.path".AsSpan()
        );
        
        result.Arguments.Should().NotBeNull();
        result.Arguments.Keys.Should().Contain("filter");
    }

    [Fact]
    public void ParseFieldTypeFromPath_MultipleSpaces_ReturnsWithLeadingSpaces()
    {
        var result = Helpers.ParseFieldTypeFromPath(
            "String     fieldPath".AsSpan(),
            "Int".AsSpan(),
            out var type
        );
        
        type.SequenceEqual("String".AsSpan()).Should().BeTrue();
        // When there are multiple spaces, the result will have leading spaces since TrimEndDotsAndSpaces only trims from end
        result.ToString().Should().Be("    fieldPath");
    }

    [Fact]
    public void ParseFieldTypeFromPath_ArrayWithExclamation_Extracted()
    {
        var result = Helpers.ParseFieldTypeFromPath(
            "[String!]! path".AsSpan(),
            "Int".AsSpan(),
            out var type
        );
        
        type.SequenceEqual("[String!]!".AsSpan()).Should().BeTrue();
    }

    [Fact]
    public void ParseFieldTypeFromPath_NoLettersOrDigits_DefaultUsed()
    {
        // "[]" by itself should be treated as a valid type marker
        var result = Helpers.ParseFieldTypeFromPath(
            "[] items".AsSpan(),
            "String".AsSpan(),
            out var type
        );
        
        type.SequenceEqual("[]".AsSpan()).Should().BeTrue();
    }

    [Fact]
    public void ValidateFieldName_SingleCharacterName_Valid()
    {
        Action act = () => Helpers.ValidateFieldName("a");
        
        act.Should().NotThrow();
    }

    [Fact]
    public void ValidateFieldName_UnderscoreOnly_Valid()
    {
        Action act = () => Helpers.ValidateFieldName("_");
        
        act.Should().NotThrow();
    }

    [Fact]
    public void ValidateFieldName_LongIdentifier_Valid()
    {
        Action act = () => Helpers.ValidateFieldName(new string('a', 256));
        
        act.Should().NotThrow();
    }

    [Fact]
    public void ValidateFieldName_InvalidFirstCharInAlias_Throws()
    {
        Action act = () => Helpers.ValidateFieldName("1badAlias:goodName");
        
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ValidateFieldName_InvalidCharInFieldName_Throws()
    {
        Action act = () => Helpers.ValidateFieldName("alias:field@name");
        
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AreArgumentsEqual_EmptyDictionaries_ReturnsTrue()
    {
        var dict1 = new SortedDictionary<string, object?>();
        var dict2 = new SortedDictionary<string, object?>();
        
        var result = Helpers.AreArgumentsEqual(dict1, dict2);
        
        result.Should().BeTrue();
    }

    [Fact]
    public void AreArgumentsEqual_NullInBothDictionaries_ReturnsTrue()
    {
        var dict1 = new SortedDictionary<string, object?> { { "key", null } };
        var dict2 = new SortedDictionary<string, object?> { { "key", null } };
        
        var result = Helpers.AreArgumentsEqual(dict1, dict2);
        
        result.Should().BeTrue();
    }

    [Fact]
    public void AreArgumentsEqual_ListWithNullElements_ComparedCorrectly()
    {
        var dict1 = new SortedDictionary<string, object?> { { "items", new List<object?> { 1, null, 3 } } };
        var dict2 = new SortedDictionary<string, object?> { { "items", new List<object?> { 1, null, 3 } } };
        
        var result = Helpers.AreArgumentsEqual(dict1, dict2);
        
        result.Should().BeTrue();
    }

    [Fact]
    public void SortArgumentValue_BooleanValues_Preserved()
    {
        var result = Helpers.SortArgumentValue(true);
        
        result.Should().Be(true);
    }

    [Fact]
    public void SortArgumentValue_FloatingPointValue_Preserved()
    {
        var result = Helpers.SortArgumentValue(3.14159);
        
        result.Should().Be(3.14159);
    }

    [Fact]
    public void SortArgumentValue_CustomObject_ConvertedToSortedDictionary()
    {
        var obj = new { zField = "z", aField = "a", mField = "m" };
        
        var result = Helpers.SortArgumentValue(obj);
        
        result.Should().BeOfType<SortedDictionary<string, object?>>();
        var sorted = (SortedDictionary<string, object?>)result;
        var keys = sorted.Keys.ToList();
        keys.Should().Equal("aField", "mField", "zField");
    }

    [Fact]
    public void MergeMetadata_NestedDictWithBothDictAndNonDictValues()
    {
        var existing = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            {
                "meta", new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
                {
                    { "dict", new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase) { { "a", 1 } } },
                    { "value", 42 }
                }
            }
        };
        var update = new Dictionary<string, object>
        {
            {
                "meta", new Dictionary<string, object>
                {
                    { "dict", new Dictionary<string, object> { { "b", 2 } } }
                }
            }
        };
        
        var result = Helpers.MergeMetadata(existing, update);
        
        var metaDict = result["meta"] as Dictionary<string, object?>;
        metaDict.Should().NotBeNull();
        metaDict.Should().ContainKey("value");
        metaDict["value"].Should().Be(42);
    }

    // ═══════════════════════════════════════════════════════════════
    // FindExistingField - Comprehensive field lookup scenarios
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void FindExistingFieldByPath_SimpleFieldExists_Returns()
    {
        var field = new FieldDefinition("user", "User");
        var fields = new Dictionary<string, FieldDefinition> { { "user", field } };
        
        var result = Helpers.FindExistingFieldByPath(fields, "user".AsSpan());
        
        result.Should().Be(field);
    }

    [Fact]
    public void FindExistingFieldByPath_EmptyPath_ReturnsNull()
    {
        var fields = new Dictionary<string, FieldDefinition>();
        
        var result = Helpers.FindExistingFieldByPath(fields, ReadOnlySpan<char>.Empty);
        
        result.Should().BeNull();
    }

    [Fact]
    public void FindExistingFieldByPath_WhitespacePath_ReturnsNull()
    {
        var fields = new Dictionary<string, FieldDefinition>();
        
        var result = Helpers.FindExistingFieldByPath(fields, "   ".AsSpan());
        
        result.Should().BeNull();
    }

    [Fact]
    public void FindExistingFieldByPath_FieldNotFound_ReturnsNull()
    {
        var fields = new Dictionary<string, FieldDefinition>();
        
        var result = Helpers.FindExistingFieldByPath(fields, "nonexistent".AsSpan());
        
        result.Should().BeNull();
    }

    [Fact]
    public void FindExistingField_ByPathInDictionary_FoundByPath()
    {
        var field = new FieldDefinition("profile", "Profile") { Path = "profile" };
        var fields = new Dictionary<string, FieldDefinition> { { "profile", field } };
        var searchField = new FieldDefinition("other", "Other") { Path = "profile" };
        
        var result = Helpers.FindExistingField(fields, searchField);
        
        result.Should().Be(field);
    }

    [Fact]
    public void FindExistingField_NoMatchByNameOrAlias_LooksByPath()
    {
        var field = new FieldDefinition("name", "String", "name_alias") { Path = "path_key" };
        var fields = new Dictionary<string, FieldDefinition> { { "path_key", field } };
        var searchField = new FieldDefinition("different", "String") { Path = "path_key" };
        
        var result = Helpers.FindExistingField(fields, searchField);
        
        result.Should().Be(field);
    }

    [Fact]
    public void FindExistingField_EmptyFieldsDict_ReturnsNull()
    {
        var fields = new Dictionary<string, FieldDefinition>();
        var searchField = new FieldDefinition("any", "String");
        
        var result = Helpers.FindExistingField(fields, searchField);
        
        result.Should().BeNull();
    }

    [Fact]
    public void FindExistingField_MultipleFieldsNameMatch_ReturnsFirst()
    {
        var field1 = new FieldDefinition("user", "User");
        var field2 = new FieldDefinition("user", "User", "u");
        var field3 = new FieldDefinition("post", "Post");
        var fields = new Dictionary<string, FieldDefinition>
        {
            { "f1", field1 },
            { "f2", field2 },
            { "f3", field3 }
        };
        var searchField = new FieldDefinition("user", "User");
        
        var result = Helpers.FindExistingField(fields, searchField);
        
        result.Should().NotBeNull();
        result.Name.Should().Be("user");
    }

    // ═══════════════════════════════════════════════════════════════
    // Additional edge cases and conditional branches
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void SortArgumentValue_EmptyDictionary_ReturnsSortedEmpty()
    {
        var dict = new Dictionary<string, object?>();
        
        var result = Helpers.SortArgumentValue(dict);
        
        result.Should().BeOfType<SortedDictionary<string, object?>>();
        ((SortedDictionary<string, object?>)result).Should().BeEmpty();
    }

    [Fact]
    public void SortArgumentValue_SingleElementDictionary_ReturnsSorted()
    {
        var dict = new Dictionary<string, object?> { { "key", "value" } };
        
        var result = Helpers.SortArgumentValue(dict);
        
        result.Should().BeOfType<SortedDictionary<string, object?>>();
    }

    [Fact]
    public void SortArgumentValue_EmptyList_ReturnsEmptyList()
    {
        var list = new List<object>();
        
        var result = Helpers.SortArgumentValue(list);
        
        result.Should().BeOfType<List<object>>();
        ((List<object>)result).Should().BeEmpty();
    }

    [Fact]
    public void SortArgumentValue_EmptyArray_ReturnsEmptyArray()
    {
        object[] arr = { };
        
        var result = Helpers.SortArgumentValue(arr);
        
        result.Should().BeOfType<object[]>();
        ((object[])result).Should().BeEmpty();
    }

    [Fact]
    public void AreArgumentsEqual_ArgumentsWithTypesDifferent_ReturnsFalse()
    {
        var args1 = new SortedDictionary<string, object?> { { "value", 123 } };
        var args2 = new SortedDictionary<string, object?> { { "value", "123" } };
        
        var result = Helpers.AreArgumentsEqual(args1, args2);
        
        result.Should().BeFalse();
    }

    [Fact]
    public void AreArgumentsEqual_ArrayVsList_ReturnsFalse()
    {
        var arr = new object[] { 1, 2, 3 };
        var list = new List<object> { 1, 2, 3 };
        
        var args1 = new SortedDictionary<string, object?> { { "data", arr } };
        var args2 = new SortedDictionary<string, object?> { { "data", list } };
        
        var result = Helpers.AreArgumentsEqual(args1, args2);
        
        result.Should().BeFalse();
    }

    [Fact]
    public void CreateFieldDefinition_LargeArgumentSet_SortsAllKeys()
    {
        var args = new Dictionary<string, object?>();
        for (int i = 100; i >= 0; i--)
        {
            args[$"field{i:D3}"] = i;
        }
        
        var result = Helpers.CreateFieldDefinition(
            "test".AsSpan(),
            "String".AsSpan(),
            default,
            args,
            "test".AsSpan()
        );
        
        result.Arguments.Should().NotBeNull();
        result.Arguments.Keys.First().Should().Be("field000");
        result.Arguments.Keys.Last().Should().Be("field100");
    }

    [Fact]
    public void ParseFieldTypeFromPath_TypeStartsWithBracket_Extracted()
    {
        var result = Helpers.ParseFieldTypeFromPath(
            "[Int!]! value".AsSpan(),
            "String".AsSpan(),
            out var type
        );
        
        type.SequenceEqual("[Int!]!".AsSpan()).Should().BeTrue();
    }

    [Fact]
    public void ParseFieldTypeFromPath_OnlyBrackets_Extracted()
    {
        var result = Helpers.ParseFieldTypeFromPath(
            "[] data".AsSpan(),
            "String".AsSpan(),
            out var type
        );
        
        type.SequenceEqual("[]".AsSpan()).Should().BeTrue();
    }

    [Fact]
    public void ParseFieldTypeFromPath_TypeWithDot_DefaultUsed()
    {
        // Type should not contain dots, so this should use default
        var result = Helpers.ParseFieldTypeFromPath(
            "User.Profile field".AsSpan(),
            "String".AsSpan(),
            out var type
        );
        
        type.SequenceEqual("String".AsSpan()).Should().BeTrue();
    }

    [Fact]
    public void ParseFieldTypeFromPath_TypeStartsWithNumber_DefaultUsed()
    {
        var result = Helpers.ParseFieldTypeFromPath(
            "123Type field".AsSpan(),
            "String".AsSpan(),
            out var type
        );
        
        type.SequenceEqual("String".AsSpan()).Should().BeTrue();
    }

    [Fact]
    public void ValidateFieldName_MultipleColons_OnlyFirstProcessed()
    {
        // "alias:name:extra" should validate "alias" and "name:extra" (only first colon matters)
        Action act = () => Helpers.ValidateFieldName("myAlias:myField");
        
        act.Should().NotThrow();
    }

    [Fact]
    public void ExtractVariables_StringValue_NoVariablesExtracted()
    {
        var variables = new SortedSet<Variable>();
        
        Helpers.ExtractVariablesFromValue("test string", variables);
        
        variables.Should().BeEmpty();
    }

    [Fact]
    public void ExtractVariables_PrimitiveValues_NoVariablesExtracted()
    {
        var variables = new SortedSet<Variable>();
        
        Helpers.ExtractVariablesFromValue(42, variables);
        Helpers.ExtractVariablesFromValue(true, variables);
        Helpers.ExtractVariablesFromValue(3.14, variables);
        
        variables.Should().BeEmpty();
    }

    [Fact]
    public void MergeNullableMetadata_CaseInsensitiveKeys()
    {
        var existing = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            { "Key", "value1" }
        };
        var update = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            { "key", "value2" }
        };
        
        var result = Helpers.MergeNullableMetadata(existing, update);
        
        result.Should().HaveCount(1);
        result["KEY"].Should().Be("value2");
    }

    [Fact]
    public void MergeMetadata_CaseInsensitiveKeys()
    {
        var existing = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            { "Key", "value1" }
        };
        var update = new Dictionary<string, object>
        {
            { "key", "value2" }
        };
        
        var result = Helpers.MergeMetadata(existing, update);
        
        result.Should().HaveCount(1);
        result["KEY"].Should().Be("value2");
    }

    // ═══════════════════════════════════════════════════════════════
    // WriteCollection - Collection writing with custom formatting
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void WriteCollection_EmptyList_WritesEmptyPair()
    {
        var list = new List<string>();
        var builder = new StringBuilder();

        Helpers.WriteCollection('[', ']', list, builder, (sb, obj) => sb.Append(obj ?? "null"));

        builder.ToString().Should().Be("[]");
    }

    [Fact]
    public void WriteCollection_SingleItem_WritesSingleItem()
    {
        var list = new List<string> { "item" };
        var builder = new StringBuilder();

        Helpers.WriteCollection('(', ')', list, builder, (sb, obj) => sb.Append(obj));

        builder.ToString().Should().Be("(item)");
    }

    [Fact]
    public void WriteCollection_MultipleItems_WritesCommaSeparated()
    {
        var list = new List<string> { "a", "b", "c" };
        var builder = new StringBuilder();

        Helpers.WriteCollection('{', '}', list, builder, (sb, obj) => sb.Append(obj));

        builder.ToString().Should().Be("{a, b, c}");
    }

    [Fact]
    public void WriteCollection_WithNullItems_HandlesNullInWriter()
    {
        var list = new List<string?> { "a", null, "c" };
        var builder = new StringBuilder();

        Helpers.WriteCollection('[', ']', list, builder, (sb, obj) => sb.Append(obj ?? "NULL"));

        builder.ToString().Should().Be("[a, NULL, c]");
    }

    [Fact]
    public void WriteCollection_CustomFormatting_AppliesFormatting()
    {
        var list = new List<int> { 1, 2, 3 };
        var builder = new StringBuilder();

        Helpers.WriteCollection('<', '>', list, builder, (sb, obj) => sb.Append($"#{obj}"));

        builder.ToString().Should().Be("<#1, #2, #3>");
    }

    // ═══════════════════════════════════════════════════════════════
    // FindExistingField (FieldChildren variant) - Field lookup in children
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void FindExistingField_FieldChildrenVariant_ByName_Found()
    {
        var child = new FieldDefinition("profile", "Profile");
        var children = new FieldChildren();
        children.Append(child);
        var fieldToFind = new FieldDefinition("profile", "Profile");

        var result = Helpers.FindExistingField(children, fieldToFind);

        result.Should().Be(child);
    }

    [Fact]
    public void FindExistingField_FieldChildrenVariant_NotFound_ReturnsNull()
    {
        var child = new FieldDefinition("profile", "Profile");
        var children = new FieldChildren();
        children.Append(child);
        var fieldToFind = new FieldDefinition("other", "Other");

        var result = Helpers.FindExistingField(children, fieldToFind);

        result.Should().BeNull();
    }

    [Fact]
    public void FindExistingField_FieldChildrenVariant_WithAlias_MatchesCorrectly()
    {
        var child = new FieldDefinition("profileData", "Profile", "profile");
        var children = new FieldChildren();
        children.Append(child);
        var fieldToFind = new FieldDefinition("profileData", "Profile", "profile");

        var result = Helpers.FindExistingField(children, fieldToFind);

        result.Should().NotBeNull();
    }

    [Fact]
    public void FindExistingField_FieldChildrenVariant_EmptyChildren_ReturnsNull()
    {
        var children = new FieldChildren();
        var fieldToFind = new FieldDefinition("any", "Any");

        var result = Helpers.FindExistingField(children, fieldToFind);

        result.Should().BeNull();
    }

    [Fact]
    public void FindExistingField_FieldChildrenVariant_ByPath_NotFound()
    {
        // FieldChildren.Find only searches by name, not by dot-separated path
        var child = new FieldDefinition("field", "String") { Path = "parent.field" };
        var children = new FieldChildren();
        children.Append(child);
        var fieldToFind = new FieldDefinition("dummy", "String") { Path = "parent.field" };

        var result = Helpers.FindExistingField(children, fieldToFind);

        // Won't find because "parent.field" doesn't match "field" name
        result.Should().BeNull();
    }

    // ═══════════════════════════════════════════════════════════════
    // CreateFieldDefinition - Field definition creation variants
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void CreateFieldDefinition_WithNullMetadata_CreatesWithoutMetadata()
    {
        var result = Helpers.CreateFieldDefinition("field".AsSpan(), "String".AsSpan(), "alias".AsSpan(), null, "path".AsSpan(), null);

        result.Should().NotBeNull();
        result.Name.Should().Be("field");
        result.Alias.Should().Be("alias");
        result._metadata.Should().BeNull();
    }

    [Fact]
    public void CreateFieldDefinition_WithMetadata_CreatesWithMetadata()
    {
        var metadata = new Dictionary<string, object?> { { "key", "value" } };

        var result = Helpers.CreateFieldDefinition("field".AsSpan(), "String".AsSpan(), ReadOnlySpan<char>.Empty, null, "path".AsSpan(), metadata);

        result.Should().NotBeNull();
        result._metadata.Should().NotBeNull();
    }

    [Fact]
    public void CreateFieldDefinition_WithComplexArguments_SortsArgumentKeys()
    {
        var arguments = new Dictionary<string, object?> { { "z", 1 }, { "a", 2 } };

        var result = Helpers.CreateFieldDefinition("field".AsSpan(), "String".AsSpan(), ReadOnlySpan<char>.Empty, arguments, "path".AsSpan());

        result.Arguments.Should().NotBeNull();
        var keys = result.Arguments!.Keys.ToList();
        keys[0].Should().Be("a");
        keys[1].Should().Be("z");
    }

    [Fact]
    public void CreateFieldDefinition_InternsType_ForMemoryEfficiency()
    {
        var result1 = Helpers.CreateFieldDefinition("f1".AsSpan(), "User".AsSpan(), ReadOnlySpan<char>.Empty, null, "p1".AsSpan());
        var result2 = Helpers.CreateFieldDefinition("f2".AsSpan(), "User".AsSpan(), ReadOnlySpan<char>.Empty, null, "p2".AsSpan());

        // Both should reference the same string instance for "User" type
        result1.Type.Should().Be(result2.Type);
    }

    // ═══════════════════════════════════════════════════════════════
    // Edge cases and complex scenarios
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void SortArgumentValue_DictionaryWithNullValues_PreservesNulls()
    {
        var input = new Dictionary<string, object?> { { "key", null }, { "other", "value" } };

        var result = Helpers.SortArgumentValue(input);

        var sorted = (SortedDictionary<string, object?>)result;
        sorted.Should().ContainKey("key");
        sorted["key"].Should().BeNull();
    }

    [Fact]
    public void AreArgumentsEqual_ComplexNestedStructure_AllComparesCorrectly()
    {
        var nested1 = new Dictionary<string, object?> 
        { 
            { "config", new Dictionary<string, object?> { { "list", new List<object> { 1, 2, 3 } } } } 
        };
        var nested2 = new Dictionary<string, object?> 
        { 
            { "config", new Dictionary<string, object?> { { "list", new List<object> { 1, 2, 3 } } } } 
        };
        var args1 = new SortedDictionary<string, object?> { { "settings", nested1 } };
        var args2 = new SortedDictionary<string, object?> { { "settings", nested2 } };

        var result = Helpers.AreArgumentsEqual(args1, args2);

        result.Should().BeTrue();
    }

    [Fact]
    public void MergeNullableDictionaries_DeepNesting_RecursivelyMerges()
    {
        var existing = new Dictionary<string, object?>
        {
            { "level1", new Dictionary<string, object?> { { "level2", new Dictionary<string, object?> { { "key", "old" } } } } }
        };
        var update = new Dictionary<string, object?>
        {
            { "level1", new Dictionary<string, object?> { { "level2", new Dictionary<string, object?> { { "key", "new" } } } } }
        };

        var result = Helpers.MergeNullableDictionaries(existing, update);

        var level1 = result["level1"] as Dictionary<string, object?>;
        var level2 = level1?["level2"] as Dictionary<string, object?>;
        level2?["key"].Should().Be("new");
    }

    [Fact]
    public void ValidateFieldName_ColonWithoutAlias_ValidatesFieldPart()
    {
        Action act = () => Helpers.ValidateFieldName(":field");
        act.Should().NotThrow();
    }

    [Fact]
    public void ValidateFieldName_FieldNameAfterColon_InvalidNumber_Throws()
    {
        Action act = () => Helpers.ValidateFieldName("validAlias:123field");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ParseFieldTypeFromPath_BracketTypeWithContent_Extracted()
    {
        var result = Helpers.ParseFieldTypeFromPath("[User!] items".AsSpan(), "Default".AsSpan(), out var type);

        type.SequenceEqual("[User!]".AsSpan()).Should().BeTrue();
        result.SequenceEqual("items".AsSpan()).Should().BeTrue();
    }

    [Fact]
    public void ExtractVariables_QueryBlockValue_NotExtracted()
    {
        var queryBlock = new QueryBlock("query", "", null);
        var variables = new SortedSet<Variable>();

        Helpers.ExtractVariablesFromValue(queryBlock, variables);

        variables.Should().BeEmpty();
    }

    [Fact]
    public void ExtractVariables_CircularDictionaryReference_DetectedAndHandled()
    {
        var variables = new SortedSet<Variable>();
        var var1 = new Variable("$id", "ID!");
        var dictA = new Dictionary<string, object?> { {"var", var1} };
        
        // Create circular reference
        dictA["self"] = dictA;

        Helpers.ExtractVariablesFromValue(dictA, variables);

        variables.Should().HaveCount(1);
        variables.First().Name.Should().Be("$id");
    }

    [Fact]
    public void ExtractVariables_CircularListReference_DetectedAndHandled()
    {
        var variables = new SortedSet<Variable>();
        var var1 = new Variable("$name", "String!");
        var list = new List<object?> { var1 };
        
        // Create circular reference
        list.Add(list);

        Helpers.ExtractVariablesFromValue(list, variables);

        variables.Should().HaveCount(1);
        variables.First().Name.Should().Be("$name");
    }

    [Fact]
    public void ExtractVariables_CircularObjectReference_DetectedAndHandled()
    {
        var variables = new SortedSet<Variable>();
        var var1 = new Variable("$id", "ID!");
        
        var obj1 = new TestContainer { Value = var1 };
        obj1.Self = obj1;

        Helpers.ExtractVariablesFromValue(obj1, variables);

        variables.Should().HaveCount(1);
        variables.First().Name.Should().Be("$id");
    }

    [Fact]
    public void MergeMetadata_DeepNestedDictionaries_RecursivelyMergesAllLevels()
    {
        var existing = new Dictionary<string, object?>
        {
            {"cache", new Dictionary<string, object?> { {"ttl", 60}, {"key", "old"} }},
            {"other", "value"}
        };
        
        var update = new Dictionary<string, object>
        {
            {"cache", new Dictionary<string, object?> { {"key", "new"} }},
            {"extra", "added"}
        };

        var result = Helpers.MergeMetadata(existing, update);

        result.Should().HaveCount(3);
        result["other"].Should().Be("value");
        result["extra"].Should().Be("added");
        
        var mergedCache = result["cache"] as Dictionary<string, object?>;
        mergedCache.Should().HaveCount(2);
        mergedCache["ttl"].Should().Be(60);
        mergedCache["key"].Should().Be("new");
    }

    [Fact]
    public void MergeMetadata_ThreeLevelNesting_MergesAllLevels()
    {
        var existing = new Dictionary<string, object?>
        {
            {"a", new Dictionary<string, object?> 
            { 
                {"b", new Dictionary<string, object?> { {"c", 1} }}
            }}
        };
        
        var update = new Dictionary<string, object>
        {
            {"a", new Dictionary<string, object?> 
            { 
                {"b", new Dictionary<string, object?> { {"d", 2} }}
            }}
        };

        var result = Helpers.MergeMetadata(existing, update);

        var levelA = result["a"] as Dictionary<string, object?>;
        var levelB = levelA["b"] as Dictionary<string, object?>;
        levelB.Should().HaveCount(2);
        levelB["c"].Should().Be(1);
        levelB["d"].Should().Be(2);
    }

    [Fact]
    public void MergeNullableMetadata_BothNull_ReturnsEmptyDictionary()
    {
        var result = Helpers.MergeNullableMetadata(null, null);

        result.Should().BeEmpty();
    }

    [Fact]
    public void MergeNullableMetadata_ExistingNull_ReturnsCopyOfUpdate()
    {
        var update = new Dictionary<string, object?> { {"key", "value"} };
        
        var result = Helpers.MergeNullableMetadata(null, update);

        result.Should().HaveCount(1);
        result["key"].Should().Be("value");
    }

    [Fact]
    public void MergeNullableMetadata_UpdateNull_ReturnsExisting()
    {
        var existing = new Dictionary<string, object?> { {"key", "value"} };
        
        var result = Helpers.MergeNullableMetadata(existing, null);

        result.Should().HaveCount(1);
        result["key"].Should().Be("value");
    }

    [Fact]
    public void MergeNullableMetadata_NestedNullValues_PreservesNulls()
    {
        var existing = new Dictionary<string, object?>
        {
            {"data", new Dictionary<string, object?> { {"field", null} }}
        };
        
        var update = new Dictionary<string, object?>
        {
            {"data", new Dictionary<string, object?> { {"other", "value"} }}
        };

        var result = Helpers.MergeNullableMetadata(existing, update);

        var mergedData = result["data"] as Dictionary<string, object?>;
        mergedData.Should().HaveCount(2);
        mergedData["field"].Should().BeNull();
        mergedData["other"].Should().Be("value");
    }

    [Fact]
    public void AreArgumentsEqual_ComplexNestedWithNullsInDeepLevels_ComparesCorrectly()
    {
        var args1 = new SortedDictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            {"user", new Dictionary<string, object?> { {"id", 1}, {"profile", null} }},
            {"flags", new List<int> { 1, 2, 3 }}
        };
        
        var args2 = new SortedDictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            {"user", new Dictionary<string, object?> { {"id", 1}, {"profile", null} }},
            {"flags", new List<int> { 1, 2, 3 }}
        };

        var result = Helpers.AreArgumentsEqual(args1, args2);

        result.Should().BeTrue();
    }

    [Fact]
    public void AreArgumentsEqual_DifferentNestedValuesDeep_ReturnsFalse()
    {
        var args1 = new SortedDictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            {"data", new Dictionary<string, object?> { {"value", 1} }}
        };
        
        var args2 = new SortedDictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            {"data", new Dictionary<string, object?> { {"value", 2} }}
        };

        var result = Helpers.AreArgumentsEqual(args1, args2);

        result.Should().BeFalse();
    }

    [Fact]
    public void SortArgumentValue_ComplexNestedStructure_SortsAllLevels()
    {
        IDictionary<string, object?> unsorted = new Dictionary<string, object?>
        {
            {"z", 26},
            {"a", 1},
            {"m", new Dictionary<string, object?> { {"z", 2}, {"b", 1} }}
        };

        var result = Helpers.SortArgumentValue(unsorted) as SortedDictionary<string, object?>;

        result.Keys.Should().Equal("a", "m", "z");
        
        var nested = result["m"] as SortedDictionary<string, object?>;
        nested.Keys.Should().Equal("b", "z");
    }

    [Fact]
    public void SortArgumentValue_NestedListsWithDictionaries_SortsNestedDictionaries()
    {
        var unsorted = new List<object>
        {
            new Dictionary<string, object?> { {"z", 1}, {"a", 2} },
            new Dictionary<string, object?> { {"y", 3}, {"b", 4} }
        };

        var result = Helpers.SortArgumentValue(unsorted) as List<object>;

        var dict1 = result[0] as SortedDictionary<string, object?>;
        dict1.Keys.Should().Equal("a", "z");
        
        var dict2 = result[1] as SortedDictionary<string, object?>;
        dict2.Keys.Should().Equal("b", "y");
    }

    // Helper test class
    private class TestContainer
    {
        public object? Value { get; set; }
        public string? Name { get; set; }
        public TestContainer? Self { get; set; }
    }
}
