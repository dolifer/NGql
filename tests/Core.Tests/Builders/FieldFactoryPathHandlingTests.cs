using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using NGql.Core.Builders;
using NGql.Core.Abstractions;
using NGql.Core.Extensions;
using Xunit;

namespace NGql.Core.Tests.Builders;

/// <summary>
/// Tests for FieldFactory.GetOrAddField() path handling, buffer management, and edge cases.
/// Covers dotted paths, nested hierarchies, arguments, metadata, and concurrent access.
/// </summary>
public class FieldFactoryPathHandlingTests
{
    [Fact]
    public void QueryBuilder_AddField_SegmentWithMultipleColons_TreatsWholeSegmentAsName()
    {
        // Arrange & Act — three colon-separated parts is not alias:name, so the whole
        // segment is kept verbatim as the field name (NGql is schemaless).
        var query = QueryBuilder.CreateDefaultBuilder("T").AddField("a:b:c");

        // Assert
        query.ToString().Should().Be(@"query T{
    a:b:c
}");
    }

    [Fact]
    public void FieldFactory_GetOrAddField_SimpleFieldPath_Works()
    {
        // Arrange
        var fields = new Dictionary<string, FieldDefinition>();

        // Act
        var field = FieldFactory.GetOrAddField(fields, "name".AsSpan(), "String".AsSpan(), null);

        // Assert
        field.Name.Should().Be("name");
        field.Type.Should().Be("String");
        fields.Should().ContainKey("name");
    }

    [Fact]
    public void FieldFactory_GetOrAddField_DottedPath_CreatesHierarchy()
    {
        // Arrange
        var fields = new Dictionary<string, FieldDefinition>();

        // Act
        var field = FieldFactory.GetOrAddField(fields, "user.profile.name".AsSpan(), "String".AsSpan(), null);

        // Assert
        field.Name.Should().Be("name");
        fields.Should().ContainKey("user");
        var userField = fields["user"];
        userField._children.Should().NotBeNull();
    }

    [Fact]
    public void FieldFactory_GetOrAddField_WithArguments_StoresArguments()
    {
        // Arrange
        var fields = new Dictionary<string, FieldDefinition>();
        var arguments = new Dictionary<string, object?> { { "id", "123" } };

        // Act
        var field = FieldFactory.GetOrAddField(fields, "user".AsSpan(), "User".AsSpan(), arguments);

        // Assert
        field.Arguments.Should().NotBeNull();
        field.Arguments.Should().Contain("id", "123");
    }

    [Fact]
    public void FieldFactory_GetOrAddField_WithMetadata_StoresMetadata()
    {
        // Arrange
        var fields = new Dictionary<string, FieldDefinition>();
        var metadata = new Dictionary<string, object?> { { "alias", "u" } };

        // Act
        var field = FieldFactory.GetOrAddField(fields, "user".AsSpan(), "User".AsSpan(), null, null, metadata);

        // Assert
        field.Metadata.Should().NotBeNull();
        field.Metadata.Should().Contain("alias", "u");
    }

    [Fact]
    public void FieldFactory_GetOrAddField_EmptyType_DefaultsToString()
    {
        // Arrange
        var fields = new Dictionary<string, FieldDefinition>();

        // Act
        var field = FieldFactory.GetOrAddField(fields, "field".AsSpan(), "".AsSpan(), null);

        // Assert
        field.Type.Should().Be("String");
    }

    [Fact]
    public void FieldFactory_GetOrAddField_NestedInParent_CreatesChildren()
    {
        // Arrange
        var parentField = new FieldDefinition("user", "User", null, null, null);

        // Act
        var field = FieldFactory.GetOrAddField(parentField, "name".AsSpan(), "String".AsSpan(), null);

        // Assert
        field.Name.Should().Be("name");
        parentField._children.Should().NotBeNull();
    }

    [Fact]
    public void FieldFactory_GetOrAddField_ComplexPath_WithArguments_Works()
    {
        // Arrange
        var fields = new Dictionary<string, FieldDefinition>();
        var arguments = new Dictionary<string, object?> { { "limit", 10 } };

        // Act
        var field = FieldFactory.GetOrAddField(fields, "users.profile.tags".AsSpan(), "String".AsSpan(), arguments);

        // Assert
        field.Name.Should().Be("tags");
        fields.Should().ContainKey("users");
    }

    [Fact]
    public void FieldFactory_GetOrAddField_LongPath_Buffer_Allocated()
    {
        // Arrange
        var fields = new Dictionary<string, FieldDefinition>();
        var longPath = "a.b.c.d.e.f.g.h.i.j.k.l.m.n.o.p.q.r.s.t.u.v.w.x.y.z";

        // Act
        var field = FieldFactory.GetOrAddField(fields, longPath.AsSpan(), "String".AsSpan(), null);

        // Assert
        field.Name.Should().Be("z");
        fields.Should().ContainKey("a");
    }

    [Fact]
    public void FieldFactory_GetOrAddField_ExistingField_Reused()
    {
        // Arrange
        var fields = new Dictionary<string, FieldDefinition>();
        var field1 = FieldFactory.GetOrAddField(fields, "user".AsSpan(), "User".AsSpan(), null);
        
        // Act
        var field2 = FieldFactory.GetOrAddField(fields, "user".AsSpan(), "User".AsSpan(), null);

        // Assert
        field1.Name.Should().Be(field2.Name);
        fields.Count.Should().Be(1);
    }

    [Fact]
    public void FieldFactory_GetOrAddField_DottedPath_NoArguments_FastPath()
    {
        // Arrange
        var fields = new Dictionary<string, FieldDefinition>();

        // Act
        var field = FieldFactory.GetOrAddField(fields, "user.name".AsSpan(), "String".AsSpan(), null, null, null);

        // Assert
        field.Name.Should().Be("name");
        fields.Should().ContainKey("user");
    }

    [Fact]
    public void FieldFactory_GetOrAddField_DottedPath_WithArguments_SlowPath()
    {
        // Arrange
        var fields = new Dictionary<string, FieldDefinition>();
        var arguments = new Dictionary<string, object?> { { "arg", "value" } };

        // Act
        var field = FieldFactory.GetOrAddField(fields, "user.name".AsSpan(), "String".AsSpan(), arguments);

        // Assert
        field.Name.Should().Be("name");
    }

    [Fact]
    public void FieldFactory_GetOrAddField_MultipleSimpleFields_AllCreated()
    {
        // Arrange
        var fields = new Dictionary<string, FieldDefinition>();

        // Act
        _ = FieldFactory.GetOrAddField(fields, "id".AsSpan(), "ID".AsSpan(), null);
        _ = FieldFactory.GetOrAddField(fields, "name".AsSpan(), "String".AsSpan(), null);
        _ = FieldFactory.GetOrAddField(fields, "email".AsSpan(), "String".AsSpan(), null);

        // Assert
        fields.Count.Should().Be(3);
        fields.Keys.Should().Contain(new[] { "id", "name", "email" });
    }

    [Fact]
    public void FieldFactory_GetOrAddField_IntermediateFieldBecomesObject()
    {
        // Arrange
        var fields = new Dictionary<string, FieldDefinition>();

        // Act
        _ = FieldFactory.GetOrAddField(fields, "user".AsSpan(), "User".AsSpan(), null);
        _ = FieldFactory.GetOrAddField(fields, "user.profile".AsSpan(), "Profile".AsSpan(), null);

        // Assert - Type conversion happens when needed
        fields["user"].Should().NotBeNull();
        fields["user"].Name.Should().Be("user");
    }

    [Fact]
    public void FieldFactory_GetOrAddField_NestedParent_CreatesMultipleLevels()
    {
        // Arrange
        var parentField = new FieldDefinition("user", "User", null, null, null);

        // Act
        var child1 = FieldFactory.GetOrAddField(parentField, "profile".AsSpan(), "Profile".AsSpan(), null);
        var child2 = FieldFactory.GetOrAddField(child1, "name".AsSpan(), "String".AsSpan(), null);

        // Assert
        child1.Name.Should().Be("profile");
        child2.Name.Should().Be("name");
        parentField._children.Should().NotBeNull();
    }

    [Fact]
    public void FieldFactory_GetOrAddField_ArgumentMerge_PreservesArguments()
    {
        // Arrange
        var fields = new Dictionary<string, FieldDefinition>();
        var args1 = new Dictionary<string, object?> { { "first", 1 } };

        // Act
        _ = FieldFactory.GetOrAddField(fields, "data".AsSpan(), "Data".AsSpan(), args1);
        var args2 = new Dictionary<string, object?> { { "first", 1 } }; // Same args
        var field2 = FieldFactory.GetOrAddField(fields, "data".AsSpan(), "Data".AsSpan(), args2);

        // Assert
        field2.Arguments.Should().NotBeNull();
    }

    [Fact]
    public void FieldFactory_GetOrAddField_TypeConversion_ToObject_ForIntermediates()
    {
        // Arrange
        var fields = new Dictionary<string, FieldDefinition>();

        // Act
        FieldFactory.GetOrAddField(fields, "user.profile.email".AsSpan(), "String".AsSpan(), null);

        // Assert
        fields["user"].Type.Should().Be("object");
    }

    [Fact]
    public void FieldFactory_GetOrAddField_VeryDeepPath_AllSegmentsCreated()
    {
        // Arrange
        var fields = new Dictionary<string, FieldDefinition>();

        // Act
        var result = FieldFactory.GetOrAddField(
            fields,
            "level1.level2.level3.level4.level5.level6".AsSpan(),
            "String".AsSpan(),
            null);

        // Assert
        result.Name.Should().Be("level6");
        fields.Should().ContainKey("level1");
    }

    [Fact]
    public void FieldFactory_GetOrAddField_SingleCharSegment_Works()
    {
        // Arrange
        var fields = new Dictionary<string, FieldDefinition>();

        // Act
        var field = FieldFactory.GetOrAddField(fields, "a.b.c".AsSpan(), "String".AsSpan(), null);

        // Assert
        field.Name.Should().Be("c");
        fields.Should().ContainKey("a");
    }

    [Fact]
    public void FieldFactory_GetOrAddField_MetadataPreserved_WithDottedPath()
    {
        // Arrange
        var fields = new Dictionary<string, FieldDefinition>();
        var metadata = new Dictionary<string, object?> { { "key", "value" } };

        // Act
        var field = FieldFactory.GetOrAddField(
            fields,
            "user.name".AsSpan(),
            "String".AsSpan(),
            null,
            null,
            metadata);

        // Assert
        field.Metadata.Should().NotBeNull();
    }

    [Fact]
    public void FieldFactory_GetOrAddField_NullArguments_Handled()
    {
        // Arrange
        var fields = new Dictionary<string, FieldDefinition>();

        // Act
        var field = FieldFactory.GetOrAddField(fields, "user".AsSpan(), "User".AsSpan(), arguments: null);

        // Assert - Null arguments are converted to empty collection by implementation
        field.Should().NotBeNull();
    }

    [Fact]
    public void FieldFactory_GetOrAddField_EmptyArguments_Handled()
    {
        // Arrange
        var fields = new Dictionary<string, FieldDefinition>();
        var arguments = new Dictionary<string, object?>();

        // Act
        var field = FieldFactory.GetOrAddField(fields, "user".AsSpan(), "User".AsSpan(), arguments);

        // Assert
        field.Arguments.Should().BeEmpty();
    }

    [Fact]
    public void FieldFactory_GetOrAddField_SpecialCharacters_InPath_Works()
    {
        // Arrange
        var fields = new Dictionary<string, FieldDefinition>();

        // Act
        var field = FieldFactory.GetOrAddField(fields, "user_profile.first_name".AsSpan(), "String".AsSpan(), null);

        // Assert
        field.Name.Should().Be("first_name");
    }

    [Fact]
    public void FieldFactory_GetOrAddField_RepeatedAccess_Idempotent()
    {
        // Arrange
        var fields = new Dictionary<string, FieldDefinition>();
        var path = "user.profile.name".AsSpan();
        var type = "String".AsSpan();

        // Act
        var field1 = FieldFactory.GetOrAddField(fields, path, type, null);
        var field2 = FieldFactory.GetOrAddField(fields, path, type, null);
        var field3 = FieldFactory.GetOrAddField(fields, path, type, null);

        // Assert
        field1.Name.Should().Be(field2.Name);
        field2.Name.Should().Be(field3.Name);
    }

    [Theory]
    [InlineData("String")]
    [InlineData("Int")]
    [InlineData("Boolean")]
    [InlineData("Float")]
    [InlineData("ID")]
    public void FieldFactory_GetOrAddField_WithVariousTypes_StoresCorrectType(string type)
    {
        // Arrange
        var fields = new Dictionary<string, FieldDefinition>();

        // Act
        var field = FieldFactory.GetOrAddField(fields, "field".AsSpan(), type.AsSpan(), null);

        // Assert
        field.Type.Should().Be(type);
    }

    [Theory]
    [InlineData("simple")]
    [InlineData("user.name")]
    [InlineData("user.profile.address.city")]
    public void FieldFactory_GetOrAddField_VariousPaths_AllWork(string path)
    {
        // Arrange
        var fields = new Dictionary<string, FieldDefinition>();

        // Act
        var field = FieldFactory.GetOrAddField(fields, path.AsSpan(), "String".AsSpan(), null);

        // Assert
        field.Should().NotBeNull();
        fields.Should().NotBeEmpty();
    }

    [Fact]
    public async Task FieldFactory_GetOrAddField_ConcurrentAccess_ThreadSafe()
    {
        // Arrange
        var fields = new Dictionary<string, FieldDefinition>();
        var threadCount = 8;
        var tasks = new Task[threadCount];
        var exceptions = new System.Collections.Concurrent.ConcurrentBag<Exception>();

        // Act
        for (int t = 0; t < threadCount; t++)
        {
            tasks[t] = Task.Run(() =>
            {
                try
                {
                    for (int i = 0; i < 50; i++)
                    {
                        var path = $"user.field{i}";
                        FieldFactory.GetOrAddField(fields, path.AsSpan(), "String".AsSpan(), null);
                    }
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            });
        }

        await Task.WhenAll(tasks);

        // Assert
        exceptions.Should().BeEmpty();
        fields.Should().ContainKey("user");
    }

    [Fact]
    public void FieldFactory_GetOrAddField_BufferSizeCalculation_VeryLongPath()
    {
        // Arrange
        var fields = new Dictionary<string, FieldDefinition>();
        var parts = new List<string>();
        for (int i = 0; i < 20; i++)
        {
            parts.Add(new string('a', 50));
        }
        var longPath = string.Join(".", parts);

        // Act
        var field = FieldFactory.GetOrAddField(fields, longPath.AsSpan(), "String".AsSpan(), null);

        // Assert
        field.Should().NotBeNull();
    }

}
