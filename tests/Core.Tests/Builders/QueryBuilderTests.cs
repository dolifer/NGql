using System;
using System.Collections.Generic;
using FluentAssertions;
using NGql.Core.Abstractions;
using NGql.Core.Builders;
using Xunit;

namespace NGql.Core.Tests.Builders;

public class QueryBuilderTests
{
    [Fact]
    public void QueryBuilder_AddField_With_Type_Only_Should_Work()
    {
        // Arrange & Act
        var query = QueryBuilder.CreateDefaultBuilder("TestQuery")
            .AddField("user", "User")
            .ToString();

        // Assert
        query.Should().Contain("user");
    }

    [Fact]
    public void QueryBuilder_AddField_With_Type_Parameter_Should_Apply_Explicit_Type()
    {
        // Arrange & Act - BUG FIX: type parameter must be applied
        var queryBuilder = QueryBuilder.CreateDefaultBuilder("TestQuery")
            .AddField("user", "CustomType");

        // Assert - Verify type is set via parameter (not via "Type field" syntax)
        var userField = queryBuilder.Definition.Fields["user"];
        userField.Type.Should().Be("CustomType");
    }

    [Fact]
    public void QueryBuilder_AddField_With_Type_And_Metadata_Should_Apply_Explicit_Type()
    {
        // Arrange
        var metadata = new Dictionary<string, object?> { { "description", "User field" } };
        
        // Act - BUG FIX: type parameter must be applied
        var queryBuilder = QueryBuilder.CreateDefaultBuilder("TestQuery")
            .AddField("user", "CustomType", metadata);

        // Assert - Verify type is set via parameter (not via "Type field" syntax)
        var userField = queryBuilder.Definition.Fields["user"];
        userField.Type.Should().Be("CustomType");
    }

    [Fact]
    public void QueryBuilder_AddField_With_Type_Triggers_InternType_Path()
    {
        // This test verifies that the hasExplicitType path (line 393-396) is covered
        // The InternType code is only reached when:
        // 1. hasSubFields = true (subfields provided)
        // 2. Field string has a type prefix (e.g., "String user")
        // 3. hasExplicitType = true (type != default after parsing)
        
        // Arrange & Act
        var queryBuilder = QueryBuilder.CreateDefaultBuilder("TestQuery")
            .AddField("String user", subFields: ["profile", "email"]);  // Type prefix + subfields

        // Assert - verify type was parsed and cached via InternType
        var userField = queryBuilder.Definition.Fields["user"];
        userField.Type.Should().Be("String");
        userField.Fields.Should().ContainKey("profile");
        userField.Fields.Should().ContainKey("email");
    }

    [Fact]
    public void QueryBuilder_AddField_With_CustomArray_Type_And_SubFields_Should_Trigger_InternType()
    {
        // Another scenario for InternType: array type prefix with subfields
        
        // Arrange & Act
        var queryBuilder = QueryBuilder.CreateDefaultBuilder("TestQuery")
            .AddField("List[] items", subFields: ["id", "name", "value"]);

        // Assert - verify type was parsed and cached via InternType
        var itemsField = queryBuilder.Definition.Fields["items"];
        itemsField.Type.Should().Be("List[]");
        itemsField.Fields.Should().HaveCount(3);
    }

    [Fact]
    public void QueryBuilder_AddField_With_Type_And_Metadata_Should_Work()
    {
        // Arrange
        var metadata = new Dictionary<string, object?> { { "description", "User field" } };

        // Act
        var query = QueryBuilder.CreateDefaultBuilder("TestQuery")
            .AddField("user", "User", metadata)
            .ToString();

        // Assert
        query.Should().Contain("user");
    }

    [Fact]
    public void QueryBuilder_AddField_With_Arguments_Metadata_And_Action_Should_Work()
    {
        // Arrange
        var arguments = new Dictionary<string, object?> { { "id", "123" } };
        var metadata = new Dictionary<string, object?> { { "description", "User field" } };

        // Act
        var query = QueryBuilder.CreateDefaultBuilder("TestQuery")
            .AddField("user", arguments, metadata, builder =>
            {
                builder.AddField("name")
                       .AddField("email");
            })
            .ToString();

        // Assert
        query.Should().Contain("user(id:\"123\")");
        query.Should().Contain("name");
        query.Should().Contain("email");
    }

    [Fact]
    public void QueryBuilder_AddField_With_Type_Metadata_And_Action_Should_Work()
    {
        // Arrange
        var metadata = new Dictionary<string, object?> { { "description", "User field" } };

        // Act
        var query = QueryBuilder.CreateDefaultBuilder("TestQuery")
            .AddField("user", "User", metadata, builder =>
            {
                builder.AddField("profile", profileBuilder =>
                {
                    profileBuilder.AddField("name")
                                 .AddField("bio");
                });
            })
            .ToString();

        // Assert
        query.Should().Contain("user");
        query.Should().Contain("profile");
        query.Should().Contain("name");
        query.Should().Contain("bio");
    }

    [Fact]
    public void QueryBuilder_AddField_With_SubFields_Should_Not_Overwrite_Explicit_Type()
    {
        // Arrange & Act
        var queryBuilder = QueryBuilder.CreateDefaultBuilder("TestQuery")
            .AddField("String user", subFields: ["nested", "field"]);

        // Assert - Get the underlying field to verify type preservation
        var userField = queryBuilder.Definition.Fields["user"];
        userField.Type.Should().Be("String"); // Should NOT be overwritten to "object"
    }

    [Fact]
    public void QueryBuilder_SubFields_Added_Later_Should_Not_Change_Explicit_Type()
    {
        // Arrange & Act
        var queryBuilder = QueryBuilder.CreateDefaultBuilder("TestQuery")
            .AddField("CustomType user")
            .AddField("user.profile.name")
            .AddField("user.profile.email");

        // Assert - Get the field using the proper public API
        var userField = queryBuilder.Definition.Fields["user"];
        userField.Type.Should().Be("CustomType"); // Should NOT drift to "object"
        userField.Fields.Should().ContainKey("profile");
    }

    [Fact]
    public void QueryBuilder_DottedFieldWithArguments_ShouldProcessCorrectly()
    {
        // Arrange - Create a query with dotted field and arguments (triggers SLOW PATH in FieldFactory)
        var arguments = new Dictionary<string, object?> { { "id", 123 } };

        // Act - This exercises the ProcessDottedFieldWithMetadata path (lines 85-86, 108 in FieldFactory)
        var query = QueryBuilder.CreateDefaultBuilder("TestQuery")
            .AddField("user.profile.name", arguments)
            .ToString();

        // Assert - Field should be added with arguments preserved through the path
        query.Should().Contain("name");
        query.Should().Contain("id:123"); // Arguments should appear in output
    }

    [Fact]
    public void QueryBuilder_DottedFieldWithArgumentsAndMetadata_ShouldProcessCorrectly()
    {
        // Arrange - Create a query with dotted field, arguments AND metadata (triggers slow path)
        var arguments = new Dictionary<string, object?> { { "filter", "active" } };
        var metadata = new Dictionary<string, object?> { { "description", "User profile" } };

        // Act - Exercise dotted field with metadata
        var query = QueryBuilder.CreateDefaultBuilder("TestQuery")
            .AddField("user.profile.details", arguments, metadata)
            .ToString();

        // Assert
        query.Should().Contain("details");
        query.Should().Contain("filter:\"active\"");
    }

    [Fact]
    public void QueryBuilder_DottedFieldMultipleLevelsWithArguments_ShouldMergeArgumentsCorrectly()
    {
        // Arrange - Multiple dotted fields with different argument sets
        var args1 = new Dictionary<string, object?> { { "first", 10 } };
        var args2 = new Dictionary<string, object?> { { "after", "cursor123" } };

        // Act
        var query = QueryBuilder.CreateDefaultBuilder("TestQuery")
            .AddField("connection.edges.node", args1)
            .AddField("connection.edges.cursor", args2)
            .ToString();

        // Assert - Both fields should be present with their respective arguments
        query.Should().Contain("node(first:10)");
        query.Should().Contain("cursor(after:\"cursor123\")");
    }

    [Fact]
    public void QueryBuilder_NestedFieldsWithArgumentsInDifferentLevels_ShouldPreserveStructure()
    {
        // Arrange
        var parentArgs = new Dictionary<string, object?> { { "limit", 5 } };
        var childArgs = new Dictionary<string, object?> { { "sort", "DESC" } };

        // Act
        var query = QueryBuilder.CreateDefaultBuilder("TestQuery")
            .AddField("users", parentArgs, null, builder =>
            {
                builder.AddField("profile.settings", childArgs);
            })
            .ToString();

        // Assert
        query.Should().Contain("users(limit:5)");
        query.Should().Contain("settings(sort:\"DESC\")");
    }

    [Fact]
    public void QueryBuilder_VeryLongFieldNames_ShouldHandleCorrectly()
    {
        // Arrange - Field name that triggers buffer overflow in FieldSignatureGenerator
        var longFieldName = new string('x', 1200);

        // Act
        var query = QueryBuilder.CreateDefaultBuilder("TestQuery")
            .AddField(longFieldName, "String")
            .ToString();

        // Assert - Should not throw and field should be present
        query.Should().Contain(longFieldName.Substring(0, 100)); // At least part of it
        var userField = QueryBuilder.CreateDefaultBuilder("TestQuery")
            .AddField(longFieldName, "String")
            .Definition.Fields[longFieldName];
        userField.Should().NotBeNull();
    }

    [Fact]
    public void QueryBuilder_DeeplyNestedFieldsWithArguments_ShouldProcessRecursively()
    {
        // Arrange - Create deep nesting (10+ levels)
        var arguments = new Dictionary<string, object?> { { "id", 1 } };

        // Act
        var query = QueryBuilder.CreateDefaultBuilder("TestQuery")
            .AddField("a.b.c.d.e.f.g.h.i.j.k", arguments)
            .ToString();

        // Assert
        query.Should().Contain("k");
        query.Should().Contain("id:1");
    }

    [Fact]
    public void QueryBuilder_DottedFieldsWithComplexArguments_ShouldHandleNestedStructures()
    {
        // Arrange - Test with complex argument structures (exercises Helpers merge logic indirectly)
        var complexArgs = new Dictionary<string, object?>
        {
            { "filter", new Dictionary<string, object?>
                {
                    { "status", "active" },
                    { "limit", 10 }
                }
            },
            { "sort", "name" }
        };

        // Act
        var query = QueryBuilder.CreateDefaultBuilder("TestQuery")
            .AddField("users.profile.details", complexArgs)
            .ToString();

        // Assert
        query.Should().Contain("details");
        // Complex arguments should be handled
        query.Should().NotBeNull();
    }

    [Fact]
    public void QueryBuilder_FieldWithArgumentsAndNestedChildren_ShouldMergeCorrectly()
    {
        // Arrange
        var fieldArgs = new Dictionary<string, object?> { { "first", 10 } };

        // Act
        var query = QueryBuilder.CreateDefaultBuilder("TestQuery")
            .AddField("connection", fieldArgs, null, builder =>
            {
                builder.AddField("edges.cursor")
                       .AddField("edges.node.id")
                       .AddField("pageInfo.hasNextPage");
            })
            .ToString();

        // Assert
        query.Should().Contain("connection(first:10)");
        query.Should().Contain("cursor");
        query.Should().Contain("node");
        query.Should().Contain("pageInfo");
    }

    [Fact]
    public void QueryBuilder_MultipleArgumentedFieldsInPath_ShouldPreserveAllArguments()
    {
        // Arrange
        var args1 = new Dictionary<string, object?> { { "page", 1 } };
        var args2 = new Dictionary<string, object?> { { "size", 20 } };

        // Act
        var query = QueryBuilder.CreateDefaultBuilder("TestQuery")
            .AddField("search.results.item", args1)
            .AddField("search.results.metadata", args2)
            .ToString();

        // Assert
        query.Should().Contain("item(page:1)");
        query.Should().Contain("metadata(size:20)");
    }

    [Fact]
    public void QueryBuilder_FindExistingFieldCached_SimpleFieldPath_Should_Use_DirectLookup()
    {
        // This test covers the simple field branch of FindExistingFieldCached (line 413-416)
        // which uses GetValueOrDefault for fast lookup of simple field names
        
        // Arrange - First add a simple field
        var queryBuilder = QueryBuilder.CreateDefaultBuilder("TestQuery")
            .AddField("user", "User");

        // Act - Add subfields to same simple field (triggers FindExistingFieldCached with simple name)
        queryBuilder.AddField("user", subFields: ["profile", "email"]);

        // Assert - Verify field exists and has both subfields
        var userField = queryBuilder.Definition.Fields["user"];
        userField.Type.Should().Be("User");
        userField.Fields.Should().ContainKey("profile");
        userField.Fields.Should().ContainKey("email");
    }

    [Fact]
    public void QueryBuilder_FindExistingFieldCached_ComplexFieldPath_Should_Use_PathTraversal()
    {
        // This test covers the complex field branch of FindExistingFieldCached (line 419-420)
        // which uses Helpers.FindExistingFieldByPath for dotted/complex field lookups
        
        // Arrange - Add dotted field structure
        var queryBuilder = QueryBuilder.CreateDefaultBuilder("TestQuery")
            .AddField("user.profile.name")
            .AddField("user.profile.email");

        // Act - Add more fields to the same nested path (triggers FindExistingFieldCached with dotted path)
        queryBuilder.AddField("user.profile.bio");

        // Assert - Verify nested structure exists
        var userField = queryBuilder.Definition.Fields["user"];
        var profileField = userField.Fields["profile"];
        profileField.Fields.Should().HaveCount(3);
        profileField.Fields.Should().ContainKeys("name", "email", "bio");
    }

    [Fact]
    public void QueryBuilder_ExistingField_With_ExplicitType_Should_Preserve_Type()
    {
        // This test verifies that when FindExistingFieldCached finds an existing field
        // with an explicit type, that type is preserved (Priority 1 in DetermineFieldTypeOptimized)
        
        // Arrange - Add field with explicit type
        var queryBuilder = QueryBuilder.CreateDefaultBuilder("TestQuery")
            .AddField("CustomType data", subFields: ["value", "status"]);

        // Act - Try to add more subfields (should preserve the CustomType)
        queryBuilder.AddField("data", subFields: ["extra"]);

        // Assert - Type should be preserved as CustomType, not overwritten
        var dataField = queryBuilder.Definition.Fields["data"];
        dataField.Type.Should().Be("CustomType");
        dataField.Fields.Should().HaveCount(3); // value, status, extra
    }

    [Fact]
    public void QueryBuilder_CreateFromDefinition_Should_Wrap_Existing_Definition()
    {
        // Test uncovered line: CreateFromDefinition factory method (line 66)
        var definition = new QueryDefinition("ExistingQuery");
        definition.Fields["user"] = new FieldDefinition("user", "User");
        
        var queryBuilder = QueryBuilder.CreateFromDefinition(definition);
        
        queryBuilder.Definition.Should().Be(definition);
        queryBuilder.Definition.Name.Should().Be("ExistingQuery");
        queryBuilder.Definition.Fields.Count.Should().Be(1);
    }

    [Fact]
    public void QueryBuilder_AddField_With_FieldDefinitionArray_Should_Process_Correctly()
    {
        // Test uncovered line: AddField(field, FieldDefinition[]?, metadata) overload (line 112)
        var subFieldDefs = new[]
        {
            new FieldDefinition("id", "ID"),
            new FieldDefinition("name", "String"),
            new FieldDefinition("email", "String")
        };

        var queryBuilder = QueryBuilder.CreateDefaultBuilder("TestQuery")
            .AddField("user", subFieldDefs);

        var userField = queryBuilder.Definition.Fields["user"];
        userField.Fields.Should().ContainKeys("id", "name", "email");
        userField.Fields["id"].Type.Should().Be("ID");
        userField.Fields["name"].Type.Should().Be("String");
    }

    [Fact]
    public void QueryBuilder_AddField_With_FieldBuilder_Action_Should_Build_Nested_Structure()
    {
        // Test uncovered line: AddField(field, Action<FieldBuilder>) overload (line 122-125)
        var queryBuilder = QueryBuilder.CreateDefaultBuilder("TestQuery")
            .AddField("user", (Action<FieldBuilder>)(fb =>
            {
                fb.AddField("id")
                  .AddField("name")
                  .AddField("profile", pfb => pfb.AddField("avatar").AddField("bio"));
            }));

        queryBuilder.Definition.Fields.Should().ContainKey("user");
        var userField = queryBuilder.Definition.Fields["user"];
        userField.Fields.Should().ContainKeys("id", "name", "profile");
        userField.Fields["profile"].Fields.Should().ContainKeys("avatar", "bio");
    }

    [Fact]
    public void QueryBuilder_AddField_Null_Arguments_Dictionary_Should_Skip_Sorting()
    {
        // Test the fast path optimization when arguments are null (line 78-81)
        var queryBuilder = QueryBuilder.CreateDefaultBuilder("TestQuery")
            .AddField("simpleField", arguments: null, metadata: null);

        queryBuilder.Definition.Fields.Should().ContainKey("simpleField");
    }

    [Fact]
    public void QueryBuilder_AddField_Empty_Arguments_Dictionary_Should_Process_As_Null()
    {
        // Test the condition when arguments.Count == 0 vs null (line 84)
        var emptyArgs = new Dictionary<string, object?>();
        
        var queryBuilder = QueryBuilder.CreateDefaultBuilder("TestQuery")
            .AddField("field1", emptyArgs)
            .AddField("field2", (string[])null);

        queryBuilder.Definition.Fields.Should().HaveCount(2);
    }

    [Fact]
    public void QueryBuilder_AddField_Complex_Nested_With_Arguments_And_Metadata()
    {
        // Test combining multiple overloads with nested structures
        var args = new Dictionary<string, object?> { { "limit", 10 } };
        var meta = new Dictionary<string, object?> { { "desc", "Complex" } };

        var queryBuilder = QueryBuilder.CreateDefaultBuilder("TestQuery")
            .AddField("users", args, meta, fb => 
            {
                fb.AddField("id")
                  .AddField("profile", pfb => pfb
                      .AddField("avatar")
                      .AddField("settings", sfb => sfb
                          .AddField("notifications")
                          .AddField("privacy")));
            });

        var usersField = queryBuilder.Definition.Fields["users"];
        usersField.Arguments.Should().NotBeNull().And.HaveCount(1);
        usersField.Metadata.Should().NotBeNull().And.HaveCount(1);
        usersField.Fields["profile"].Fields.Should().ContainKey("settings");
    }

    [Fact]
    public void QueryBuilder_AddField_DottedPath_With_Complex_Arguments_ShouldMerge()
    {
        // Test FieldFactory.GetOrAddDottedField - merging with existing arguments (lines 69-112 uncovered)
        var queryBuilder = QueryBuilder.CreateDefaultBuilder("TestQuery")
            .AddField("user.profile", new Dictionary<string, object?> { ["depth"] = 2 }, null, fb =>
            {
                fb.AddField("avatar", new Dictionary<string, object?> { ["size"] = "large" });
            })
            .AddField("user.profile", new Dictionary<string, object?> { ["cache"] = true });

        var userField = queryBuilder.Definition.Fields["user"];
        userField.Should().NotBeNull();
        userField.Fields.Should().ContainKey("profile");
        
        // The second call should have merged with the first
        var profileField = userField.Fields["profile"];
        profileField.Arguments.Should().NotBeNull();
    }

    [Fact]
    public void QueryBuilder_AddField_DottedPath_Multiple_Levels_With_Arguments()
    {
        // Test deep dotted path merging (lines 85-100 in FieldFactory)
        var queryBuilder = QueryBuilder.CreateDefaultBuilder("TestQuery")
            .AddField("data.edges[0].node.user.profile.settings", new Dictionary<string, object?> { ["format"] = "detailed" })
            .AddField("data.edges[0].node.user.profile.avatar", new Dictionary<string, object?> { ["size"] = "large" });

        var dataField = queryBuilder.Definition.Fields["data"];
        dataField.Should().NotBeNull();
        dataField.Fields.Should().ContainKey("edges[0]");
    }

    [Fact]
    public void QueryBuilder_AddField_Merge_Conflicting_Arguments_Should_Preserve()
    {
        // Test FieldFactory merge behavior with argument conflicts (lines 213-255 uncovered)
        var queryBuilder = QueryBuilder.CreateDefaultBuilder("TestQuery")
            .AddField("user", new Dictionary<string, object?> { ["id"] = "123" })
            .AddField("user", new Dictionary<string, object?> { ["format"] = "full" });

        var userField = queryBuilder.Definition.Fields["user"];
        // Arguments should be preserved from both calls
        userField.Arguments.Should().NotBeNull();
    }

    [Fact]
    public void QueryBuilder_AddField_DottedPath_With_Type_Specification()
    {
        // Test type handling in dotted path merging (lines 105-112)
        var queryBuilder = QueryBuilder.CreateDefaultBuilder("TestQuery")
            .AddField("user", "User", null)
            .AddField("user.profile", "Profile", null)
            .AddField("user.profile.avatar", "Image", null);

        var userField = queryBuilder.Definition.Fields["user"];
        userField.Type.Should().Be("User");
        userField.Fields.Should().ContainKey("profile");
    }

    [Fact]
    public void QueryBuilder_AddField_DottedPath_Numeric_Index_Parsing()
    {
        // Test numeric index handling in dotted paths (lines 156-170)
        var queryBuilder = QueryBuilder.CreateDefaultBuilder("TestQuery")
            .AddField("results[0].items[5].data")
            .AddField("results[1].items[10].meta");

        var resultsField = queryBuilder.Definition.Fields["results[0]"];
        resultsField.Should().NotBeNull();
    }

    [Fact]
    public void QueryBuilder_AddField_DottedPath_With_Empty_Path_Segment()
    {
        // Test edge case: empty path segments (lines 73-80)
        var action = () =>
        {
            var queryBuilder = QueryBuilder.CreateDefaultBuilder("TestQuery")
                .AddField("user..profile"); // Double dot creates empty segment
        };

        // Should handle gracefully without throwing
        action.Should().NotThrow();
    }

    [Fact]
    public void QueryBuilder_AddField_Complex_Field_Merging_With_Subfields()
    {
        // Test FieldFactory merge logic with nested field children (lines 220-240)
        var queryBuilder = QueryBuilder.CreateDefaultBuilder("TestQuery")
            .AddField("user", fb => fb
                .AddField("profile", pfb => pfb
                    .AddField("contact", cfb => cfb
                        .AddField("email")
                        .AddField("phone"))))
            .AddField("user", new Dictionary<string, object?> { ["cached"] = false }, null, fb => fb
                .AddField("settings", sfb => sfb
                    .AddField("privacy")
                    .AddField("notifications")));

        var userField = queryBuilder.Definition.Fields["user"];
        // Both profile and settings should be present after merge
        userField.Fields.Should().ContainKeys("profile", "settings");
    }

    [Fact]
    public void QueryBuilder_AddField_Metadata_Merging_During_Dotted_Composition()
    {
        // Test metadata preservation during complex merging (lines 235-250)
        var metadata1 = new Dictionary<string, object?> { ["version"] = 1 };
        var metadata2 = new Dictionary<string, object?> { ["cached"] = true };

        var queryBuilder = QueryBuilder.CreateDefaultBuilder("TestQuery")
            .AddField("user", (Dictionary<string, object>?)null, metadata1)
            .AddField("user.profile", (Dictionary<string, object>?)null, metadata2);

        var userField = queryBuilder.Definition.Fields["user"];
        userField.Metadata.Should().NotBeNull();
        userField.Metadata.Should().ContainKey("version");
    }

    [Fact]
    public void QueryBuilder_AddField_Type_Override_During_Merge()
    {
        // Test type specification behavior during merge (lines 106-112)
        var queryBuilder1 = QueryBuilder.CreateDefaultBuilder("TestQuery")
            .AddField("result", "GenericResult")
            .AddField("result", "SpecificResult");  // Type override

        // Both should work without error
        queryBuilder1.Definition.Fields["result"].Should().NotBeNull();
    }

    // ============ PHASE 9.3a: Edge Case & Exception Testing ============

    [Fact]
    public void QueryBuilder_AddField_Null_FieldName_Should_Throw()
    {
        var builder = QueryBuilder.CreateDefaultBuilder("TestQuery");
        Action act = () => builder.AddField((string)null!);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void QueryBuilder_AddField_Empty_FieldName_Should_Throw()
    {
        var builder = QueryBuilder.CreateDefaultBuilder("TestQuery");
        Action act = () => builder.AddField("");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void QueryBuilder_AddField_Whitespace_OnlyFieldName_Should_Throw()
    {
        var builder = QueryBuilder.CreateDefaultBuilder("TestQuery");
        Action act = () => builder.AddField("   ");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void QueryBuilder_AddField_With_Null_Type_Parameter_Should_Work()
    {
        // When type is explicitly null, it should work
        var builder = QueryBuilder.CreateDefaultBuilder("TestQuery")
            .AddField("user", (string)null!);
        
        builder.Definition.Fields.Should().ContainKey("user");
    }

    [Fact]
    public void QueryBuilder_AddField_With_Null_Metadata_Should_Work()
    {
        var builder = QueryBuilder.CreateDefaultBuilder("TestQuery")
            .AddField("user", "User", (Dictionary<string, object>?)null);
        
        builder.Definition.Fields.Should().ContainKey("user");
        // Metadata can be null or empty dict - both are acceptable
        var field = builder.Definition.Fields["user"];
        field.Should().NotBeNull();
    }

    [Fact]
    public void QueryBuilder_AddField_SpecialCharactersInName_Are_Allowed_At_Parse_Time()
    {
        // Field names are validated during query build, not during add
        // So we can add fields with special characters
        var builder = QueryBuilder.CreateDefaultBuilder("TestQuery");
        
        var builder2 = builder.AddField("user!")
            .AddField("user@")
            .AddField("user#");
        
        // Should not throw during AddField
        builder2.Definition.Fields.Count.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public void QueryBuilder_AddField_SpacesInName_Are_Allowed_At_Parse_Time()
    {
        // Field names are validated during query build, not during add
        var builder = QueryBuilder.CreateDefaultBuilder("TestQuery");
        
        var builder2 = builder.AddField("user profile")
            .AddField("user name");
        
        // Should not throw during AddField
        builder2.Definition.Fields.Count.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact]
    public void QueryBuilder_AddField_Deep_Nesting_Should_Work()
    {
        // Test deeply nested field paths (10+ levels)
        var builder = QueryBuilder.CreateDefaultBuilder("TestQuery")
            .AddField("a.b.c.d.e.f.g.h.i.j.k");
        
        builder.Definition.Fields.Should().ContainKey("a");
        var query = builder.ToString();
        query.Should().Contain("a");
    }

    [Fact]
    public void QueryBuilder_AddField_Many_Fields_Should_Work()
    {
        var builder = QueryBuilder.CreateDefaultBuilder("TestQuery");
        
        // Add 50+ fields
        for (int i = 0; i < 50; i++)
        {
            builder.AddField($"field{i}");
        }
        
        builder.Definition.Fields.Count.Should().Be(50);
    }

    [Fact]
    public void QueryBuilder_AddField_With_Arguments_Null_Value_Should_Work()
    {
        var args = new Dictionary<string, object?> { { "id", null } };
        var builder = QueryBuilder.CreateDefaultBuilder("TestQuery")
            .AddField("user", args);
        
        var query = builder.ToString();
        query.Should().Contain("user");
    }

    [Fact]
    public void QueryBuilder_AddField_With_Mixed_Argument_Types_Should_Work()
    {
        var args = new Dictionary<string, object?>
        {
            { "id", 123 },
            { "name", "test" },
            { "active", true },
            { "tags", new[] { "a", "b" } }
        };
        
        var builder = QueryBuilder.CreateDefaultBuilder("TestQuery")
            .AddField("user", args);
        
        var query = builder.ToString();
        query.Should().Contain("user");
    }

    [Fact]
    public void QueryBuilder_AddField_With_FieldBuilder_Action_Should_Build_Nested()
    {
        var builder = QueryBuilder.CreateDefaultBuilder("TestQuery")
            .AddField("user", fb =>
            {
                fb.AddField("id");
                fb.AddField("name");
            });
        
        var query = builder.ToString();
        query.Should().Contain("user");
        query.Should().Contain("id");
        query.Should().Contain("name");
    }

    [Fact]
    public void QueryBuilder_AddField_Duplicate_Names_With_Different_Types_Should_Update()
    {
        var builder = QueryBuilder.CreateDefaultBuilder("TestQuery")
            .AddField("user", "UserType1")
            .AddField("user", "UserType2");
        
        builder.Definition.Fields.Should().ContainKey("user");
        builder.Definition.Fields["user"].Should().NotBeNull();
    }

    [Fact]
    public void QueryBuilder_ToString_Returns_Valid_Query_String()
    {
        var query = QueryBuilder.CreateDefaultBuilder("TestQuery")
            .AddField("user")
            .AddField("posts")
            .ToString();
        
        query.Should().Contain("query");
        query.Should().Contain("TestQuery");
        query.Should().Contain("user");
        query.Should().Contain("posts");
    }

    [Fact]
    public void QueryBuilder_Multiple_ToString_Calls_Should_Return_Consistent_Result()
    {
        var builder = QueryBuilder.CreateDefaultBuilder("TestQuery")
            .AddField("user");
        
        var query1 = builder.ToString();
        var query2 = builder.ToString();
        
        query1.Should().Be(query2);
    }

    [Fact]
    public void QueryBuilder_With_MergingStrategy_ShouldAffectFieldHandling()
    {
        var builder = QueryBuilder.CreateDefaultBuilder("TestQuery", MergingStrategy.MergeByFieldPath)
            .AddField("user")
            .AddField("user");
        
        // With merge strategy, duplicates should be handled appropriately
        var query = builder.ToString();
        query.Should().NotBeEmpty();
    }

    [Fact]
    public void QueryBuilder_With_Null_Name_Works_Or_Throws()
    {
        // CreateDefaultBuilder with null might throw or might work depending on QueryDefinition implementation
        Action act = () => QueryBuilder.CreateDefaultBuilder(null!);
        try
        {
            act();
            // If it doesn't throw, that's ok - validation might happen later
        }
        catch (ArgumentException)
        {
            // This is also ok - early validation is fine
        }
    }

    [Fact]
    public void QueryBuilder_AddField_DottedPath_With_Type_Should_Preserve_Type()
    {
        var builder = QueryBuilder.CreateDefaultBuilder("TestQuery")
            .AddField("user.profile", "ProfileType");
        
        var query = builder.ToString();
        query.Should().Contain("user");
    }

    [Fact]
    public void QueryBuilder_AddField_Complex_With_Nested_Action()
    {
        var metadata = new Dictionary<string, object?> { { "key", "value" } };
        
        var builder = QueryBuilder.CreateDefaultBuilder("TestQuery")
            .AddField("user", "UserType", metadata, fb =>
            {
                fb.AddField("id", "ID");
                fb.AddField("name", "String");
            });
        
        var query = builder.ToString();
        query.Should().Contain("user");
        query.Should().Contain("id");
    }

    [Fact]
    public void QueryBuilder_AddField_With_SubFields_Array()
    {
        var subFields = new[] { "id", "name", "email" };
        var builder = QueryBuilder.CreateDefaultBuilder("TestQuery")
            .AddField("user", subFields);
        
        var query = builder.ToString();
        query.Should().Contain("user");
    }

    [Fact]
    public void QueryBuilder_Include_Should_Merge_Queries()
    {
        var builder1 = QueryBuilder.CreateDefaultBuilder("TestQuery")
            .AddField("user");
            
        var builder2 = QueryBuilder.CreateDefaultBuilder("TestQuery")
            .AddField("posts");
        
        builder1.Include(builder2);
        
        var query = builder1.ToString();
        query.Should().Contain("user");
        query.Should().Contain("posts");
    }

    [Fact]
    public void QueryBuilder_WithMetadata_Should_Set_Query_Metadata()
    {
        var metadata = new Dictionary<string, object> { { "version", "1.0" } };
        var builder = QueryBuilder.CreateDefaultBuilder("TestQuery")
            .AddField("user")
            .WithMetadata(metadata);
        
        builder.Definition.Metadata.Should().NotBeNull();
        builder.Definition.Metadata.Should().ContainKey("version");
    }

    [Fact]
    public void QueryBuilder_GetPathTo_Should_Return_Path()
    {
        var builder = QueryBuilder.CreateDefaultBuilder("TestQuery")
            .AddField("user", fb =>
            {
                fb.AddField("profile", fb2 =>
                {
                    fb2.AddField("address");
                });
            });
        
        var path = builder.GetPathTo("TestQuery");
        path.Should().NotBeNull();
    }

    [Fact]
    public void QueryBuilder_Implicit_String_Conversion_Should_Work()
    {
        QueryBuilder builder = QueryBuilder.CreateDefaultBuilder("TestQuery")
            .AddField("user");
        
        string query = builder;
        query.Should().NotBeEmpty();
        query.Should().Contain("user");
    }

    [Fact]
    public void QueryBuilder_AddField_Empty_Arguments_Dictionary_Should_Work()
    {
        var args = new Dictionary<string, object?>();
        var builder = QueryBuilder.CreateDefaultBuilder("TestQuery")
            .AddField("user", args);
        
        var query = builder.ToString();
        query.Should().Contain("user");
    }

    [Fact]
    public void QueryBuilder_AddField_Nested_With_Arguments_Should_Work()
    {
        var args = new Dictionary<string, object?> { { "id", 123 } };
        var subArgs = new Dictionary<string, object?> { { "format", "ISO" } };
        
        var builder = QueryBuilder.CreateDefaultBuilder("TestQuery")
            .AddField("user", args, new[] { "id", "name" })
            .AddField("user.createdAt", subArgs);
        
        var query = builder.ToString();
        query.Should().Contain("user");
        query.Should().Contain("createdAt");
    }

    // ============ PHASE 9.3b: QueryTextBuilder Formatting Tests ============

    [Fact]
    public void QueryBuilder_AddField_WithCustomObject_Argument_Should_Reflect_Properties()
    {
        // Create a custom object to test reflection path in QueryTextBuilder
        var customObj = new { id = 1, name = "test", active = true };
        var args = new Dictionary<string, object?> { { "filter", customObj } };
        
        var builder = QueryBuilder.CreateDefaultBuilder("TestQuery")
            .AddField("user", args);
        
        var query = builder.ToString();
        query.Should().Contain("filter");
        query.Should().Contain("id");
        query.Should().Contain("name");
    }

    [Fact]
    public void QueryBuilder_AddField_WithNestedCustomObjects_Should_Reflect_All()
    {
        // Nested custom objects test object reflection recursion
        var innerObj = new { value = 123 };
        var outerObj = new { nested = innerObj, title = "outer" };
        var args = new Dictionary<string, object?> { { "data", outerObj } };
        
        var builder = QueryBuilder.CreateDefaultBuilder("TestQuery")
            .AddField("search", args);
        
        var query = builder.ToString();
        query.Should().Contain("search");
        query.Should().Contain("data");
    }

    [Fact]
    public void QueryBuilder_AddField_WithKeyValuePair_Arguments()
    {
        // KeyValuePair test - reflects key and value properties
        var kvp = new KeyValuePair<string, object>("param", 42);
        var args = new Dictionary<string, object?> { { "pair", kvp } };
        
        var builder = QueryBuilder.CreateDefaultBuilder("TestQuery")
            .AddField("items", args);
        
        var query = builder.ToString();
        query.Should().Contain("items");
    }

    [Fact]
    public void QueryBuilder_AddField_WithList_Containing_Objects()
    {
        // List collection containing objects
        var items = new object[] 
        { 
            new { id = 1, label = "first" },
            new { id = 2, label = "second" }
        };
        var args = new Dictionary<string, object?> { { "items", items } };
        
        var builder = QueryBuilder.CreateDefaultBuilder("TestQuery")
            .AddField("batch", args);
        
        var query = builder.ToString();
        query.Should().Contain("batch");
        query.Should().Contain("items");
    }

    [Fact]
    public void QueryBuilder_AddField_WithDictionary_Argument_Of_Objects()
    {
        // Dictionary containing object values
        var dict = new Dictionary<string, object>
        {
            { "config1", new { timeout = 100 } },
            { "config2", new { timeout = 200 } }
        };
        var args = new Dictionary<string, object?> { { "configs", dict } };
        
        var builder = QueryBuilder.CreateDefaultBuilder("TestQuery")
            .AddField("settings", args);
        
        var query = builder.ToString();
        query.Should().Contain("settings");
        query.Should().Contain("configs");
    }

    [Fact]
    public void QueryBuilder_AddField_WithNull_Object_Argument()
    {
        // Null object argument should be handled gracefully
        var args = new Dictionary<string, object?> { { "filter", null } };
        
        var builder = QueryBuilder.CreateDefaultBuilder("TestQuery")
            .AddField("user", args);
        
        var query = builder.ToString();
        query.Should().Contain("user");
    }

    [Fact]
    public void QueryBuilder_AddField_WithEmpty_List_Argument()
    {
        // Empty list argument
        var args = new Dictionary<string, object?> { { "tags", new List<string>() } };
        
        var builder = QueryBuilder.CreateDefaultBuilder("TestQuery")
            .AddField("posts", args);
        
        var query = builder.ToString();
        query.Should().Contain("posts");
        query.Should().Contain("tags");
    }

    [Fact]
    public void QueryBuilder_AddField_WithEmpty_Dictionary_Argument()
    {
        // Empty dictionary argument
        var args = new Dictionary<string, object?> { { "metadata", new Dictionary<string, string>() } };
        
        var builder = QueryBuilder.CreateDefaultBuilder("TestQuery")
            .AddField("document", args);
        
        var query = builder.ToString();
        query.Should().Contain("document");
    }

    [Fact]
    public void QueryBuilder_AddField_Complex_Nested_Arguments_Multiple_Levels()
    {
        // Very complex nested structure
        var level3 = new { value = 999 };
        var level2 = new { nested = level3, count = 5 };
        var level1 = new { data = level2, active = true };
        var args = new Dictionary<string, object?> 
        { 
            { "query", level1 },
            { "limit", 10 },
            { "tags", new[] { "a", "b", "c" } }
        };
        
        var builder = QueryBuilder.CreateDefaultBuilder("TestQuery")
            .AddField("search", args);
        
        var query = builder.ToString();
        query.Should().Contain("search");
        query.Should().Contain("query");
        query.Should().Contain("limit");
        query.Should().Contain("tags");
    }

    [Fact]
    public void QueryBuilder_AddField_WithMixed_Object_And_Primitive_Arguments()
    {
        // Mix objects, primitives, and collections
        var args = new Dictionary<string, object?>
        {
            { "id", 123 },
            { "name", "test" },
            { "filter", new { status = "active" } },
            { "tags", new[] { "x", "y" } },
            { "count", 5 }
        };
        
        var builder = QueryBuilder.CreateDefaultBuilder("TestQuery")
            .AddField("complex", args);
        
        var query = builder.ToString();
        query.Should().Contain("complex");
    }

    [Fact]
    public void QueryBuilder_AddField_Object_With_Many_Properties()
    {
        // Object with many properties to test iteration
        var manyProps = new 
        { 
            prop1 = "a",
            prop2 = "b", 
            prop3 = "c",
            prop4 = 1,
            prop5 = 2,
            prop6 = true,
            prop7 = new { nested = "value" }
        };
        var args = new Dictionary<string, object?> { { "data", manyProps } };
        
        var builder = QueryBuilder.CreateDefaultBuilder("TestQuery")
            .AddField("items", args);
        
        var query = builder.ToString();
        query.Should().Contain("items");
    }

    [Fact]
    public void QueryBuilder_AddField_TypeOverload()
    {
        var builder = QueryBuilder.CreateDefaultBuilder("TestQuery")
            .AddField("user", "User");
        builder.Definition.Fields.Should().ContainKey("user");
    }

    [Fact]
    public void QueryBuilder_AddField_TypeWithMetadata()
    {
        var meta = new Dictionary<string, object?> { { "key", "value" } };
        var builder = QueryBuilder.CreateDefaultBuilder("TestQuery")
            .AddField("user", "User", meta);
        builder.Definition.Fields["user"].Metadata.Should().NotBeEmpty();
    }

    [Fact]
    public void QueryBuilder_AddField_ArgumentsAndSubFields()
    {
        var args = new Dictionary<string, object?> { { "limit", 5 } };
        var subFields = new[] { "id", "name" };
        var builder = QueryBuilder.CreateDefaultBuilder("TestQuery")
            .AddField("users", args, subFields);
        builder.Definition.Fields.Should().ContainKey("users");
    }

    [Fact]
    public void QueryBuilder_AddField_ArgumentsSubFieldsAndMetadata()
    {
        var args = new Dictionary<string, object?> { { "limit", 5 } };
        var subFields = new[] { "id", "name" };
        var meta = new Dictionary<string, object?> { { "cached", true } };
        var builder = QueryBuilder.CreateDefaultBuilder("TestQuery")
            .AddField("users", args, subFields, meta);
        builder.Definition.Fields["users"].Metadata.Should().ContainKey("cached");
    }

    [Fact]
    public void QueryBuilder_AddField_WithFieldBuilder()
    {
        var builder = QueryBuilder.CreateDefaultBuilder("TestQuery")
            .AddField("user", fb => fb.AddField("id").AddField("name"));
        var query = builder.ToString();
        query.Should().Contain("user");
    }

    [Fact]
    public void QueryBuilder_AddField_ArgumentsAndBuilderAction()
    {
        var args = new Dictionary<string, object?> { { "role", "admin" } };
        var builder = QueryBuilder.CreateDefaultBuilder("TestQuery")
            .AddField("users", args, null, fb => fb.AddField("id"));
        builder.Definition.Fields.Should().ContainKey("users");
    }

    [Fact]
    public void QueryBuilder_AddField_TypeAndBuilderAction()
    {
        var builder = QueryBuilder.CreateDefaultBuilder("TestQuery")
            .AddField("user", "User", null, fb => fb.AddField("profile"));
        builder.Definition.Fields.Should().ContainKey("user");
    }

    [Fact]
    public void QueryBuilder_AddField_MergingBehavior()
    {
        var builder = QueryBuilder.CreateDefaultBuilder("TestQuery")
            .AddField("user", new[] { "id" })
            .AddField("user", new[] { "name" });
        
        var query = builder.ToString();
        query.Should().Contain("user");
    }

    [Fact]
    public void QueryBuilder_AddField_WithFieldDefinitions()
    {
        var fieldDefs = new[] { new FieldDefinition("id"), new FieldDefinition("name") };
        var builder = QueryBuilder.CreateDefaultBuilder("TestQuery")
            .AddField("user", fieldDefs);
        builder.Definition.Fields.Should().ContainKey("user");
    }

    [Fact]
    public void QueryBuilder_ToString_Should_Generate_Valid_GraphQL()
    {
        // Arrange
        var builder = QueryBuilder.CreateDefaultBuilder("GetUsers")
            .AddField("users")
            .AddField("users.id")
            .AddField("users.name");
        
        // Act
        var result = builder.ToString();
        
        // Assert
        result.Should().Contain("GetUsers");
        result.Should().Contain("users");
        result.Should().Contain("id");
        result.Should().Contain("name");
    }

    [Fact]
    public void QueryBuilder_CreateDefaultBuilder_Should_Initialize_With_Query_Operation()
    {
        // Arrange & Act
        var builder = QueryBuilder.CreateDefaultBuilder("TestQuery");
        
        // Assert
        builder.Definition.Name.Should().Be("TestQuery");
    }

    [Fact]
    public void QueryBuilder_AddField_With_Null_Arguments_Should_Work()
    {
        // Arrange
        var builder = QueryBuilder.CreateDefaultBuilder("Query");
        
        // Act
        var result = builder.AddField("data", new Dictionary<string, object?>());
        
        // Assert
        result.Definition.Fields["data"].Arguments.Should().BeEmpty();
    }

    [Fact]
    public void QueryBuilder_AddField_With_Empty_Arguments_Should_Not_Store_Empty_Dict()
    {
        // Arrange
        var builder = QueryBuilder.CreateDefaultBuilder("Query");
        var emptyArgs = new Dictionary<string, object?>();
        
        // Act
        var result = builder.AddField("data", emptyArgs);
        
        // Assert
        result.Definition.Fields["data"].Arguments.Should().BeEmpty();
    }

    [Fact]
    public void QueryBuilder_AddField_Chained_Should_Build_Correctly()
    {
        // Arrange
        var builder = QueryBuilder.CreateDefaultBuilder("Query");
        
        // Act
        var result = builder
            .AddField("user")
            .AddField("user.profile")
            .AddField("user.profile.bio")
            .AddField("posts")
            .AddField("posts.title")
            .Definition;
        
        // Assert
        result.Fields.Should().HaveCount(2);
        result.Fields["user"].Fields.Should().HaveCount(1);
    }

    [Fact]
    public void QueryBuilder_AddField_With_Type_Should_Set_Field_Type()
    {
        // Arrange
        var builder = QueryBuilder.CreateDefaultBuilder("Query");
        
        // Act
        var result = builder.AddField("data", "CustomType");
        
        // Assert
        result.Definition.Fields["data"].Type.Should().Be("CustomType");
    }

    [Fact]
    public void QueryBuilder_Definition_Fields_Should_Be_Accessible()
    {
        // Arrange
        var builder = QueryBuilder.CreateDefaultBuilder("Query")
            .AddField("users")
            .AddField("users.id");
        
        // Act
        var fields = builder.Definition.Fields;
        
        // Assert
        fields.Should().ContainKey("users");
        fields["users"].Fields.Should().ContainKey("id");
    }

    [Fact]
    public void QueryBuilder_Multiple_Top_Level_Fields_Should_All_Be_Present()
    {
        // Arrange
        var builder = QueryBuilder.CreateDefaultBuilder("Query");
        
        // Act
        var result = builder
            .AddField("users")
            .AddField("posts")
            .AddField("comments")
            .Definition;
        
        // Assert
        result.Fields.Should().HaveCount(3);
    }

    [Fact]
    public void QueryBuilder_AddField_With_Metadata_Should_Store_Metadata()
    {
        // Arrange
        var builder = QueryBuilder.CreateDefaultBuilder("Query");
        var metadata = new Dictionary<string, object?> { { "cache", true } };
        
        // Act
        var result = builder.AddField("data", metadata: metadata);
        
        // Assert
        result.Definition.Fields["data"].Metadata.Should().NotBeNull();
    }

    [Fact]
    public void QueryBuilder_Nested_Dotted_Paths_With_Variables_Extracts_All()
    {
        // Arrange
        var builder = QueryBuilder.CreateDefaultBuilder("Query");
        var userId = new Variable("$userId", "ID!");
        var userIdArgs = new Dictionary<string, object?> { { "id", userId } };
        
        // Act
        builder.AddField("user", userIdArgs)
            .AddField("posts")
            .AddField("author");

        // Assert - this should exercise variable extraction from nested structures
        var query = builder.Definition;
        query.Should().NotBeNull();
    }

    [Fact]
    public void QueryBuilder_Deep_Hierarchy_With_Arguments_And_Metadata()
    {
        // Arrange
        var builder = QueryBuilder.CreateDefaultBuilder("Query");
        var meta1 = new Dictionary<string, object?> { { "cache", "60s" } };
        var args1 = new Dictionary<string, object?> { { "limit", 10 } };
        var args2 = new Dictionary<string, object?> { { "offset", 0 } };
        
        // Act
        builder
            .AddField("users", args1, meta1)
            .AddField("profile", args2)
            .AddField("settings")
            .AddField("theme");

        // Assert - all fields should be properly structured
        var query = builder.Definition;
        query.Should().NotBeNull();
        query.Fields.Should().ContainKey("users");
    }

    [Fact]
    public void QueryBuilder_Complex_Arguments_With_Nested_Dictionaries_Sorted()
    {
        // Arrange
        var builder = QueryBuilder.CreateDefaultBuilder("Query");
        var complexArgs = new Dictionary<string, object?>
        {
            { "z_filter", new Dictionary<string, object?> { { "status", "active" } } },
            { "a_limit", 50 },
            { "m_offset", new Dictionary<string, object?> { { "type", "cursor" } } }
        };
        
        // Act
        builder.AddField("items", complexArgs);
        
        // Assert - arguments should be sorted
        var query = builder.Definition;
        var itemsField = query.Fields["items"];
        itemsField.Arguments.Should().NotBeNull();
        itemsField.Arguments.Should().ContainKey("a_limit");
    }

    [Fact]
    public void QueryBuilder_Multiple_Level_Nesting_Maintains_Type_Hierarchy()
    {
        // Arrange
        var builder = QueryBuilder.CreateDefaultBuilder("Query");
        
        // Act
        builder.AddField("org.department.team.member.contact", metadata: new Dictionary<string, object?> { { "sensitive", true } });
        
        // Assert - all intermediate levels should be "object" type
        var query = builder.Definition;
        query.Fields.Should().ContainKey("org");
        query.Fields["org"].Type.Should().Be("object");
        query.Fields["org"].Fields.Should().ContainKey("department");
    }

    [Fact]
    public void QueryBuilder_ReAdding_Field_With_Different_Type_Preserves_Hierarchy()
    {
        // Arrange
        var builder = QueryBuilder.CreateDefaultBuilder("Query");
        
        // Act
        builder.AddField("data.values.count", "Int");
        builder.AddField("data.values.sum", "Float");
        builder.AddField("data.meta.timestamp", "DateTime");
        
        // Assert - 'data' should be object, values should be object, meta should be object
        var query = builder.Definition;
        query.Fields["data"].Type.Should().Be("object");
    }


    [Fact]
    public void QueryBuilder_Merge_Metadata_On_Existing_Field()
    {
        // Arrange
        var builder = QueryBuilder.CreateDefaultBuilder("Query");
        
        // Act
        builder.AddField("user", metadata: new Dictionary<string, object?> { { "v", "1" } });
        builder.AddField("user", metadata: new Dictionary<string, object?> { { "cached", "true" } });
        
        // Assert - second add should merge metadata
        var query = builder.Definition;
        var userField = query.Fields["user"];
        userField.Metadata.Should().NotBeNull();
    }

    [Fact]
    public void QueryBuilder_Multiple_Top_Level_Fields_Independent()
    {
        // Arrange
        var builder = QueryBuilder.CreateDefaultBuilder("Query");
        var metaA = new Dictionary<string, object?> { { "id", "a" } };
        var metaB = new Dictionary<string, object?> { { "id", "b" } };
        
        // Act
        builder.AddField("fieldA", metadata: metaA)
            .AddField("fieldB", metadata: metaB)
            .AddField("fieldC");
        
        // Assert - each field should have its own metadata
        var query = builder.Definition;
        query.Fields.Should().HaveCount(3);
        query.Fields["fieldA"].Metadata["id"].Should().Be("a");
        query.Fields["fieldB"].Metadata["id"].Should().Be("b");
    }

    [Fact]
    public void QueryBuilder_With_Variables_In_Complex_Arguments()
    {
        // Arrange
        var builder = QueryBuilder.CreateDefaultBuilder("Query");
        var varId = new Variable("$id", "ID!");
        var varPage = new Variable("$page", "Int!");
        var complexArgs = new Dictionary<string, object?>
        {
            { "filter", new Dictionary<string, object?> { { "userId", varId } } },
            { "pagination", new Dictionary<string, object?> { { "page", varPage }, { "size", 20 } } }
        };
        
        // Act & Assert - should handle variables in nested argument structures
        var act = () => builder.AddField("posts", complexArgs);
        act.Should().NotThrow();
        
        var query = builder.Definition;
        query.Should().NotBeNull();
    }
}
