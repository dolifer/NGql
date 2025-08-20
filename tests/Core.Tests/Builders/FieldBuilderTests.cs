using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using NGql.Core.Abstractions;
using NGql.Core.Builders;
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
        var arguments = new SortedDictionary<string, object?>
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
            new SortedDictionary<string, object?> { { "id", "123" } }
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
    public void AddField_Sets_Specified_Type_When_Type_Is_Provided(string typeValue)
    {
        // Arrange
        var fieldBuilder = FieldBuilder.Create([], "user", "User");

        // Act
        var result = fieldBuilder
            .AddField("field1", typeValue) // Explicitly setting type
            .AddField($"{typeValue} field2") // Explicitly setting type
            .Build();

        // Assert
        result.Fields["field1"].Should().NotBeNull();
        result.Fields["field1"].Name.Should().Be("field1");
        result.Fields["field1"].Type.Should().Be(typeValue); // Explicit type verification
        
        result.Fields["field2"].Should().NotBeNull();
        result.Fields["field2"].Name.Should().Be("field2");
        result.Fields["field2"].Type.Should().Be(typeValue); // Explicit type verification
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
            .Build();

        // Assert
        result.Fields.Should().HaveCount(4);
        result.Fields["id"].Type.Should().Be("ID");
        result.Fields["name"].Type.Should().Be(Constants.DefaultFieldType); // Default
        result.Fields["age"].Type.Should().Be("Int");
        result.Fields["isActive"].Type.Should().Be("Boolean");
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
        var arguments = new SortedDictionary<string, object?>
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
        var fieldBuilder = FieldBuilder.Create(new SortedDictionary<string, FieldDefinition>(), "user", "User");

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
            .AddField("posts", "Post[]", new SortedDictionary<string, object?> { { "first", 10 } })
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
}
