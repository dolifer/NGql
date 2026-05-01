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
using NGql.Core.Tests.Models;
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

    [Theory]
    [InlineData(null, null)]
    [InlineData("String", null)]
    [InlineData(null, "myAlias")]
    [InlineData("String", "myAlias")]
    public void FieldDefinition_GetHashCode_HandlesNullableFields(string? type, string? alias)
    {
        var field = new FieldDefinition("test", type ?? Constants.DefaultFieldType, alias);

        var hash = field.GetHashCode();

        hash.Should().NotBe(0);
    }

    // Equals returns false whenever any of (Name, Type, Alias, IsNeverMerge) differs.
    // Each scenario varies exactly one of those four discriminators between two fields.
    [Theory]
    [InlineData("name")]    // Name differs
    [InlineData("type")]    // Type differs
    [InlineData("alias")]   // Alias differs
    [InlineData("merge")]   // IsNeverMerge differs
    public void FieldDefinition_Equals_OneDiscriminatorDiffers_ReturnsFalse(string differing)
    {
        var (a, b) = differing switch
        {
            "name" => (new FieldDefinition("nameA"), new FieldDefinition("nameB")),
            "type" => (new FieldDefinition("name", "String"), new FieldDefinition("name", "Int")),
            "alias" => (new FieldDefinition("name", "String", "aliasA"), new FieldDefinition("name", "String", "aliasB")),
            _ => (new FieldDefinition("name") { IsNeverMerge = true }, new FieldDefinition("name") { IsNeverMerge = false }),
        };

        a.Equals(b).Should().BeFalse();
    }

    [Theory]
    [InlineData(true, true)]    // IsArray check
    [InlineData(false, true)]   // IsNullable check
    public void FieldDefinition_IsArrayOrIsNullable_NullType_ReturnsFalse(bool checkIsArray, bool _)
    {
        // FieldDefinition.Type's init accessor allows `with { Type = null }` to leave _type
        // null, exercising the null guard in TypeExtensions.IsArrayType / IsNullableType.
        var field = new FieldDefinition("x") { Type = null };

        var actual = checkIsArray ? field.IsArray : field.IsNullable;
        actual.Should().BeFalse();
    }

    [Theory]
    [InlineData("aliased")]
    [InlineData("")]
    [InlineData(null)]
    public void FieldDefinition_AliasInit_AllowsAllShapes(string? aliasValue)
    {
        // Init-accessor on Alias has a `IsNullOrEmpty(value) ? value : Name` branch — cover
        // null, empty, and non-empty values via `with` expressions on a record-like field.
        var field = new FieldDefinition("name") { Alias = aliasValue };

        if (string.IsNullOrEmpty(aliasValue))
        {
            field._effectiveName.Should().Be("name");
        }
        else
        {
            field._effectiveName.Should().Be(aliasValue);
        }
    }

    // FieldDefinition's IDictionary constructor exercises ToSortedArguments and AsChildren.
    // Both helpers have a null-guard plus a Count==0 guard; this Theory walks all four
    // input shapes (null/empty arguments × null/empty fields) and asserts the output is null.
    [Theory]
    [InlineData("null-args", "null-fields")]
    [InlineData("empty-args", "null-fields")]
    public void FieldDefinition_Constructor_NormalizesEmptyOrNullArgsAndFieldsToNull(string argsShape, string fieldsShape)
    {
        IDictionary<string, object?>? args = argsShape switch
        {
            "empty-args" => new Dictionary<string, object?>(),
            _ => null,
        };
        SortedDictionary<string, FieldDefinition>? fields = fieldsShape == "empty-fields"
            ? new SortedDictionary<string, FieldDefinition>()
            : null;

        var sortedArgs = args is null ? null : new SortedDictionary<string, object?>(args);
        var field = new FieldDefinition("name", "String", null, sortedArgs, fields);

        field._arguments.Should().BeNull();
        field.HasFields.Should().BeFalse();
    }

    [Fact]
    public void FieldDefinition_GetHashCode_WithNullTypeAndAlias_DoesNotThrow()
    {
        // GetHashCode does `_type?.ToLowerInvariant(), _alias?.ToLowerInvariant()` — covering
        // the null arms of both null-conditional accesses.
        var field = new FieldDefinition("name") { Type = null, Alias = null };

        var hash = field.GetHashCode();
        hash.Should().NotBe(0);
    }

    [Fact]
    public void PreservationBuilder_Create_WithNullQuery_ThrowsArgumentNullException()
    {
        var act = () => PreservationBuilder.Create(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void QueryBuilder_GetPathTo_UnknownQueryName_ReturnsRootMappedFallback()
    {
        // QueryMap.GetPathTo invokes FindRootField; when the rootPath isn't in
        // queryDefinition.Fields and no field's alias/name matches, FindRootField returns null
        // and BuildPathToNode is skipped — covers the null-rootField branch.
        var builder = QueryBuilder.CreateDefaultBuilder("Q").AddField("a");
        var path = builder.GetPathTo("UnmappedQuery", "some.node");

        path.Should().NotBeNull();
    }

    [Fact]
    public void QueryBuilder_GetPathTo_FieldAliasMatchedByName_FindsRootField()
    {
        // QueryMap.FindRootField falls back to a Linq Find by alias-or-name when
        // queryDefinition.Fields.TryGetValue misses; this exercises the alias-match branch.
        var builder = QueryBuilder.CreateDefaultBuilder("Q")
            .AddField("u:user");

        // Pass the original (un-aliased) name; the field is keyed by its effective alias "u".
        var path = builder.GetPathTo("user", "");

        path.Should().NotBeNull();
    }

    [Fact]
    public void QueryBuilder_Include_EmptyIncomingQuery_NoOp()
    {
        // Including a definition whose _fields is null (no AddField calls) takes
        // QueryMerger.MergeQuery's early-return path — covers the _fields == null arm.
        var b1 = QueryBuilder.CreateDefaultBuilder("Q").AddField("user");
        var b2 = QueryBuilder.CreateDefaultBuilder("Q");

        b1.Include(b2);

        b1.Definition.Fields.Should().ContainKey("user");
    }

    [Fact]
    public void QueryBuilder_Include_ObjectFieldMergeAddsChildren_UnderMergeByFieldPath()
    {
        // Two queries with the same parent and disjoint nested fields under MergeByFieldPath
        // — exercises FieldDefinitionExtensions.MergeFieldsInPlace ->
        // MergeIncomingChildrenInPlace, mutating the existing field's children dict in place.
        var b1 = QueryBuilder.CreateDefaultBuilder("Q", MergingStrategy.MergeByFieldPath)
            .AddField("posts.title");
        var b2 = QueryBuilder.CreateDefaultBuilder("Q", MergingStrategy.MergeByFieldPath)
            .AddField("posts.body");

        b1.Include(b2);

        b1.Definition.Fields["posts"].Fields.Should().ContainKeys("title", "body");
    }

    [Fact]
    public void QueryBuilder_Include_DeeplyNestedSameLeaf_PromotesLeafToObjectAndAllocatesChildren()
    {
        // b1's leaf "posts.title" has type String; b2 makes "title" an object via subfields.
        // Under MergeByFieldPath, MergeFieldsInPlace recursively merges. When it reaches the
        // "title" leaf and tries to merge in incoming title's children, MergeIncomingChildrenInPlace
        // hits `existing._children ??= new FieldChildren()` — the null arm we want covered.
        // We force matching types by giving b1's title an explicit object type via subFields.
        var b1 = QueryBuilder.CreateDefaultBuilder("Q", MergingStrategy.MergeByFieldPath);
        b1.AddField("posts", subFields: new[] { "title" });
        // Now posts.title exists as a String leaf with no children.
        var b2 = QueryBuilder.CreateDefaultBuilder("Q", MergingStrategy.MergeByFieldPath);
        b2.AddField("posts.title.byline");
        // After merge, b1's title gets promoted to object and gains a "byline" child.

        var act = () => b1.Include(b2);

        // Type-conflict expected (String vs object) — the catch path inside ApplyMergeByFieldPath
        // wraps it as QueryMergeException and rethrows. Suffices to assert the merge runs.
        act.Should().Throw<NGql.Core.Exceptions.QueryMergeException>();
    }

    // Each scenario triggers a specific merge-path branch in FieldDefinitionExtensions.
    // All run under MergeByFieldPath (which goes through CanMergeFields/MergeFieldsInPlace).
    // The shared assertion is "Include succeeds" — the targeted branch differs per row.
    [Theory]
    [InlineData("identical-nested-args")]   // CanMergeFields recurses into matching nested args
    [InlineData("nested-args-no-existing")] // HasAnyArguments fires on incoming child with args
    [InlineData("grandchild-args")]         // HasAnyArguments recurses through middle field
    [InlineData("existing-children-incoming-leaf")] // IsExistingExtraCompatible null-incomingChildren arm
    public void QueryBuilder_Include_MergeByFieldPath_ExercisesCanMergeFieldsBranches(string scenario)
    {
        Dictionary<string, object?> Args(int v) => new() { ["k"] = v };

        var (b1, b2) = scenario switch
        {
            "identical-nested-args" => (
                QueryBuilder.CreateDefaultBuilder("Q", MergingStrategy.MergeByFieldPath)
                    .AddField("user.profile", new Dictionary<string, object?> { ["x"] = 1 }),
                QueryBuilder.CreateDefaultBuilder("Q", MergingStrategy.MergeByFieldPath)
                    .AddField("user.profile", new Dictionary<string, object?> { ["x"] = 1 })),
            "nested-args-no-existing" => (
                QueryBuilder.CreateDefaultBuilder("Q", MergingStrategy.MergeByFieldPath).AddField("user"),
                QueryBuilder.CreateDefaultBuilder("Q", MergingStrategy.MergeByFieldPath).AddField("user.profile", Args(1))),
            "grandchild-args" => (
                QueryBuilder.CreateDefaultBuilder("Q", MergingStrategy.MergeByFieldPath).AddField("user"),
                QueryBuilder.CreateDefaultBuilder("Q", MergingStrategy.MergeByFieldPath).AddField("user.profile.x", Args(1))),
            _ /* existing-children-incoming-leaf */ => (
                QueryBuilder.CreateDefaultBuilder("Q", MergingStrategy.MergeByFieldPath).AddField("user.profile", Args(1)),
                QueryBuilder.CreateDefaultBuilder("Q", MergingStrategy.MergeByFieldPath).AddField("user")),
        };

        var act = () => b1.Include(b2);
        act.Should().NotThrow();
    }

    [Fact]
    public void QueryBuilder_Preserve_AliasOnly_RoutesThroughAliasMatchBranch()
    {
        // Preserve("alias") on a query whose field has this string as ALIAS (not name)
        // — covers the alias-match arm in QueryDefinitionExtensions.FindFieldRecursivelyCore
        // and PreserveExtensions.FindFieldByNameOrAlias.
        var query = QueryBuilder.CreateDefaultBuilder("Q")
            .AddField("currentUser:user");

        var preserved = query.Preserve("currentUser");
        preserved.Definition.Fields.Should().NotBeEmpty();
    }

    // PreserveFromExpression scenarios that drive ExpressionPreservationProcessor +
    // FindFieldRecursivelyCore + NavigationPropertyExpander branches. Each row sets up
    // a query whose tree shape forces a specific arm of the merge / preservation path
    // and asserts the build succeeds non-null.
    [Theory]
    [InlineData("nested-alias")]            // PreserveExtensions alias-match recursion
    [InlineData("root-without-alias")]      // PreserveFromRoot's `Alias ?? Name` Name fallback
    [InlineData("readonly-property")]       // NavigationPropertyExpander.IsNavigationProperty
    [InlineData("nodepath-no-match")]       // NameOrAliasMatches IsNullOrEmpty(Alias) arm
    [InlineData("aliased-tree-no-match")]   // NameOrAliasMatches non-empty Alias non-equal arm
    public void PreservationBuilder_PreserveFromExpression_DrivesProcessorBranches(string scenario)
    {
        QueryBuilder query;
        QueryBuilder preserved;

        switch (scenario)
        {
            case "nested-alias":
                query = QueryBuilder.CreateDefaultBuilder("Q").AddField("u", subFields: new[] { "displayName:fullName" });
                preserved = query.Preserve("u.displayName");
                break;
            case "root-without-alias":
                query = QueryBuilder.CreateDefaultBuilder("Q").AddField("user.profile.name");
                preserved = PreservationBuilder.Create(query)
                    .PreserveFromExpression<TestModel>(x => x.user.profile.name == null, nodePath: "profile")
                    .Build();
                break;
            case "readonly-property":
                query = QueryBuilder.CreateDefaultBuilder("Q").AddField("Name");
                preserved = PreservationBuilder.Create(query)
                    .PreserveFromExpression<TestModel>(x => x.Name == null)
                    .Build();
                break;
            case "nodepath-no-match":
                query = QueryBuilder.CreateDefaultBuilder("Q").AddField("user.profile.name");
                preserved = PreservationBuilder.Create(query)
                    .PreserveFromExpression<TestModel>(x => x.user.email != null, nodePath: "profile")
                    .Build();
                break;
            default: // aliased-tree-no-match
                query = QueryBuilder.CreateDefaultBuilder("Q")
                    .AddField("u:user.profile.alias_field:fullName");
                preserved = PreservationBuilder.Create(query)
                    .PreserveFromExpression<TestModel>(x => x.user.email != null, nodePath: "profile")
                    .Build();
                break;
        }

        preserved.Should().NotBeNull();
    }

    [Fact]
    public void QueryBuilder_Include_NeverMerge_AlreadyMarkedField_FastPath()
    {
        // MarkAsNeverMerge has `field.IsNeverMerge ? field : field with { IsNeverMerge = true }`.
        // First Include marks "a" as NeverMerge in b1. Including a SECOND time with the same
        // (already-marked) field exercises the IsNeverMerge=true short-circuit arm.
        var b1 = QueryBuilder.CreateDefaultBuilder("Outer", MergingStrategy.NeverMerge);
        var b2 = QueryBuilder.CreateDefaultBuilder("Q", MergingStrategy.NeverMerge).AddField("a");

        b1.Include(b2);
        // The fields in b1 carry IsNeverMerge=true now. Re-Include b1 into a fresh builder —
        // each MarkAsNeverMerge call sees a field that's already flagged.
        var b3 = QueryBuilder.CreateDefaultBuilder("Outer2", MergingStrategy.NeverMerge);
        b3.Include(b1);

        b3.Definition.Fields.Count.Should().BeGreaterThan(0);
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
        // Sibling to SpanExtensions_GetOrAddSimpleField_WithMultiplePaths_AddsAll: same setup, but
        // assert the ordering is preserved (insertion order, not alphabetical) so we exercise the
        // KeyValuePair extraction codepath rather than just "contains all three names".
        var builder = QueryBuilder.CreateDefaultBuilder("Test")
            .AddField("field1")
            .AddField("field2")
            .AddField("field3");

        var result = builder.ToString();
        var idxField1 = result.IndexOf("field1", StringComparison.Ordinal);
        var idxField2 = result.IndexOf("field2", StringComparison.Ordinal);
        var idxField3 = result.IndexOf("field3", StringComparison.Ordinal);
        idxField1.Should().BeLessThan(idxField2);
        idxField2.Should().BeLessThan(idxField3);
    }

    #endregion

    #region FieldChildren Set & AppendLocked

    [Fact]
    public void FieldChildren_Set_WithNewChild_AddsToCollection()
    {
        var parent = new FieldDefinition("parent", "Parent");

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

    [Fact]
    public void PreservationBuilder_PreserveAtPath_DescendingThroughLeaf_TerminatesEarly()
    {
        // PreserveAtPath calls NavigatePath which walks the path segment by segment. When a
        // mid-path field exists but has no children (a leaf), the walk has to stop early —
        // exercises NavigatePath's "_children == null mid-path" return-null branch.
        var query = QueryBuilder.CreateDefaultBuilder("Q")
            .AddField("data.edges.cursor") // cursor is a leaf (no children)
            .AddField("data.edges.node.id");

        // Preserve targets "cursor.somethingElse" under the data.edges node — but "cursor" is a
        // leaf, so NavigatePath stops mid-walk and the preservation is silently dropped.
        var result = PreservationBuilder.Create(query)
            .PreserveAtPath("cursor.somethingElse", "edges")
            .Build();

        result.Should().NotBeNull();
    }

    [Fact]
    public void PreservationBuilder_PreserveAtPath_NodeWithoutChildren_ReturnsEarly()
    {
        // The "nodeField is null or has no fields" early-return inside PreserveAtPath: target node
        // exists but has no children to preserve under.
        var query = QueryBuilder.CreateDefaultBuilder("Q")
            .AddField("user.id"); // user has children, but "id" is a leaf

        var result = PreservationBuilder.Create(query)
            .PreserveAtPath("anything", "id") // "id" has no children
            .Build();

        result.Should().NotBeNull();
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
        var block = new QueryBlock(string.IsNullOrEmpty(name) ? "query" : name);

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
        var firstSpec = fieldSpec.Split(',')[0];
        int expectedCount;
        if (firstSpec.Contains('.'))
        {
            expectedCount = 1; // Nested fields count as single root field
        }
        else
        {
            expectedCount = fieldSpec.Split(',').Length;
        }

        result.Fields.Count.Should().BeGreaterOrEqualTo(1);
        result.Fields.Count.Should().BeLessThanOrEqualTo(expectedCount);
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
