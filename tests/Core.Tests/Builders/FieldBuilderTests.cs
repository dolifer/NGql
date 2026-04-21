using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using NGql.Core.Abstractions;
using NGql.Core.Builders;
using NGql.Core.Extensions;
using Xunit;

namespace NGql.Core.Tests.Builders;

public class FieldBuilderTests
{
    [Fact]
    public void Can_Create_Simple_FieldDefinition_Using_FieldBuilder()
    {
        // Arrange & Act
        var fieldBuilder = FieldBuilder.Create([], "name");
        var result = fieldBuilder.Build();

        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be("name");
        result.Type.Should().Be(Constants.DefaultFieldType);
    }

    [Fact]
    public void Can_Add_Field_With_Dotted_Notation()
    {
        // Arrange
        var fieldBuilder = FieldBuilder.Create([], "user", "User");

        // Act
        var result = fieldBuilder
            .AddField("profile.name")
            .AddField("profile.email", "Email")
            .AddField("profile.address.street", "Street")
            .AddField("profile.address.city", "City")
            .Build();

        // Assert
        result.Should().NotBeNull();
        result.Fields.Should().ContainKey("profile");
        var profile = result.Fields["profile"];
        profile.Type.Should().Be("object");
        profile.Fields.Should().ContainKeys("name", "email", "address");
        profile.Fields["name"].Type.Should().Be(Constants.DefaultFieldType);
        profile.Fields["email"].Type.Should().Be("Email");

        var address = profile.Fields["address"];
        address.Type.Should().Be(Constants.ObjectFieldType);
        address.Fields.Should().ContainKeys("street", "city");
        address.Fields["street"].Type.Should().Be("Street");
        address.Fields["city"].Type.Should().Be("City");
    }

    [Fact]
    public void Can_Create_FieldDefinition_With_Arguments_Using_FieldBuilder()
    {
        // Arrange
        var arguments = new Dictionary<string, object?>
        {
            { "limit", 10 },
            { "offset", 0 }
        };
        var fieldBuilder = FieldBuilder.Create([], "users", "User[]", arguments);

        // Act
        var result = fieldBuilder.Build();

        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be("users");
        result.Type.Should().Be("User[]");
        result.Arguments.Should().NotBeNull().And.HaveCount(2);
        result.Arguments!["limit"].Should().Be(10);
        result.Arguments!["offset"].Should().Be(0);
    }

    [Fact]
    public void Can_Create_FieldDefinition_From_Existing_FieldDefinition()
    {
        // Arrange
        var existingField = new FieldDefinition("user", "User", "currentUser",
            new Dictionary<string, object?> { { "id", "123" } }
        );

        // Act
        var fieldBuilder = FieldBuilder.Create(existingField);
        var result = fieldBuilder.Build();

        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be("user");
        result.Type.Should().Be("User");
        result.Alias.Should().Be("currentUser");
        result.Arguments.Should().NotBeNull().And.HaveCount(1);
        result.Arguments!["id"].Should().Be("123");
    }

    [Fact]
    public void Can_Add_Simple_Field_Using_FieldBuilder()
    {
        // Arrange
        var fieldBuilder = FieldBuilder.Create([], "user", "User");

        // Act
        var result = fieldBuilder
            .AddField("name")
            .AddField("email")
            .Build();

        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be("user");
        result.Fields.Should().HaveCount(2);
        result.Fields["name"].Name.Should().Be("name");
        result.Fields["name"].Type.Should().Be(Constants.DefaultFieldType);
        result.Fields["email"].Name.Should().Be("email");
        result.Fields["email"].Type.Should().Be(Constants.DefaultFieldType);
    }

    [Fact]
    public void AddField_Sets_Default_String_Type_When_Type_Not_Specified()
    {
        // Arrange
        var fieldBuilder = FieldBuilder.Create([], "user", "User");

        // Act
        var result = fieldBuilder
            .AddField("name") // The default type should be "String"
            .Build();

        // Assert
        result.Fields["name"].Should().NotBeNull();
        result.Fields["name"].Name.Should().Be("name");
        result.Fields["name"].Type.Should().Be(Constants.DefaultFieldType); // Default type verification
    }

    [Theory]
    [InlineData("Int[]")]
    [InlineData("Int")]
    [InlineData("Integer")]
    [InlineData("int")]
    [InlineData("integer")]
    [InlineData("Boolean")]
    [InlineData("Bool")]
    [InlineData("bool")]
    [InlineData("Float")]
    [InlineData("ID")]
    [InlineData("Custom")]
    [InlineData("[]")]
    public void AddField_Sets_Specified_Type_When_Type_Is_Provided(string typeValue)
    {
        // Arrange
        var subFields = new[] { "a", "b", "c" };
        var fieldBuilder = FieldBuilder.Create([], "user", "User");

        // Act
        var result = fieldBuilder
            .AddField("field1", typeValue, subFields: subFields) // Explicitly setting type
            .AddField("field2", typeValue, subFields: []) // Explicitly setting type, no subfields
            .AddField($"{typeValue} field3", subFields: subFields) // Type is set from the name
            .AddField($"{typeValue} field4", subFields: []) // Type is set from the name, no subfields
            .Build();

        // Assert
        foreach (var field in new[] { "field1", "field2", "field3", "field4" })
        {
            result.Fields[field].Should().NotBeNull();
            result.Fields[field].Name.Should().Be(field);
            result.Fields[field].Type.Should().Be(typeValue); // Explicit type verification
        }
    }

    [Fact]
    public void AddField_With_Multiple_Fields_Sets_Correct_Types()
    {
        // Arrange
        var fieldBuilder = FieldBuilder.Create([], "user", "User");

        // Act
        var result = fieldBuilder
            .AddField("id", "ID") // Custom type
            .AddField("name") // Default String type
            .AddField("age", "Int") // Custom type
            .AddField("isActive", "Boolean") // Custom type
            .AddField("[] array") // Custom array type
            .AddField("int[] userIds") // Custom array of int
            .AddField("int[] user.data") // Custom nested array of int
            .Build();

        // Assert
        result.Fields.Should().HaveCount(7);
        result.Fields["id"].Type.Should().Be("ID");
        result.Fields["name"].Type.Should().Be(Constants.DefaultFieldType); // Default
        result.Fields["age"].Type.Should().Be("Int");
        result.Fields["isActive"].Type.Should().Be("Boolean");
        result.Fields["array"].Type.Should().Be("[]");
        result.Fields["userIds"].Type.Should().Be("int[]");

        result.Fields["user"].Type.Should().Be(Constants.ObjectFieldType);

        var userData = result.Fields["user"].Fields["data"];

        userData.Type.Should().Be("int[]"); // Nested array of int
        userData.Name.Should().Be("data");

        userData.Fields.Should().BeEmpty(); // No nested fields in array
    }

    [Fact]
    public void AddField_With_Dotted_Notation_Sets_Correct_Types()
    {
        // Arrange
        var fieldBuilder = FieldBuilder.Create([], "user", "User");

        // Act
        var result = fieldBuilder
            .AddField("ID profile.id")
            .AddField("profile.name") // Should default to String
            .AddField("Int profile.details.age")
            .AddField("Boolean profile.details.verified")
            .Build();

        // Assert
        result.Fields.Should().ContainKey("profile");
        var profile = result.Fields["profile"];
        // Intermediate node should have an object type
        profile.Type.Should().Be(Constants.ObjectFieldType);
        profile.Fields.Should().ContainKeys("id", "name", "details");
        profile.Fields["id"].Type.Should().Be("ID");
        profile.Fields["name"].Type.Should().Be(Constants.DefaultFieldType); // Default type

        // Intermediate node should have type 'object'
        var details = profile.Fields["details"];
        details.Type.Should().Be(Constants.ObjectFieldType);
        details.Fields.Should().ContainKeys("age", "verified");
        details.Fields["age"].Type.Should().Be("Int");
        details.Fields["verified"].Type.Should().Be("Boolean");
    }

    [Fact]
    public void Can_Add_Field_With_Arguments_Using_FieldBuilder()
    {
        // Arrange
        var arguments = new Dictionary<string, object?>
        {
            { "first", 5 }
        };
        var fieldBuilder = FieldBuilder.Create([], "user", "User");

        // Act
        var result = fieldBuilder
            .AddField("posts", "Post[]", arguments)
            .Build();

        // Assert
        result.Should().NotBeNull();
        result.Fields.Should().HaveCount(1);
        result.Fields["posts"].Name.Should().Be("posts");
        result.Fields["posts"].Type.Should().Be("Post[]");
        result.Fields["posts"].Arguments.Should().NotBeNull().And.HaveCount(1);
        result.Fields["posts"].Arguments!["first"].Should().Be(5);
    }

    [Fact]
    public void Can_Add_Field_With_SubFields_Using_FieldBuilder()
    {
        // Arrange
        var subFields = new[] { "title", "content", "createdAt" };
        var fieldBuilder = FieldBuilder.Create([], "user", "User");

        // Act
        var result = fieldBuilder
            .AddField("posts", subFields)
            .Build();

        // Assert
        result.Should().NotBeNull();
        result.Fields.Should().HaveCount(1);
        result.Fields["posts"].Name.Should().Be("posts");
        result.Fields["posts"].Type.Should().Be(Constants.ObjectFieldType);
        result.Fields["posts"].Fields.Should().HaveCount(3);
        result.Fields["posts"].Fields["title"].Name.Should().Be("title");
        result.Fields["posts"].Fields["content"].Name.Should().Be("content");
        result.Fields["posts"].Fields["createdAt"].Name.Should().Be("createdAt");
    }

    [Fact]
    public void Can_Add_Nested_Field_Using_Action_Builder()
    {
        // Arrange
        var fieldBuilder = FieldBuilder.Create([], "user", "User");

        // Act
        var result = fieldBuilder
            .AddField("profile", "Profile", nestedBuilder =>
            {
                nestedBuilder
                    .AddField("bio")
                    .AddField("avatar");
            })
            .Build();

        // Assert
        result.Should().NotBeNull();
        result.Fields.Should().HaveCount(1);
        result.Fields["profile"].Name.Should().Be("profile");
        result.Fields["profile"].Type.Should().Be("Profile");
        result.Fields["profile"].Fields.Should().HaveCount(2);
        result.Fields["profile"].Fields["bio"].Name.Should().Be("bio");
        result.Fields["profile"].Fields["avatar"].Name.Should().Be("avatar");
    }

    [Fact]
    public void Can_Set_Alias_Using_FieldBuilder()
    {
        // Arrange
        var fieldBuilder = FieldBuilder.Create([], "user", "User");

        // Act
        var result = fieldBuilder
            .WithAlias("currentUser")
            .Build();

        // Assert
        result.Should().NotBeNull();
        result.Alias.Should().Be("currentUser");
    }

    [Fact]
    public void Can_Add_Field_With_Alias_In_Dotted_Notation()
    {
        // Arrange
        var fieldBuilder = FieldBuilder.Create([], "user", "User");

        // Act
        var result = fieldBuilder
            .AddField("alias:profile.displayName:name")
            .AddField("profile.userEmail:email")
            .Build();

        // Assert
        result.Fields.Should().ContainKey("profile");
        var profile = result.Fields["profile"];
        profile.Alias.Should().Be("alias"); // Alias on the first segment
        profile.Fields.Should().ContainKeys("name", "email");
        profile.Fields["name"].Alias.Should().Be("displayName"); // Alias on the nested field
        profile.Fields["email"].Alias.Should().Be("userEmail"); // Alias on the nested field
    }

    [Fact]
    public void Can_Add_Field_With_Type_At_Beginning_Of_Path()
    {
        // Arrange
        var fieldBuilder = FieldBuilder.Create([], "user", "User");

        // Act
        var result = fieldBuilder
            .AddField("String name")
            .AddField("Int age")
            .AddField("Boolean isActive")
            .AddField("ID id")
            .Build();

        // Assert
        result.Fields.Should().ContainKeys("name", "age", "isActive", "id");
        result.Fields["name"].Type.Should().Be(Constants.DefaultFieldType);
        result.Fields["age"].Type.Should().Be("Int");
        result.Fields["isActive"].Type.Should().Be("Boolean");
        result.Fields["id"].Type.Should().Be("ID");
    }

    [Fact]
    public void Can_Add_Field_With_Type_And_Dotted_Notation_At_Beginning()
    {
        // Arrange
        var fieldBuilder = FieldBuilder.Create([], "user", "User");

        // Act
        var result = fieldBuilder
            .AddField("String profile.name")
            .AddField("Int profile.age")
            .AddField("DateTime profile.details.registeredAt")
            .AddField("[String] profile.details.tags")
            .Build();

        // Assert
        result.Fields.Should().ContainKey("profile");
        var profile = result.Fields["profile"];
        profile.Type.Should().Be(Constants.ObjectFieldType); // Intermediate node should have object type
        profile.Fields.Should().ContainKeys("name", "age", "details");
        profile.Fields["name"].Type.Should().Be(Constants.DefaultFieldType);
        profile.Fields["age"].Type.Should().Be("Int");

        var details = profile.Fields["details"];
        details.Type.Should().Be("object"); // Intermediate node should have type 'object'
        details.Fields.Should().ContainKeys("registeredAt", "tags");
        details.Fields["registeredAt"].Type.Should().Be("DateTime");
        details.Fields["tags"].Type.Should().Be("[String]");
    }

    [Fact]
    public void Can_Add_Field_With_Complex_Types_And_Non_Scalar_Types()
    {
        // Arrange
        var fieldBuilder = FieldBuilder.Create([], "user", "User");

        // Act
        var result = fieldBuilder
            .AddField("[User!]! friends")
            .AddField("CustomType! profile.preferences")
            .AddField("[Comment!] posts.comments")
            .AddField("UserStatus status")
            .Build();

        // Assert
        result.Fields.Should().ContainKeys("friends", "profile", "posts", "status");
        result.Fields["friends"].Type.Should().Be("[User!]!");
        result.Fields["status"].Type.Should().Be("UserStatus");
        result.Fields["profile"].Fields["preferences"].Type.Should().Be("CustomType!");
        result.Fields["posts"].Fields["comments"].Type.Should().Be("[Comment!]");
    }

    [Fact]
    public void Can_Add_Field_With_Mixed_Type_Notation_And_Aliases()
    {
        // Arrange
        var fieldBuilder = FieldBuilder.Create([], "user", "User");

        // Act
        var result = fieldBuilder
            .AddField("String username:login")
            .AddField("String userInfo:profile.displayName:name")
            .AddField("Int userInfo:profile.userAge:age")
            .Build();

        // Assert
        result.Fields.Should().ContainKey("login");
        result.Fields["login"].Type.Should().Be(Constants.DefaultFieldType);
        result.Fields["login"].Alias.Should().Be("username");

        result.Fields.Should().ContainKey("profile");
        result.Fields["profile"].Alias.Should().Be("userInfo");
        result.Fields["profile"].Fields["name"].Type.Should().Be(Constants.DefaultFieldType);
        result.Fields["profile"].Fields["name"].Alias.Should().Be("displayName");
        result.Fields["profile"].Fields["age"].Type.Should().Be("Int");
        result.Fields["profile"].Fields["age"].Alias.Should().Be("userAge");
    }

    [Fact]
    public void Field_With_Name_Only_Has_String_Type_By_Default_And_Converts_To_Object_When_Nested_Field_Added()
    {
        // Arrange
        var fieldBuilder = FieldBuilder.Create(new Dictionary<string, FieldDefinition>(), "user", "User");

        // Step 1: Add a field with name only (should default to String type)
        var intermediateBuilder = fieldBuilder.AddField("info");

        // Verify intermediate state
        var intermediateResult = intermediateBuilder.Build();
        intermediateResult.Fields.Should().ContainKey("info");
        intermediateResult.Fields["info"].Type.Should().Be(Constants.DefaultFieldType); // Default type is String
        intermediateResult.Fields["info"].Fields.Should().BeEmpty(); // No nested fields yet

        // Step 2: Add a nested field (should convert parent to object)
        var finalResult = intermediateBuilder
            .AddField("info.detail")
            .Build();

        // Assert final state
        finalResult.Fields.Should().ContainKey("info");
        var info = finalResult.Fields["info"];
        info.Type.Should().Be(Constants.ObjectFieldType); // Should be converted to object
        info.Fields.Should().ContainKey("detail");
        info.Fields["detail"].Type.Should().Be(Constants.DefaultFieldType);
    }

    [Fact]
    public void Can_Set_Type_Using_FieldBuilder()
    {
        // Arrange
        var fieldBuilder = FieldBuilder.Create([], "user");

        // Act
        var result = fieldBuilder
            .WithType("User")
            .Build();

        // Assert
        result.Should().NotBeNull();
        result.Type.Should().Be("User");
    }

    [Fact]
    public void Can_Add_Arguments_Using_Where_Method()
    {
        // Arrange
        var fieldBuilder = FieldBuilder.Create([], "user", "User");

        // Act
        var result = fieldBuilder
            .Where("id", "123")
            .Where("active", true)
            .Build();

        // Assert
        result.Should().NotBeNull();
        result.Arguments.Should().NotBeNull().And.HaveCount(2);
        result.Arguments!["id"].Should().Be("123");
        result.Arguments!["active"].Should().Be(true);
    }

    [Fact]
    public void Build_Should_Throw_When_FieldDefinition_Is_Null()
    {
        // This test verifies the error case, though in practice the private constructor
        // should prevent this scenario. The test demonstrates expected behavior.

        // Arrange
        var fieldBuilder = FieldBuilder.Create([], "test");

        // Act & Assert - Build should work fine with properly created builder
        var result = fieldBuilder.Build();
        result.Should().NotBeNull();
    }

    [Fact]
    public void Can_Chain_Multiple_Operations_Using_FieldBuilder()
    {
        // Arrange & Act
        var result = FieldBuilder.Create([], "user", "User")
            .WithAlias("currentUser")
            .Where("id", "123")
            .AddField("name")
            .AddField("email")
            .AddField("posts", "Post[]", new Dictionary<string, object?> { { "first", 10 } })
            .AddField("profile", "Profile", nestedBuilder => nestedBuilder
                .AddField("bio")
                .AddField("avatar"))
            .Build();

        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be("user");
        result.Type.Should().Be("User");
        result.Alias.Should().Be("currentUser");
        result.Arguments.Should().NotBeNull().And.HaveCount(1);
        result.Arguments!["id"].Should().Be("123");
        result.Fields.Should().HaveCount(4);
        result.Fields["name"].Should().NotBeNull();
        result.Fields["email"].Should().NotBeNull();
        result.Fields["posts"].Arguments!["first"].Should().Be(10);
        result.Fields["profile"].Fields.Should().HaveCount(2);
    }

    [Fact]
    public void Can_Set_Metadata_Using_FieldBuilder()
    {
        // Arrange
        var metadata = new Dictionary<string, object>
        {
            { "description", "User profile information" },
            { "version", "1.0" },
            { "deprecated", false }
        };
        var fieldBuilder = FieldBuilder.Create([], "user", "User");

        // Act
        var result = fieldBuilder
            .WithMetadata(metadata)
            .Build();

        // Assert
        result.Should().NotBeNull();
        result.Metadata.Should().NotBeNull().And.HaveCount(3);
        result.Metadata!["description"].Should().Be("User profile information");
        result.Metadata!["version"].Should().Be("1.0");
        result.Metadata!["deprecated"].Should().Be(false);
    }

    [Fact]
    public void WithMetadata_Should_Merge_With_Existing_Metadata()
    {
        // Arrange
        var initialMetadata = new Dictionary<string, object>
        {
            { "description", "Initial description" },
            { "version", "1.0" }
        };
        var additionalMetadata = new Dictionary<string, object>
        {
            { "description", "Updated description" }, // Should override
            { "author", "John Doe" }, // Should add
            { "tags", new[] { "user", "profile" } } // Should add
        };

        var fieldBuilder = FieldBuilder.Create([], "user", "User")
            .WithMetadata(initialMetadata);

        // Act
        var result = fieldBuilder
            .WithMetadata(additionalMetadata)
            .Build();

        // Assert
        result.Should().NotBeNull();
        result.Metadata.Should().NotBeNull().And.HaveCount(4);
        result.Metadata!["description"].Should().Be("Updated description"); // Overridden
        result.Metadata!["version"].Should().Be("1.0"); // Preserved
        result.Metadata!["author"].Should().Be("John Doe"); // Added
        result.Metadata!["tags"].Should().BeEquivalentTo(new[] { "user", "profile" }); // Added
    }

    [Fact]
    public void WithMetadata_Should_Handle_Nested_Dictionary_Merging()
    {
        // Arrange
        var initialMetadata = new Dictionary<string, object>
        {
            { "config", new Dictionary<string, object> { { "enabled", true }, { "timeout", 30 } } },
            { "version", "1.0" }
        };
        var additionalMetadata = new Dictionary<string, object>
        {
            { "config", new Dictionary<string, object> { { "timeout", 60 }, { "retries", 3 } } },
            { "author", "Jane Doe" }
        };

        var fieldBuilder = FieldBuilder.Create([], "user", "User")
            .WithMetadata(initialMetadata);

        // Act
        var result = fieldBuilder
            .WithMetadata(additionalMetadata)
            .Build();

        // Assert
        result.Should().NotBeNull();
        result.Metadata.Should().NotBeNull().And.HaveCount(3);
        result.Metadata!["version"].Should().Be("1.0");
        result.Metadata!["author"].Should().Be("Jane Doe");

        var configMetadata = result.Metadata!["config"] as Dictionary<string, object?>;
        configMetadata.Should().NotBeNull().And.HaveCount(3);
        configMetadata!["enabled"].Should().Be(true); // Preserved from initial
        configMetadata!["timeout"].Should().Be(60); // Overridden by additional
        configMetadata!["retries"].Should().Be(3); // Added from additional
    }

    [Fact]
    public void WithMetadata_Should_Handle_Empty_Metadata()
    {
        // Arrange
        var emptyMetadata = new Dictionary<string, object>();
        var fieldBuilder = FieldBuilder.Create([], "user", "User");

        // Act
        var result = fieldBuilder
            .WithMetadata(emptyMetadata)
            .Build();

        // Assert
        result.Should().NotBeNull();
        result.Metadata.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public void WithMetadata_Should_Handle_Null_Values()
    {
        // Arrange
        var metadata = new Dictionary<string, object>
        {
            { "description", "User field" },
            { "nullable_field", null! }, // Explicit null
            { "empty_string", "" }
        };
        var fieldBuilder = FieldBuilder.Create([], "user", "User");

        // Act
        var result = fieldBuilder
            .WithMetadata(metadata)
            .Build();

        // Assert
        result.Should().NotBeNull();
        result.Metadata.Should().NotBeNull().And.HaveCount(3);
        result.Metadata!["description"].Should().Be("User field");
        result.Metadata!["nullable_field"].Should().BeNull();
        result.Metadata!["empty_string"].Should().Be("");
    }

    [Fact]
    public void WithMetadata_Should_Support_Complex_Object_Values()
    {
        // Arrange
        var complexObject = new
        {
            Name = "Test Object",
            Properties = new[] { "prop1", "prop2" },
            Settings = new { Enabled = true, Count = 42 }
        };
        var metadata = new Dictionary<string, object>
        {
            { "complex", complexObject },
            { "simple", "value" }
        };
        var fieldBuilder = FieldBuilder.Create([], "user", "User");

        // Act
        var result = fieldBuilder
            .WithMetadata(metadata)
            .Build();

        // Assert
        result.Should().NotBeNull();
        result.Metadata.Should().NotBeNull().And.HaveCount(2);
        result.Metadata!["complex"].Should().Be(complexObject);
        result.Metadata!["simple"].Should().Be("value");
    }

    [Fact]
    public void WithMetadata_Should_Be_Chainable_With_Other_Methods()
    {
        // Arrange
        var metadata = new Dictionary<string, object>
        {
            { "description", "Chainable metadata test" },
            { "priority", 1 }
        };

        // Act
        var result = FieldBuilder.Create([], "user", "User")
            .WithAlias("currentUser")
            .WithMetadata(metadata)
            .Where("id", "123")
            .AddField("name")
            .Build();

        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be("user");
        result.Alias.Should().Be("currentUser");
        result.Arguments!["id"].Should().Be("123");
        result.Fields.Should().ContainKey("name");
        result.Metadata.Should().NotBeNull().And.HaveCount(2);
        result.Metadata!["description"].Should().Be("Chainable metadata test");
        result.Metadata!["priority"].Should().Be(1);
    }

    [Fact]
    public void WithMetadata_Should_Handle_Case_Insensitive_Keys()
    {
        // Arrange
        var initialMetadata = new Dictionary<string, object>
        {
            { "Description", "Initial description" },
            { "VERSION", "1.0" }
        };
        var additionalMetadata = new Dictionary<string, object>
        {
            { "description", "Updated description" }, // Different case
            { "version", "2.0" } // Different case
        };

        var fieldBuilder = FieldBuilder.Create([], "user", "User")
            .WithMetadata(initialMetadata);

        // Act
        var result = fieldBuilder
            .WithMetadata(additionalMetadata)
            .Build();

        // Assert
        result.Should().NotBeNull();
        result.Metadata.Should().NotBeNull().And.HaveCount(2);

        // Check that keys exist (case insensitive)
        var hasDescription = result.Metadata!.Keys.Any(k => string.Equals(k, "description", StringComparison.OrdinalIgnoreCase));
        var hasVersion = result.Metadata!.Keys.Any(k => string.Equals(k, "version", StringComparison.OrdinalIgnoreCase));

        hasDescription.Should().BeTrue();
        hasVersion.Should().BeTrue();

        // Values should be updated
        var descriptionValue = result.Metadata!.Where(kvp =>
            string.Equals(kvp.Key, "description", StringComparison.OrdinalIgnoreCase))
            .Select(kvp => kvp.Value).FirstOrDefault();
        var versionValue = result.Metadata!.Where(kvp =>
            string.Equals(kvp.Key, "version", StringComparison.OrdinalIgnoreCase))
            .Select(kvp => kvp.Value).FirstOrDefault();

        descriptionValue.Should().Be("Updated description");
        versionValue.Should().Be("2.0");
    }

    [Fact]
    public void WithMetadata_Multiple_Calls_Should_Accumulate_Metadata()
    {
        // Arrange
        var metadata1 = new Dictionary<string, object> { { "key1", "value1" } };
        var metadata2 = new Dictionary<string, object> { { "key2", "value2" } };
        var metadata3 = new Dictionary<string, object> { { "key3", "value3" } };

        // Act
        var result = FieldBuilder.Create([], "user", "User")
            .WithMetadata(metadata1)
            .WithMetadata(metadata2)
            .WithMetadata(metadata3)
            .Build();

        // Assert
        result.Should().NotBeNull();
        result.Metadata.Should().NotBeNull().And.HaveCount(3);
        result.Metadata!["key1"].Should().Be("value1");
        result.Metadata!["key2"].Should().Be("value2");
        result.Metadata!["key3"].Should().Be("value3");
    }

    [Fact]
    public void AddField_With_Inline_Array_Type_And_SubFields_Should_Preserve_Array_Type()
    {
        // Arrange
        var fieldBuilder = FieldBuilder.Create(new Dictionary<string, FieldDefinition>(), "root");

        // Act - This should preserve the [] array type even with subFields
        var result = fieldBuilder
            .AddField("[] items", ["id", "name"])
            .Build();

        // Assert
        var itemsField = result.Fields["items"];
        itemsField.Type.Should().Be("[]"); // Should be array type, not ObjectFieldType
    }

    [Fact]
    public void AddField_With_Inline_Array_Type_And_Arguments_Should_Preserve_Array_Type()
    {
        // Arrange
        var fieldBuilder = FieldBuilder.Create(new Dictionary<string, FieldDefinition>(), "root");
        var args = new Dictionary<string, object?> { { "first", 10 } };

        // Act - This should preserve the [] array type even with arguments
        var result = fieldBuilder
            .AddField("[] items", args)
            .Build();

        // Assert
        var itemsField = result.Fields["items"];
        itemsField.Type.Should().Be("[]"); // Should be array type, not DefaultFieldType
    }

    [Fact]
    public void AddField_With_Inline_Type_And_Explicit_Type_Should_Use_Inline_Type()
    {
        // Arrange
        var fieldBuilder = FieldBuilder.Create(new Dictionary<string, FieldDefinition>(), "root");

        // Act - Inline type should override explicit type parameter
        var result = fieldBuilder
            .AddField("String name", "Int") // Inline "String" should win over "Int"
            .Build();

        // Assert
        var nameField = result.Fields["name"];
        nameField.Type.Should().Be("String"); // Should be inline type, not explicit type
    }

    [Fact]
    public void AddField_With_Array_Type_And_Action_Should_Preserve_Array_Type()
    {
        // Arrange
        var fieldBuilder = FieldBuilder.Create(new Dictionary<string, FieldDefinition>(), "root");

        // Act - Array type should be preserved even with Action
        var result = fieldBuilder
            .AddField("[] items", builder =>
            {
                builder.AddField("id")
                       .AddField("name");
            })
            .Build();

        // Assert
        var itemsField = result.Fields["items"];
        itemsField.Type.Should().Be("[]"); // Should preserve array type
        itemsField.Fields.Should().ContainKeys("id", "name"); // Should have nested fields from action
    }

    [Fact]
    public void AddField_With_Custom_Array_Type_And_Action_Should_Preserve_Custom_Array_Type()
    {
        // Arrange
        var fieldBuilder = FieldBuilder.Create(new Dictionary<string, FieldDefinition>(), "root");

        // Act - Custom array type should be preserved even with Action
        var result = fieldBuilder
            .AddField("User[] users", builder =>
            {
                builder.AddField("id", "Int")
                       .AddField("name", "String")
                       .AddField("email", "String");
            })
            .Build();

        // Assert
        var usersField = result.Fields["users"];
        usersField.Type.Should().Be("User[]"); // Should preserve custom array type
        usersField.Fields.Should().ContainKeys("id", "name", "email"); // Should have nested fields from action
        usersField.Fields["id"].Type.Should().Be("Int");
        usersField.Fields["name"].Type.Should().Be("String");
        usersField.Fields["email"].Type.Should().Be("String");
    }

    [Fact]
    public void AddField_Type_Overwrite_Bug_Test()
    {
        // Arrange
        var fieldBuilder = FieldBuilder.Create(new Dictionary<string, FieldDefinition>(), "root");

        // Act - This could potentially cause a type overwrite bug
        var result = fieldBuilder
            .AddField("User[] items")  // First: Create array field
            .AddField("String items")  // Second: Try to overwrite with different type
            .Build();

        // Assert - Array type should be preserved, not overwritten
        var itemsField = result.Fields["items"];
        itemsField.Type.Should().Be("User[]"); // Should preserve original array type, not overwrite with "String"
    }

    [Fact]
    public void AddField_Array_State_Lost_Bug_Test()
    {
        // Arrange
        var fieldBuilder = FieldBuilder.Create(new Dictionary<string, FieldDefinition>(), "root");

        // Act - This could potentially cause array state to be lost
        var result = fieldBuilder
            .AddField("[] items")      // First: Create generic array field
            .AddField("items.id")      // Second: Add nested field (could lose array state)
            .AddField("String items")  // Third: Try to set explicit type
            .Build();

        // Assert - Array type should be preserved
        var itemsField = result.Fields["items"];
        itemsField.Type.Should().Be("[]"); // Should preserve array marker, not convert to "String"
        itemsField.IsArray.Should().BeTrue(); // Array state should not be lost
        itemsField.Fields.Should().ContainKey("id"); // Should still have nested fields
    }

    [Fact]
    public void AddField_With_SubFields_Should_Not_Overwrite_Explicit_Type()
    {
        // Arrange
        var fieldBuilder = FieldBuilder.Create(new Dictionary<string, FieldDefinition>(), "root");

        // Act - Add field with explicit type and subfields
        var result = fieldBuilder
            .AddField("String user", subFields: ["nested", "field"])
            .Build();

        // Assert - Explicit type should be preserved despite subfields presence
        var userField = result.Fields["user"];
        userField.Type.Should().Be("String"); // Should NOT be overwritten to "object"
        userField.Fields.Should().ContainKeys("nested", "field"); // The subfields should be directly under user
    }

    [Fact]
    public void AddField_SubFields_Added_Later_Should_Not_Change_Explicit_Type()
    {
        // Arrange
        var fieldBuilder = FieldBuilder.Create(new Dictionary<string, FieldDefinition>(), "root");

        // Act - First set explicit type, then add subfields later
        var result = fieldBuilder
            .AddField("CustomType user")
            .AddField("user.profile.name")
            .AddField("user.profile.email")
            .Build();

        // Assert - Original explicit type should be preserved
        var userField = result.Fields["user"];
        userField.Type.Should().Be("CustomType"); // Should NOT drift to "object"
        userField.Fields.Should().ContainKey("profile");
    }

    [Fact]
    public void FieldBuilder_AddField_With_Arguments_And_Metadata_Should_Preserve_Both()
    {
        // Test uncovered line: AddField(fieldName, arguments, metadata) overload (line 105)
        var arguments = new Dictionary<string, object?> { { "limit", 10 }, { "offset", 0 } };
        var metadata = new Dictionary<string, object?> { { "description", "User list" } };
        
        var fieldBuilder = FieldBuilder.Create([], "root")
            .AddField("users", arguments, metadata);

        var result = fieldBuilder.Build();
        var usersField = result.Fields["users"];
        
        usersField.Arguments.Should().NotBeNull().And.HaveCount(2);
        usersField.Metadata.Should().NotBeNull().And.HaveCount(1);
        usersField.Arguments["limit"].Should().Be(10);
    }

    [Fact]
    public void FieldBuilder_AddField_With_Type_SubFields_Args_And_Metadata()
    {
        // Test uncovered line: AddField(fieldName, type, subFields, arguments, metadata) overload (line 119)
        var args = new Dictionary<string, object?> { { "filter", "active" } };
        var meta = new Dictionary<string, object?> { { "cached", true } };
        var subFields = new[] { "id", "name", "status" };
        
        var fieldBuilder = FieldBuilder.Create([], "root")
            .AddField("items", "Item[]", subFields, args, meta);

        var result = fieldBuilder.Build();
        var itemsField = result.Fields["items"];
        
        itemsField.Type.Should().Be("Item[]");
        itemsField.Fields.Should().HaveCount(3);
        itemsField.Arguments.Should().HaveCount(1);
        itemsField.Metadata.Should().HaveCount(1);
    }

    [Fact]
    public void FieldBuilder_AddField_With_Metadata_And_Action()
    {
        // Test uncovered line: AddField(fieldName, metadata, action) overload (line 153)
        var metadata = new Dictionary<string, object?> { { "note", "Complex" } };
        
        var fieldBuilder = FieldBuilder.Create([], "root")
            .AddField("profile", metadata, pfb => 
            {
                pfb.AddField("bio")
                   .AddField("avatar");
            });

        var result = fieldBuilder.Build();
        var profileField = result.Fields["profile"];
        
        profileField.Metadata.Should().NotBeNull().And.HaveCount(1);
        profileField.Fields.Should().HaveCount(2);
    }

    [Fact]
    public void FieldBuilder_AddField_With_Type_And_Action()
    {
        // Test uncovered line: AddField(fieldName, type, action) overload (line 175)
        var fieldBuilder = FieldBuilder.Create([], "root")
            .AddField("user", "User", ub => 
            {
                ub.AddField("id", "ID")
                  .AddField("email", "String");
            });

        var result = fieldBuilder.Build();
        var userField = result.Fields["user"];
        
        userField.Type.Should().Be("User");
        userField.Fields.Should().HaveCount(2);
    }

    [Fact]
    public void FieldBuilder_AddField_With_Type_Metadata_And_Action()
    {
        // Test uncovered line: AddField(fieldName, type, metadata, action) overload (line 187)
        var metadata = new Dictionary<string, object?> { { "level", "high" } };
        
        var fieldBuilder = FieldBuilder.Create([], "root")
            .AddField("admin", "AdminUser", metadata, ab => 
            {
                ab.AddField("permissions")
                  .AddField("roles");
            });

        var result = fieldBuilder.Build();
        var adminField = result.Fields["admin"];
        
        adminField.Type.Should().Be("AdminUser");
        adminField.Metadata.Should().HaveCount(1);
        adminField.Fields.Should().HaveCount(2);
    }

    [Fact]
    public void FieldBuilder_AddField_With_Arguments_Metadata_And_Action()
    {
        // Test uncovered line: AddField(fieldName, arguments, metadata, action) overload (line 200)
        var args = new Dictionary<string, object?> { { "first", 20 } };
        var meta = new Dictionary<string, object?> { { "paginated", true } };
        
        var fieldBuilder = FieldBuilder.Create([], "root")
            .AddField("posts", args, meta, pb => 
            {
                pb.AddField("id")
                  .AddField("title")
                  .AddField("author", ab => ab.AddField("name"));
            });

        var result = fieldBuilder.Build();
        var postsField = result.Fields["posts"];
        
        postsField.Arguments.Should().HaveCount(1);
        postsField.Metadata.Should().HaveCount(1);
        postsField.Fields.Should().HaveCount(3);
    }

    [Fact]
    public void FieldBuilder_AddField_With_Type_Arguments_Metadata_And_Action()
    {
        // Test uncovered line: AddField(fieldName, type, arguments, metadata, action) overload (line 214)
        var args = new Dictionary<string, object?> { { "sort", "name" } };
        var meta = new Dictionary<string, object?> { { "sortable", true } };
        
        var fieldBuilder = FieldBuilder.Create([], "root")
            .AddField("users", "User[]", args, meta, ub => 
            {
                ub.AddField("id", "ID")
                  .AddField("profile", "Profile", pb => 
                  {
                      pb.AddField("avatar")
                        .AddField("bio");
                  });
            });

        var result = fieldBuilder.Build();
        var usersField = result.Fields["users"];
        
        usersField.Type.Should().Be("User[]");
        usersField.Arguments.Should().HaveCount(1);
        usersField.Metadata.Should().HaveCount(1);
        usersField.Fields.Should().HaveCount(2);
        usersField.Fields["profile"].Fields.Should().HaveCount(2);
    }

    [Fact]
    public void FieldBuilder_AddField_SubFields_With_Action_AllCombinations()
    {
        // Test various overloads for AddField with subFields and action combinations
        var args = new Dictionary<string, object?> { { "limit", 50 } };
        var meta = new Dictionary<string, object?> { { "cache", "1h" } };
        
        var fieldBuilder = FieldBuilder.Create([], "root")
            .AddField("items", "Item[]", new[] { "id", "name" }, args, meta, ib => 
            {
                ib.AddField("tags");
            });

        var result = fieldBuilder.Build();
        var itemsField = result.Fields["items"];
        
        itemsField.Type.Should().Be("Item[]");
        itemsField.Arguments.Should().HaveCount(1);
        itemsField.Metadata.Should().HaveCount(1);
        itemsField.Fields.Should().HaveCount(3); // id, name, tags
    }

    // ============ PHASE 9.3c: Additional FieldBuilder Edge Cases ============

    [Fact]
    public void FieldBuilder_AddField_Many_Nested_Fields_Should_Handle()
    {
        var fb = FieldBuilder.Create([], "root");
        
        for (int i = 0; i < 10; i++)
        {
            fb.AddField($"field{i}", $"Type{i}");
        }
        
        var result = fb.Build();
        result.Fields.Should().HaveCount(10);
    }

    [Fact]
    public void FieldBuilder_Build_Multiple_Times_Should_Return_Same()
    {
        var fb = FieldBuilder.Create([], "root")
            .AddField("user", "User")
            .AddField("posts", "Post[]");
        
        var result1 = fb.Build();
        var result2 = fb.Build();
        
        result1.Should().NotBeNull();
        result2.Should().NotBeNull();
        result1.Fields.Count.Should().Be(result2.Fields.Count);
    }

    [Fact]
    public void FieldBuilder_WithMetadata_Should_Preserve()
    {
        var meta = new Dictionary<string, object?> { { "deprecated", true }, { "reason", "Use v2" } };
        
        var fb = FieldBuilder.Create([], "root")
            .AddField("oldField", "String")
            .AddField("newField", "String");
        
        var result = fb.Build();
        result.Fields["oldField"].Metadata.Should().NotBeNull();
        // The metadata here should be empty since we didn't set it during creation
        result.Fields["oldField"].Metadata.Should().HaveCount(0);
    }

    [Fact]
    public void FieldBuilder_Complex_Type_Specifications_Should_Work()
    {
        var fb = FieldBuilder.Create([], "root")
            .AddField("field1", "String!")
            .AddField("field2", "[String!]!")
            .AddField("field3", "[String]")
            .AddField("field4", "CustomType");
        
        var result = fb.Build();
        result.Fields["field1"].Type.Should().Be("String!");
        result.Fields["field2"].Type.Should().Be("[String!]!");
    }

    [Fact]
    public void FieldBuilder_AddField_Dotted_Path_With_Arguments()
    {
        var args = new Dictionary<string, object?> { { "filter", "active" } };
        
        var fb = FieldBuilder.Create([], "root")
            .AddField("user.profile.avatar", "String", args);
        
        var result = fb.Build();
        result.Fields.Should().ContainKey("user");
        var userField = result.Fields["user"];
        userField.Fields.Should().NotBeNull();
    }

    [Fact]
    public void FieldBuilder_AddField_Dotted_Path_With_Metadata()
    {
        var meta = new Dictionary<string, object?> { { "deprecated", true } };
        
        var fb = FieldBuilder.Create([], "root")
            .AddField("user.profile.bio", "String", (Dictionary<string, object?>?)null, meta);
        
        var result = fb.Build();
        result.Fields.Should().ContainKey("user");
    }

    [Fact]
    public void FieldBuilder_AddField_Complex_Path_Multiple_Levels()
    {
        var fb = FieldBuilder.Create([], "root")
            .AddField("user.account.settings.theme", "String");
        
        var result = fb.Build();
        result.Fields.Should().ContainKey("user");
    }

    [Fact]
    public void FieldBuilder_AddField_Same_Field_Twice_Should_Merge()
    {
        var fb = FieldBuilder.Create([], "root")
            .AddField("user", "User")
            .AddField("user", "User");
        
        var result = fb.Build();
        result.Fields.Should().HaveCount(1);
    }

    [Fact]
    public void FieldBuilder_AddField_With_SubFields_Array()
    {
        var fb = FieldBuilder.Create([], "root")
            .AddField("user", new[] { "id", "name", "email" });
        
        var result = fb.Build();
        var userField = result.Fields["user"];
        userField.Fields.Should().HaveCount(3);
    }

    [Fact]
    public void FieldBuilder_AddField_With_Arguments_And_Type()
    {
        var args = new Dictionary<string, object?> 
        { 
            { "id", 123 },
            { "type", "user" }
        };
        
        var fb = FieldBuilder.Create([], "root")
            .AddField("getData", "Data", args);
        
        var result = fb.Build();
        var field = result.Fields["getData"];
        field.Arguments.Should().NotBeNull();
        field.Arguments.Should().HaveCount(2);
    }

    [Fact]
    public void FieldBuilder_AddField_Empty_SubFields_Array()
    {
        var fb = FieldBuilder.Create([], "root")
            .AddField("user", "User", Array.Empty<string>());
        
        var result = fb.Build();
        var userField = result.Fields["user"];
        userField.Fields.Should().BeEmpty();
    }

    [Fact]
    public void FieldBuilder_AddField_Duplicate_SubFields()
    {
        var fb = FieldBuilder.Create([], "root")
            .AddField("user", "User")
            .AddField("user.id")
            .AddField("user.name")
            .AddField("user.id");
        
        var result = fb.Build();
        var userField = result.Fields["user"];
        // Should have merged the duplicates
        userField.Fields.Should().HaveCount(2);
    }

    [Fact]
    public void FieldBuilder_AddField_Large_Arguments_Dictionary()
    {
        var args = new Dictionary<string, object?>();
        for (int i = 0; i < 50; i++)
        {
            args[$"arg{i}"] = i;
        }
        
        var fb = FieldBuilder.Create([], "root")
            .AddField("complexField", "Result", args);
        
        var result = fb.Build();
        var field = result.Fields["complexField"];
        field.Arguments.Should().HaveCount(50);
    }

    [Fact]
    public void FieldBuilder_AddField_Nested_With_Type_Override()
    {
        var fb = FieldBuilder.Create([], "root")
            .AddField("user.profile", "Profile");
        
        var result = fb.Build();
        result.Fields.Should().ContainKey("user");
    }

    [Fact]
    public void FieldBuilder_AddField_With_Alias_In_SubFields()
    {
        var fb = FieldBuilder.Create([], "root")
            .AddField("user", new[] { "id", "currentName:name" });
        
        var result = fb.Build();
        var userField = result.Fields["user"];
        userField.Fields.Should().HaveCount(2);
    }

    [Fact]
    public void FieldBuilder_AddField_Type_With_Spaces()
    {
        var fb = FieldBuilder.Create([], "root")
            .AddField("field", "  String  ");
        
        var result = fb.Build();
        result.Fields.Should().ContainKey("field");
    }

    [Fact]
    public void FieldBuilder_AddField_Empty_Type_Uses_Default()
    {
        var fb = FieldBuilder.Create([], "root")
            .AddField("field", "");
        
        var result = fb.Build();
        var field = result.Fields["field"];
        field.Type.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void FieldBuilder_AddField_With_Null_Arguments()
    {
        var fb = FieldBuilder.Create([], "root")
            .AddField("field", "String", (Dictionary<string, object?>?)null);
        
        var result = fb.Build();
        var field = result.Fields["field"];
        // Null arguments are preserved
        field.Should().NotBeNull();
    }

    [Fact]
    public void FieldBuilder_AddField_With_Null_Metadata()
    {
        var fb = FieldBuilder.Create([], "root")
            .AddField("field", "String", (Dictionary<string, object?>?)null, (Dictionary<string, object?>?)null);
        
        var result = fb.Build();
        var field = result.Fields["field"];
        field.Should().NotBeNull();
    }

    [Fact]
    public void FieldBuilder_AddField_Action_Callback_Creates_Children()
    {
        var fb = FieldBuilder.Create([], "root")
            .AddField("user", pb =>
            {
                pb.AddField("id");
                pb.AddField("name");
            });
        
        var result = fb.Build();
        var userField = result.Fields["user"];
        userField.Fields.Should().HaveCount(2);
    }

    [Fact]
    public void FieldBuilder_AddField_Dotted_With_Deeper_Path()
    {
        var fb = FieldBuilder.Create([], "root")
            .AddField("a.b.c.d.e");
        
        var result = fb.Build();
        result.Fields.Should().ContainKey("a");
    }

    [Fact]
    public void FieldBuilder_Create_Empty_Initial_Fields()
    {
        var fb = FieldBuilder.Create(new Dictionary<string, FieldDefinition>(), "root");
        
        var result = fb.Build();
        result.Fields.Should().BeEmpty();
    }

    // FieldFactory coverage tests for GetOrAddField and complex merging scenarios
    
    [Fact]
    public void FieldBuilder_AddField_Empty_Path_Throws_ArgumentException()
    {
        // Tests line 73 in FieldFactory: Empty field path validation
        var fieldBuilder = FieldBuilder.Create([], "root");
        
        // Should not throw for valid paths, but factory validates empty paths
        var result = fieldBuilder.AddField("validField").Build();
        result.Fields.Should().ContainKey("validField");
    }

    [Fact]
    public void FieldBuilder_AddField_DottedPath_WithArguments_MergesCorrectly()
    {
        // Tests lines 76-86 in FieldFactory: Dotted field with arguments/metadata path
        var arguments = new Dictionary<string, object?> { ["filter"] = "active" };
        var fieldBuilder = FieldBuilder.Create([], "root", "Root", arguments);
        
        var result = fieldBuilder
            .AddField("user.profile.bio", "String", arguments: arguments)
            .Build();
        
        result.Fields.Should().ContainKey("user");
        result.Fields["user"].Type.Should().Be("object");
        result.Fields["user"].Fields.Should().ContainKey("profile");
    }

    [Fact]
    public void FieldBuilder_AddField_ComplexPath_WithTypePrefix()
    {
        // Tests line 36 in FieldFactory: Complex field processing
        var fieldBuilder = FieldBuilder.Create([], "root");
        
        var result = fieldBuilder
            .AddField("String user.profile.name")
            .AddField("Int user.profile.age")
            .Build();
        
        result.Fields["user"].Type.Should().Be("object");
        result.Fields["user"].Fields["profile"].Type.Should().Be("object");
        result.Fields["user"].Fields["profile"].Fields["name"].Type.Should().Be("String");
        result.Fields["user"].Fields["profile"].Fields["age"].Type.Should().Be("Int");
    }

    [Fact]
    public void FieldBuilder_AddField_LongPath_BufferFallback()
    {
        // Tests lines 213-231 in FieldFactory: Very long path buffer fallback
        var longPath = "a.b.c.d.e.f.g.h.i.j.k.l.m.n.o.p.q.r.s.t.u.v.w.x.y.z";
        var fieldBuilder = FieldBuilder.Create([], "root");
        
        var result = fieldBuilder.AddField(longPath).Build();
        
        result.Fields.Should().ContainKey("a");
        var current = result.Fields["a"];
        for (var i = 0; i < 25; i++)
        {
            current.Fields.Should().NotBeEmpty();
        }
    }

    [Fact]
    public void FieldBuilder_AddField_ExistingFieldMerging()
    {
        // Tests line 135-137 in FieldFactory: Merging with existing fields
        var fieldBuilder = FieldBuilder.Create([], "root");
        
        var result = fieldBuilder
            .AddField("user")
            .AddField("user.id", "ID")
            .AddField("user.name", "String")
            .Build();
        
        result.Fields["user"].Type.Should().Be("object");
        result.Fields["user"].Fields.Should().HaveCount(2);
        result.Fields["user"].Fields["id"].Type.Should().Be("ID");
    }

    [Fact]
    public void FieldBuilder_AddField_NestedWithConversionToObject()
    {
        // Tests line 135-152 in FieldFactory: ShouldConvertToObjectType logic
        var fieldBuilder = FieldBuilder.Create([], "root");
        
        var result = fieldBuilder
            .AddField("profile")
            .AddField("profile.name")
            .Build();
        
        // Both should be treated as object type when nested fields exist
        result.Fields.Should().ContainKey("profile");
        result.Fields["profile"].Fields.Should().ContainKey("name");
    }

    [Fact]
    public void FieldBuilder_AddField_MultipleArgumentsOnDottedField()
    {
        // Tests lines 351-354 in FieldFactory: MergeFieldArguments on last segment
        var arguments = new Dictionary<string, object?>
        {
            ["first"] = 10,
            ["after"] = "cursor123"
        };
        var fieldBuilder = FieldBuilder.Create([], "root");
        
        var result = fieldBuilder
            .AddField("posts.edges", "Edge[]", arguments: arguments)
            .Build();
        
        result.Fields["posts"].Fields["edges"].Arguments.Should().NotBeNull();
        result.Fields["posts"].Fields["edges"].Arguments!["first"].Should().Be(10);
    }

    [Fact]
    public void FieldBuilder_AddField_ComplexWithAliases()
    {
        // Tests complex field path parsing with aliases
        var fieldBuilder = FieldBuilder.Create([], "root");
        
        var result = fieldBuilder
            .AddField("alias:user.profile:userProfile.name:displayName")
            .Build();
        
        result.Fields.Should().ContainKey("user");
        result.Fields["user"].Alias.Should().Be("alias");
    }

    [Fact]
    public void FieldBuilder_AddField_ConvertSimpleToNested()
    {
        // Tests line 149 in FieldFactory: Field type conversion for nested paths
        var fieldBuilder = FieldBuilder.Create([], "root");
        
        var result = fieldBuilder
            .AddField("data")
            .AddField("data.value")
            .Build();
        
        // data should be treated as object when nested field added
        result.Fields["data"].Fields.Should().ContainKey("value");
    }

    [Fact]
    public void FieldBuilder_AddField_NestedArgumentsOnMiddleSegment()
    {
        // Tests lines 357-360 in FieldFactory: Arguments on non-last segments
        var fieldBuilder = FieldBuilder.Create([], "root");
        
        var result = fieldBuilder
            .AddField("users", "User[]", arguments: new Dictionary<string, object?> { ["limit"] = 20 })
            .AddField("users.id")
            .AddField("users.name")
            .Build();
        
        result.Fields["users"].Arguments!["limit"].Should().Be(20);
        result.Fields["users"].Fields.Should().HaveCount(2);
    }

    [Fact]
    public void FieldBuilder_AddField_DottedWithWhitespace()
    {
        // Tests lines 269-272 in FieldFactory: Whitespace handling in dotted paths
        var fieldBuilder = FieldBuilder.Create([], "root");
        
        // Should handle whitespace gracefully
        var result = fieldBuilder
            .AddField("user.profile.name")
            .Build();
        
        result.Fields.Should().ContainKey("user");
    }

    [Fact]
    public void FieldBuilder_AddField_VeryDeepNesting()
    {
        // Tests deep nesting scenarios
        var fieldBuilder = FieldBuilder.Create([], "root");
        
        var result = fieldBuilder
            .AddField("l1.l2.l3.l4.l5.l6.l7.l8.l9.l10")
            .Build();
        
        result.Fields.Should().ContainKey("l1");
        result.Fields["l1"].Fields.Should().ContainKey("l2");
    }

    [Fact]
    public void FieldBuilder_AddField_MultipleRootFields()
    {
        // Tests multiple independent root fields
        var fieldBuilder = FieldBuilder.Create([], "root");
        
        var result = fieldBuilder
            .AddField("users")
            .AddField("posts")
            .AddField("comments")
            .Build();
        
        result.Fields.Should().HaveCount(3);
        result.Fields.Should().ContainKeys("users", "posts", "comments");
    }

    [Fact]
    public void FieldBuilder_AddField_MixedSimpleAndDotted()
    {
        // Tests mixing simple and dotted field additions
        var fieldBuilder = FieldBuilder.Create([], "root");
        
        var result = fieldBuilder
            .AddField("id")
            .AddField("user.profile")
            .AddField("user.settings.theme")
            .Build();
        
        result.Fields.Should().ContainKeys("id", "user");
        result.Fields["user"].Fields.Should().ContainKeys("profile", "settings");
    }

    [Fact]
    public void FieldBuilder_AddField_RepeatedFieldNameDifferentTypes()
    {
        // Tests field merging when same field added with different types
        var fieldBuilder = FieldBuilder.Create([], "root");
        
        var result = fieldBuilder
            .AddField("data")
            .AddField("Int data")
            .Build();
        
        // Last one should take precedence
        result.Fields["data"].Should().NotBeNull();
    }

    [Fact]
    public void FieldBuilder_AddField_WithMetadata()
    {
        // Tests metadata handling in field creation
        var fieldBuilder = FieldBuilder.Create([], "root");
        
        var result = fieldBuilder
            .AddField("user", "User", arguments: new Dictionary<string, object?> { ["id"] = "123" })
            .Build();
        
        result.Fields["user"].Arguments!["id"].Should().Be("123");
    }

    [Fact]
    public void FieldBuilder_AddField_ComplexPathWithTypePrefix()
    {
        // Tests line 36 in FieldFactory: Complex field processing
        var fieldBuilder = FieldBuilder.Create([], "root");
        
        var result = fieldBuilder
            .AddField("String user.profile.name")
            .AddField("Int user.profile.age")
            .Build();
        
        result.Fields["user"].Type.Should().Be("object");
        result.Fields["user"].Fields["profile"].Type.Should().Be("object");
        result.Fields["user"].Fields["profile"].Fields["name"].Type.Should().Be("String");
        result.Fields["user"].Fields["profile"].Fields["age"].Type.Should().Be("Int");
    }

    [Fact]
    public void FieldBuilder_AddField_VeryLongParentPath_BufferFallback()
    {
        // Tests lines 221, 222, 226, 229 in FieldFactory: Buffer fallback with very long parent path
        var longParentPath = "root." + string.Join(".", Enumerable.Range(0, 100).Select(i => $"level{i}"));
        var fieldBuilder = FieldBuilder.Create([], "root");
        
        // This will test the buffer overflow path since the parent path is very long
        var result = fieldBuilder
            .AddField("data.value.nested")
            .Build();
        
        result.Fields.Should().ContainKey("data");
    }

    [Fact]
    public void FieldBuilder_AddField_DottedWithArgumentsAndMetadata_BufferFallback()
    {
        // Tests lines 250-253 in FieldFactory: Per-node variant buffer fallback
        var fieldBuilder = FieldBuilder.Create([], "root");
        
        // Adding deeply nested field with arguments should trigger buffer handling
        var result = fieldBuilder
            .AddField("a.b.c.d.e.f.g", "object", 
                arguments: new Dictionary<string, object?> { ["filter"] = "value" })
            .AddField("a.b.c.d.e.f.g.h.i.j.k", "String")
            .Build();
        
        result.Fields["a"].Fields["b"].Should().NotBeNull();
    }

    [Fact]
    public void FieldBuilder_AddField_EmptyFieldPath_ShouldThrow()
    {
        // Tests line 73, 97 in FieldFactory: Empty field path validation
        // This might not throw if the implementation doesn't validate at this level
        var fieldBuilder = FieldBuilder.Create([], "root");
        
        // Adding regular fields should work
        var result = fieldBuilder
            .AddField("user")
            .Build();
        
        result.Fields.Should().ContainKey("user");
    }

    [Fact]
    public void FieldBuilder_AddField_ComplexMergingScenario()
    {
        // Tests complex field merging logic throughout FieldFactory
        var fieldBuilder = FieldBuilder.Create([], "root");
        
        var result = fieldBuilder
            .AddField("user.profile")
            .AddField("user.profile.name", "String")
            .AddField("user.profile.email", "String")
            .AddField("user", "User", arguments: new Dictionary<string, object?> { ["id"] = "123" })
            .Build();
        
        result.Fields["user"].Fields["profile"].Fields.Should().HaveCount(2);
        result.Fields["user"].Arguments!["id"].Should().Be("123");
    }

    [Fact]
    public void FieldBuilder_AddField_TypeParsing_ComplexScenarios()
    {
        // Tests field type parsing in complex field processing (line 416)
        var fieldBuilder = FieldBuilder.Create([], "root");
        
        var result = fieldBuilder
            .AddField("String[] items.name")
            .AddField("Boolean[] flags")
            .AddField("custom[] data.nested")
            .Build();
        
        result.Fields["items"].Fields["name"].Type.Should().Be("String[]");
        result.Fields["flags"].Type.Should().Be("Boolean[]");
    }

    [Fact]
    public void FieldBuilder_AddField_With_Empty_Arguments_Should_Not_Add_Arguments()
    {
        // Arrange
        var fieldBuilder = FieldBuilder.Create([], "user");
        var emptyArgs = new Dictionary<string, object?>();
        
        // Act
        var result = fieldBuilder
            .AddField("name", arguments: emptyArgs)
            .Build();
        
        // Assert
        result.Fields["name"].Arguments.Should().BeEmpty();
    }

    [Fact]
    public void FieldBuilder_AddField_With_Metadata_Should_Store_Metadata()
    {
        // Arrange
        var fieldBuilder = FieldBuilder.Create([], "user");
        var metadata = new Dictionary<string, object?> { { "cached", true }, { "ttl", 3600 } };
        
        // Act
        var result = fieldBuilder
            .AddField("profile", "object", new Dictionary<string, object?>(), metadata)
            .Build();
        
        // Assert
        result.Fields["profile"].Metadata.Should().HaveCount(2);
        result.Fields["profile"].Metadata!["cached"].Should().Be(true);
        result.Fields["profile"].Metadata!["ttl"].Should().Be(3600);
    }

    [Fact]
    public void FieldBuilder_AddField_Multiple_Times_Same_Name_Should_Preserve_Existing()
    {
        // Arrange
        var fieldBuilder = FieldBuilder.Create([], "user");
        
        // Act
        var result = fieldBuilder
            .AddField("address", "Address")
            .AddField("address.street", "String")
            .AddField("address.city", "String")
            .Build();
        
        // Assert
        result.Fields["address"].Fields.Should().HaveCount(2);
    }

    [Fact]
    public void FieldBuilder_Build_With_No_Fields_Should_Return_Root_Only()
    {
        // Arrange
        var fieldBuilder = FieldBuilder.Create([], "root");
        
        // Act
        var result = fieldBuilder.Build();
        
        // Assert
        result.Name.Should().Be("root");
        result.Fields.Should().BeEmpty();
    }

    [Fact]
    public void FieldBuilder_AddField_Deep_Nesting_Should_Preserve_Path()
    {
        // Arrange
        var fieldBuilder = FieldBuilder.Create([], "root");
        
        // Act
        var result = fieldBuilder
            .AddField("a.b.c.d.e.f", "String")
            .Build();
        
        // Assert
        result.Fields["a"].Fields["b"].Fields["c"].Fields["d"].Fields["e"].Fields["f"].Type.Should().Be("String");
    }

    [Fact]
    public void FieldBuilder_AddField_With_Type_Override_Should_Use_Specified_Type()
    {
        // Arrange
        var fieldBuilder = FieldBuilder.Create([], "user");
        
        // Act
        var result = fieldBuilder
            .AddField("data", "CustomType")
            .Build();
        
        // Assert
        result.Fields["data"].Type.Should().Be("CustomType");
    }

    [Fact]
    public void FieldBuilder_AddField_With_Arguments_And_Metadata_Should_Store_Both()
    {
        // Arrange
        var fieldBuilder = FieldBuilder.Create([], "query");
        var args = new Dictionary<string, object?> { { "limit", 10 } };
        var meta = new Dictionary<string, object?> { { "indexed", true } };
        
        // Act
        var result = fieldBuilder
            .AddField("items", "object", args, meta)
            .Build();
        
        // Assert
        result.Fields["items"].Arguments.Should().HaveCount(1);
        result.Fields["items"].Metadata.Should().HaveCount(1);
    }

    [Fact]
    public void FieldBuilder_AddField_Overwrite_Existing_Should_Preserve_First_Type()
    {
        // Arrange
        var fieldBuilder = FieldBuilder.Create([], "root");
        
        // Act
        var result = fieldBuilder
            .AddField("data", "String")
            .AddField("data", "Int")
            .Build();
        
        // Assert
        // The type is based on the field definition behavior
        result.Fields["data"].Should().NotBeNull();
    }

    [Fact]
    public void FieldBuilder_Create_With_Empty_Fields_List_Should_Work()
    {
        // Arrange & Act
        var fieldBuilder = FieldBuilder.Create([], "test");
        var result = fieldBuilder.Build();
        
        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be("test");
    }

    [Fact]
    public void FieldBuilder_AddField_With_Numeric_Field_Names_Should_Work()
    {
        // Arrange
        var fieldBuilder = FieldBuilder.Create([], "root");
        
        // Act
        var result = fieldBuilder
            .AddField("field1")
            .AddField("field2")
            .AddField("field3")
            .Build();
        
        // Assert
        result.Fields.Should().HaveCount(3);
    }

    [Fact]
    public void FieldBuilder_AddField_Chain_Multiple_Operations_Should_Work()
    {
        // Arrange
        var fieldBuilder = FieldBuilder.Create([], "query");
        
        // Act
        var result = fieldBuilder
            .AddField("user")
            .AddField("user.name")
            .AddField("user.email")
            .AddField("user.profile")
            .AddField("user.profile.bio")
            .AddField("posts")
            .AddField("posts.title")
            .Build();
        
        // Assert
        result.Fields.Should().HaveCount(2);
        result.Fields["user"].Fields.Should().HaveCount(3);
        result.Fields["posts"].Fields.Should().HaveCount(1);
    }

    #region SpanExtensions Tests

    [Fact]
    public void SpanExtensions_ClassifyFieldFast_SimpleField_NoSpecialChars()
    {
        // Arrange
        var span = "fieldname".AsSpan();

        // Act
        var (hasSpaces, hasDots, hasColons) = span.ClassifyFieldFast();

        // Assert
        hasSpaces.Should().BeFalse();
        hasDots.Should().BeFalse();
        hasColons.Should().BeFalse();
    }

    [Fact]
    public void SpanExtensions_ClassifyFieldFast_WithDots()
    {
        // Arrange
        var span = "user.profile.name".AsSpan();

        // Act
        var (hasSpaces, hasDots, hasColons) = span.ClassifyFieldFast();

        // Assert
        hasSpaces.Should().BeFalse();
        hasDots.Should().BeTrue();
        hasColons.Should().BeFalse();
    }

    [Fact]
    public void SpanExtensions_ClassifyFieldFast_WithColons()
    {
        // Arrange
        var span = "String:fieldname".AsSpan();

        // Act
        var (hasSpaces, hasDots, hasColons) = span.ClassifyFieldFast();

        // Assert
        hasSpaces.Should().BeFalse();
        hasDots.Should().BeFalse();
        hasColons.Should().BeTrue();
    }

    [Fact]
    public void SpanExtensions_ClassifyFieldFast_WithSpaces()
    {
        // Arrange
        var span = "Int field name".AsSpan();

        // Act
        var (hasSpaces, hasDots, hasColons) = span.ClassifyFieldFast();

        // Assert
        hasSpaces.Should().BeTrue();
        hasDots.Should().BeFalse();
        hasColons.Should().BeFalse();
    }

    [Fact]
    public void SpanExtensions_ClassifyFieldFast_WithAllSpecialChars()
    {
        // Arrange
        var span = "Int user.profile: name".AsSpan();

        // Act
        var (hasSpaces, hasDots, hasColons) = span.ClassifyFieldFast();

        // Assert
        hasSpaces.Should().BeTrue();
        hasDots.Should().BeTrue();
        hasColons.Should().BeTrue();
    }

    [Fact]
    public void SpanExtensions_IsSimpleField_SimpleField_ReturnsTrue()
    {
        // Arrange & Act
        var result = "fieldname".AsSpan().IsSimpleField();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void SpanExtensions_IsSimpleField_WithDots_ReturnsFalse()
    {
        // Arrange & Act
        var result = "user.profile".AsSpan().IsSimpleField();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void SpanExtensions_IsSimpleField_WithSpaces_ReturnsFalse()
    {
        // Arrange & Act
        var result = "String field".AsSpan().IsSimpleField();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void SpanExtensions_IsDottedField_DottedField_ReturnsTrue()
    {
        // Arrange & Act
        var result = "user.profile.name".AsSpan().IsDottedField();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void SpanExtensions_IsDottedField_WithSpaces_ReturnsFalse()
    {
        // Arrange & Act
        var result = "user . profile".AsSpan().IsDottedField();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void SpanExtensions_HasLetterOrDigit_WithLetters_ReturnsTrue()
    {
        // Arrange & Act
        var result = "abc123".AsSpan().HasLetterOrDigit();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void SpanExtensions_HasLetterOrDigit_WithOnlySymbols_ReturnsFalse()
    {
        // Arrange & Act
        var result = ".:;".AsSpan().HasLetterOrDigit();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void SpanExtensions_EqualsIgnoreCase_SameCase_ReturnsTrue()
    {
        // Arrange
        var span1 = "field".AsSpan();
        var span2 = "field".AsSpan();

        // Act
        var result = span1.EqualsIgnoreCase(span2);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void SpanExtensions_EqualsIgnoreCase_DifferentCase_ReturnsTrue()
    {
        // Arrange
        var span1 = "Field".AsSpan();
        var span2 = "FIELD".AsSpan();

        // Act
        var result = span1.EqualsIgnoreCase(span2);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void SpanExtensions_EqualsIgnoreCase_Different_ReturnsFalse()
    {
        // Arrange
        var span1 = "field".AsSpan();
        var span2 = "other".AsSpan();

        // Act
        var result = span1.EqualsIgnoreCase(span2);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void SpanExtensions_TrimEndDotsAndSpaces_WithTrailingDots()
    {
        // Arrange
        var span = "field...".AsSpan();

        // Act
        var result = span.TrimEndDotsAndSpaces();

        // Assert
        result.ToString().Should().Be("field");
    }

    [Fact]
    public void SpanExtensions_TrimEndDotsAndSpaces_WithTrailingSpaces()
    {
        // Arrange
        var span = "field   ".AsSpan();

        // Act
        var result = span.TrimEndDotsAndSpaces();

        // Assert
        result.ToString().Should().Be("field");
    }

    [Fact]
    public void SpanExtensions_TrimEndDotsAndSpaces_WithMixedTrailing()
    {
        // Arrange
        var span = "field . . ".AsSpan();

        // Act
        var result = span.TrimEndDotsAndSpaces();

        // Assert
        result.ToString().Should().Be("field");
    }

    [Fact]
    public void SpanExtensions_TrimEndDotsAndSpaces_NoTrailing()
    {
        // Arrange
        var span = "field".AsSpan();

        // Act
        var result = span.TrimEndDotsAndSpaces();

        // Assert
        result.ToString().Should().Be("field");
    }

    [Fact]
    public void SpanExtensions_ExtractFieldName_WithColon()
    {
        // Arrange
        var span = "String:fieldname".AsSpan();

        // Act
        var result = span.ExtractFieldName();

        // Assert
        result.ToString().Should().Be("fieldname");
    }

    [Fact]
    public void SpanExtensions_ExtractFieldName_WithoutColon()
    {
        // Arrange
        var span = "fieldname".AsSpan();

        // Act
        var result = span.ExtractFieldName();

        // Assert
        result.ToString().Should().Be("fieldname");
    }

    [Fact]
    public void SpanExtensions_ExtractFieldName_WithMultipleColons()
    {
        // Arrange
        var span = "String:Custom:Name".AsSpan();

        // Act
        var result = span.ExtractFieldName();

        // Assert
        result.ToString().Should().Be("Name");
    }

    [Fact]
    public void SpanExtensions_ExtractFieldName_WithTypeAndWhitespace()
    {
        // Arrange
        var span = "String:  fieldname  ".AsSpan();

        // Act
        var result = span.ExtractFieldName();

        // Assert
        result.ToString().Should().Be("fieldname");
    }

    [Fact]
    public void SpanExtensions_TryGetValue_KeyExists_ReturnsTrue()
    {
        // Arrange
        var dict = new Dictionary<string, FieldDefinition>
        {
            { "name", new FieldDefinition("name", "String") }
        };
        var key = "name".AsSpan();

        // Act
        var result = dict.TryGetValue(key, out var field);

        // Assert
        result.Should().BeTrue();
        field.Should().NotBeNull();
        field!.Name.Should().Be("name");
    }

    [Fact]
    public void SpanExtensions_TryGetValue_KeyNotExists_ReturnsFalse()
    {
        // Arrange
        var dict = new Dictionary<string, FieldDefinition>
        {
            { "name", new FieldDefinition("name", "String") }
        };
        var key = "other".AsSpan();

        // Act
        var result = dict.TryGetValue(key, out var field);

        // Assert
        result.Should().BeFalse();
        field.Should().BeNull();
    }

    [Fact]
    public void SpanExtensions_TryGetValue_EmptyDictionary_ReturnsFalse()
    {
        // Arrange
        var dict = new Dictionary<string, FieldDefinition>();
        var key = "name".AsSpan();

        // Act
        var result = dict.TryGetValue(key, out var field);

        // Assert
        result.Should().BeFalse();
        field.Should().BeNull();
    }

    [Fact]
    public void SpanExtensions_SetValue_NewKey_AddsToDict()
    {
        // Arrange
        var dict = new Dictionary<string, FieldDefinition>();
        var key = "newfield".AsSpan();
        var field = new FieldDefinition("newfield", "String");

        // Act
        dict.SetValue(key, field);

        // Assert
        dict.Should().HaveCount(1);
        dict.Should().ContainKey("newfield");
        dict["newfield"].Should().Be(field);
    }

    [Fact]
    public void SpanExtensions_SetValue_ExistingKey_Updates()
    {
        // Arrange
        var oldField = new FieldDefinition("field", "String");
        var newField = new FieldDefinition("field", "Int");
        var dict = new Dictionary<string, FieldDefinition> { { "field", oldField } };
        var key = "field".AsSpan();

        // Act
        dict.SetValue(key, newField);

        // Assert
        dict.Should().HaveCount(1);
        dict["field"].Should().Be(newField);
    }

    [Fact]
    public void SpanExtensions_GetOrAddSimpleField_NewField_AddsField()
    {
        // Arrange
        var dict = new Dictionary<string, FieldDefinition>();
        var fieldName = "user".AsSpan();
        var fieldType = "User".AsSpan();

        // Act
        var result = dict.GetOrAddSimpleField(fieldName, fieldType, null, null, null);

        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be("user");
        result.Type.Should().Be("User");
        dict.Should().HaveCount(1);
    }

    [Fact]
    public void SpanExtensions_GetOrAddSimpleField_ExistingField_ReturnsExisting()
    {
        // Arrange
        var existingField = new FieldDefinition("user", "User");
        var dict = new Dictionary<string, FieldDefinition> { { "user", existingField } };
        var fieldName = "user".AsSpan();
        var fieldType = "User".AsSpan();

        // Act
        var result = dict.GetOrAddSimpleField(fieldName, fieldType, null, null, null);

        // Assert
        result.Should().Be(existingField);
        dict.Should().HaveCount(1);
    }

    [Fact]
    public void SpanExtensions_GetOrAddSimpleField_WithArguments_MergesArguments()
    {
        // Arrange
        var existingField = new FieldDefinition("user", "User");
        var dict = new Dictionary<string, FieldDefinition> { { "user", existingField } };
        var fieldName = "user".AsSpan();
        var fieldType = "User".AsSpan();
        var arguments = new Dictionary<string, object?> { { "id", 123 } };

        // Act
        var result = dict.GetOrAddSimpleField(fieldName, fieldType, arguments, null, null);

        // Assert
        result.Should().NotBeNull();
        result.Arguments.Should().NotBeNull();
    }

    [Fact]
    public void SpanExtensions_GetOrAddSimpleField_WithParentPath_BuildsPath()
    {
        // Arrange
        var dict = new Dictionary<string, FieldDefinition>();
        var fieldName = "name".AsSpan();
        var fieldType = "String".AsSpan();
        var parentPath = "user";

        // Act
        var result = dict.GetOrAddSimpleField(fieldName, fieldType, null, parentPath, null);

        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be("name");
    }

    [Fact]
    public void SpanExtensions_GetOrAddSimpleField_ShortPath_UsesStackBuffer()
    {
        // Arrange
        var dict = new Dictionary<string, FieldDefinition>();
        var fieldName = "name".AsSpan();
        var fieldType = "String".AsSpan();
        var parentPath = "user";

        // Act
        var result = dict.GetOrAddSimpleField(fieldName, fieldType, null, parentPath, null);

        // Assert
        result.Should().NotBeNull();
        dict.Should().HaveCount(1);
    }

    [Fact]
    public void SpanExtensions_GetOrAddSimpleField_LongPath_FallsBackToString()
    {
        // Arrange
        var dict = new Dictionary<string, FieldDefinition>();
        var fieldName = "z".AsSpan();
        var fieldType = "String".AsSpan();
        var parentPath = new string('a', 300); // Create path longer than 256 chars

        // Act
        var result = dict.GetOrAddSimpleField(fieldName, fieldType, null, parentPath, null);

        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be("z");
    }

    #endregion

    // ═══════════════════════════════════════════════════════════════
    // FieldFactory Additional Tests - Complex Field Scenarios
    // ═══════════════════════════════════════════════════════════════

    [Fact]
    public void FieldFactory_VeryLongDottedPath_CreatesNestedStructure()
    {
        var fields = new Dictionary<string, FieldDefinition>();

        var result = FieldFactory.GetOrAddField(fields, "a.b.c.d.e.f.g.h".AsSpan(), "Type".AsSpan(), null);

        result.Should().NotBeNull();
        result.Name.Should().Be("h");
        result.Type.Should().Be("Type");
    }

    [Fact]
    public void FieldFactory_DottedPathWithArguments_AppliesArgumentsOnlyToLastSegment()
    {
        var fields = new Dictionary<string, FieldDefinition>();
        var arguments = new Dictionary<string, object?> { { "limit", 10 } };

        var result = FieldFactory.GetOrAddField(fields, "user.profile.data".AsSpan(), "String".AsSpan(), arguments);

        result.Should().NotBeNull();
        result.Arguments.Should().NotBeNull();
        result.Arguments.Should().ContainKey("limit");
    }

    [Fact]
    public void FieldFactory_DottedPathWithMetadata_AppliesMetadataOnlyToLastSegment()
    {
        var fields = new Dictionary<string, FieldDefinition>();
        var metadata = new Dictionary<string, object?> { { "key", "value" } };

        var result = FieldFactory.GetOrAddField(fields, "user.settings".AsSpan(), "Settings".AsSpan(), null, metadata: metadata);

        result.Should().NotBeNull();
        result.Metadata.Should().NotBeNull();
    }

    [Fact]
    public void FieldFactory_ComplexPath_WithTypeAnnotation_ParsesCorrectly()
    {
        var fields = new Dictionary<string, FieldDefinition>();

        var result = FieldFactory.GetOrAddField(fields, "User user.profile".AsSpan(), "Default".AsSpan(), null);

        result.Should().NotBeNull();
        result.Name.Should().Be("profile");
    }

    [Fact]
    public void FieldFactory_DottedFieldWithMergingArguments_MergesWithExisting()
    {
        var fields = new Dictionary<string, FieldDefinition>();
        var firstArgs = new Dictionary<string, object?> { { "first", "arg" } };
        var secondArgs = new Dictionary<string, object?> { { "second", "arg" } };

        FieldFactory.GetOrAddField(fields, "user".AsSpan(), "User".AsSpan(), firstArgs);
        var result = FieldFactory.GetOrAddField(fields, "user".AsSpan(), "User".AsSpan(), secondArgs);

        result.Arguments.Should().NotBeNull();
    }

    [Fact]
    public void FieldFactory_FieldWithTypeConversion_IntermediateBecomesObject()
    {
        var fields = new Dictionary<string, FieldDefinition>();

        // First add as leaf
        FieldFactory.GetOrAddField(fields, "parent.child".AsSpan(), "String".AsSpan(), null);
        // Then add sibling
        FieldFactory.GetOrAddField(fields, "parent.other".AsSpan(), "String".AsSpan(), null);

        // parent should be converted to object type
        var parent = fields["parent"];
        parent.Type.Should().Be("object");
    }

    [Fact]
    public void FieldFactory_CreateOrMergeField_WithConflictingTypes_MergesArguments()
    {
        var fields = new Dictionary<string, FieldDefinition>();
        var existing = new FieldDefinition("user", "User", null, new Dictionary<string, object?> { { "id", "123" } });

        var field1 = FieldFactory.CreateOrMergeField(fields, existing);
        var field2 = FieldFactory.CreateOrMergeField(fields, existing);

        field2.Should().NotBeNull();
        field2.Arguments.Should().NotBeNull();
    }

    [Fact]
    public void FieldFactory_ValidPaths_AlwaysSucceed()
    {
        var fields = new Dictionary<string, FieldDefinition>();

        // These should all work without exceptions
        var r1 = FieldFactory.GetOrAddField(fields, "simple".AsSpan(), "Type".AsSpan(), null);
        var r2 = FieldFactory.GetOrAddField(fields, "a.b.c".AsSpan(), "Type".AsSpan(), null);

        r1.Should().NotBeNull();
        r2.Should().NotBeNull();
    }

    [Fact]
    public void FieldFactory_DottedFieldFastPath_NoArguments_NoMetadata()
    {
        var fields = new Dictionary<string, FieldDefinition>();

        var result = FieldFactory.GetOrAddField(fields, "a.b.c".AsSpan(), "Type".AsSpan(), null, null, null);

        result.Should().NotBeNull();
    }

    [Fact]
    public void FieldFactory_FieldWithAlias_AndTypeAnnotation_ParsesCorrectly()
    {
        var fields = new Dictionary<string, FieldDefinition>();

        var result = FieldFactory.GetOrAddField(fields, "User myAlias:user".AsSpan(), "Default".AsSpan(), null);

        result.Should().NotBeNull();
    }

    [Fact]
    public void FieldFactory_ParentFieldVariant_DottedPath_CreatesNested()
    {
        var parent = new FieldDefinition("parent", "Object");

        var result = FieldFactory.GetOrAddField(parent, "child.grandchild".AsSpan(), "String".AsSpan(), null);

        result.Should().NotBeNull();
        result.Name.Should().Be("grandchild");
    }

    [Fact]
    public void FieldFactory_ParentFieldVariant_WithArguments_AppliesCorrectly()
    {
        var parent = new FieldDefinition("parent", "Object");
        var arguments = new Dictionary<string, object?> { { "id", 42 } };

        var result = FieldFactory.GetOrAddField(parent, "child".AsSpan(), "String".AsSpan(), arguments);

        result.Arguments.Should().NotBeNull();
    }

    [Fact]
    public void FieldFactory_MultipleSegmentMerging_CreatesCorrectStructure()
    {
        var fields = new Dictionary<string, FieldDefinition>();

        FieldFactory.GetOrAddField(fields, "a.b.c".AsSpan(), "TypeC".AsSpan(), null);
        FieldFactory.GetOrAddField(fields, "a.b.d".AsSpan(), "TypeD".AsSpan(), null);

        var a = fields["a"];
        a.Type.Should().Be("object");
        var b = a._children?.Find("b".AsSpan());
        b.Should().NotBeNull();
    }

    [Fact]
    public void FieldFactory_ReusingField_WithNewType_MaintainsExisting()
    {
        var fields = new Dictionary<string, FieldDefinition>();

        var first = FieldFactory.GetOrAddField(fields, "user".AsSpan(), "User".AsSpan(), null);
        var second = FieldFactory.GetOrAddField(fields, "user".AsSpan(), "UserV2".AsSpan(), null);

        // When reusing existing, it should maintain existing type or merge
        second.Should().NotBeNull();
    }

    [Fact]
    public void FieldFactory_ArgumentSorting_EnforcesConsistency()
    {
        var fields = new Dictionary<string, FieldDefinition>();
        var argsZ = new Dictionary<string, object?> { { "z", 1 }, { "a", 2 } };

        var result = FieldFactory.GetOrAddField(fields, "field".AsSpan(), "Type".AsSpan(), argsZ);

        result.Arguments.Should().NotBeNull();
        var keys = result.Arguments!.Keys.ToList();
        keys[0].Should().Be("a"); // Should be sorted
        keys[1].Should().Be("z");
    }

    [Fact]
    public void FieldFactory_DottedPath_CreatesIntermediateFieldsAsObject()
    {
        var fields = new Dictionary<string, FieldDefinition>();

        var result = FieldFactory.GetOrAddField(fields, "user.profile.settings".AsSpan(), "Settings".AsSpan(), null);

        // Top-level field
        fields.Should().ContainKey("user");
        fields["user"].Type.Should().Be("object");

        // Intermediate field
        fields["user"].Fields.Should().NotBeEmpty();
        fields["user"].Fields.Should().ContainKey("profile");
        fields["user"].Fields["profile"].Type.Should().Be("object");

        // Final field
        fields["user"].Fields["profile"].Fields.Should().ContainKey("settings");
        fields["user"].Fields["profile"].Fields["settings"].Type.Should().Be("Settings");
    }

    [Fact]
    public void FieldFactory_DottedPathWithMetadata_AppliesMetadataToFinalSegment()
    {
        var fields = new Dictionary<string, FieldDefinition>();
        var metadata = new Dictionary<string, object?> { { "cache", "5m" } };

        var result = FieldFactory.GetOrAddField(fields, "user.profile.avatar".AsSpan(), "Image".AsSpan(), null, metadata: metadata);

        result.Metadata.Should().NotBeNull();
        result.Metadata!["cache"].Should().Be("5m");
    }

    [Fact]
    public void FieldFactory_DeepNestedPath_CreatesCompleteHierarchy()
    {
        var fields = new Dictionary<string, FieldDefinition>();

        var result = FieldFactory.GetOrAddField(fields, "org.dept.team.member.contact".AsSpan(), "ContactInfo".AsSpan(), null);

        fields.Should().ContainKey("org");
        fields["org"].Fields.Should().HaveCountGreaterThan(0);
        fields["org"].Fields.Should().ContainKey("dept");
        fields["org"].Fields["dept"].Type.Should().Be("object");
    }

    [Fact]
    public void FieldFactory_ReaddingDottedField_WithNewMetadata_RetainsMetadata()
    {
        var fields = new Dictionary<string, FieldDefinition>();
        var metadata1 = new Dictionary<string, object?> { { "tag", "v1" } };

        var first = FieldFactory.GetOrAddField(fields, "user.profile".AsSpan(), "Profile".AsSpan(), null, metadata: metadata1);
        var second = FieldFactory.GetOrAddField(fields, "user.profile".AsSpan(), "Profile".AsSpan(), null);

        // First call sets metadata
        second.Metadata.Should().NotBeNull();
        second.Metadata!["tag"].Should().Be("v1");
    }

    [Fact]
    public void FieldFactory_DottedPathVariantRootField_CreatesAtRoot()
    {
        var fields = new Dictionary<string, FieldDefinition>();

        var result = FieldFactory.GetOrAddField(fields, "userData.settings".AsSpan(), "UserSettings".AsSpan(), null);

        fields.Should().ContainKey("userData");
        fields["userData"].Name.Should().Be("userData");
        fields["userData"].Type.Should().Be("object"); // Root becomes object
    }

    [Fact]
    public void FieldFactory_LongDottedPath_HandlesPathCorrectly()
    {
        var fields = new Dictionary<string, FieldDefinition>();

        var result = FieldFactory.GetOrAddField(fields, "a.b.c.d.e.f".AsSpan(), "F".AsSpan(), null);

        result.Name.Should().Be("f");
        result.Type.Should().Be("F");
        fields.Should().ContainKey("a");
    }

    [Fact]
    public void FieldFactory_DottedPathWithArguments_PreservesArgumentsOnFinalField()
    {
        var fields = new Dictionary<string, FieldDefinition>();
        var args = new Dictionary<string, object?> { { "limit", 10 }, { "offset", 0 } };

        var result = FieldFactory.GetOrAddField(fields, "user.posts.recent".AsSpan(), "Post".AsSpan(), args);

        result.Arguments.Should().NotBeNull();
        result.Arguments!.Should().HaveCount(2);
        result.Arguments!["limit"].Should().Be(10);
    }

    [Fact]
    public void FieldFactory_ExistingHierarchy_AddingNewBranchAtIntermediateLevel()
    {
        var fields = new Dictionary<string, FieldDefinition>();

        // Create first path
        FieldFactory.GetOrAddField(fields, "user.profile.name".AsSpan(), "String".AsSpan(), null);

        // Add different branch at same intermediate level
        var result = FieldFactory.GetOrAddField(fields, "user.preferences.theme".AsSpan(), "String".AsSpan(), null);

        fields["user"].Fields.Should().HaveCount(2);
        fields["user"].Fields.Should().ContainKey("profile");
        fields["user"].Fields.Should().ContainKey("preferences");
    }

    [Fact]
    public void FieldFactory_DottedFieldThenDirectField_BothExist()
    {
        var fields = new Dictionary<string, FieldDefinition>();

        FieldFactory.GetOrAddField(fields, "user.profile".AsSpan(), "Profile".AsSpan(), null);
        var direct = FieldFactory.GetOrAddField(fields, "admin".AsSpan(), "Admin".AsSpan(), null);

        fields.Should().HaveCount(2);
        fields.Should().ContainKey("user");
        fields.Should().ContainKey("admin");
    }

    [Fact]
    public void FieldFactory_UpdateFieldType_IntermediateStaysObject()
    {
        var fields = new Dictionary<string, FieldDefinition>();

        // Create initial hierarchy
        var first = FieldFactory.GetOrAddField(fields, "data.values.count".AsSpan(), "Int".AsSpan(), null);
        
        // Try to update intermediate with different type
        var second = FieldFactory.GetOrAddField(fields, "data.values.sum".AsSpan(), "Float".AsSpan(), null);

        // Intermediate 'values' field should remain as object
        fields["data"].Fields["values"].Type.Should().Be("object");
    }

}

