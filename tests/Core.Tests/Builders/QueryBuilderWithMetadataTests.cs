using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using NGql.Core.Builders;
using NGql.Core.Tests.Extensions;
using Xunit;

namespace NGql.Core.Tests.Builders;

public class QueryBuilderWithMetadataTests
{
    [Fact]
    public void Can_Set_Metadata_Using_QueryBuilder()
    {
        // Arrange
        var metadata = new Dictionary<string, object>
        {
            { "description", "User query with metadata" },
            { "version", "1.0" },
            { "deprecated", false }
        };

        // Act
        var queryBuilder = QueryBuilder
            .CreateDefaultBuilder("getUserWithMetadata")
            .WithMetadata(metadata)
            .AddField("users", ["id", "name"]);
        var result = queryBuilder.Definition;

        // Assert
        result.Should().NotBeNull();
        result.Metadata.Should().NotBeNull().And.HaveCount(3);
        result.Metadata!["description"].Should().Be("User query with metadata");
        result.Metadata!["version"].Should().Be("1.0");
        result.Metadata!["deprecated"].Should().Be(false);
    }

    [Fact]
    public void WithMetadata_Should_Merge_With_Existing_Metadata()
    {
        // Arrange
        var initialMetadata = new Dictionary<string, object>
        {
            { "description", "Initial query description" },
            { "version", "1.0" },
            { "tags", new[] { "user" } }
        };
        var additionalMetadata = new Dictionary<string, object>
        {
            { "description", "Updated query description" }, // Should override
            { "author", "Jane Smith" }, // Should add
            { "complexity", "medium" } // Should add
        };

        // Act
        var queryBuilder = QueryBuilder
            .CreateDefaultBuilder("mergeMetadataTest")
            .WithMetadata(initialMetadata)
            .WithMetadata(additionalMetadata)
            .AddField("users", ["id"]);
        var result = queryBuilder.Definition;

        // Assert
        result.Should().NotBeNull();
        result.Metadata.Should().NotBeNull().And.HaveCount(5);
        result.Metadata!["description"].Should().Be("Updated query description"); // Overridden
        result.Metadata!["version"].Should().Be("1.0"); // Preserved
        result.Metadata!["tags"].Should().BeEquivalentTo(new[] { "user" }); // Preserved
        result.Metadata!["author"].Should().Be("Jane Smith"); // Added
        result.Metadata!["complexity"].Should().Be("medium"); // Added
    }

    [Fact]
    public void WithMetadata_Should_Handle_Nested_Dictionary_Merging()
    {
        // Arrange
        var initialMetadata = new Dictionary<string, object>
        {
            { "config", new Dictionary<string, object> 
                { 
                    { "caching", true }, 
                    { "timeout", 5000 },
                    { "retries", 3 }
                } 
            },
            { "version", "1.0" }
        };
        var additionalMetadata = new Dictionary<string, object>
        {
            { "config", new Dictionary<string, object> 
                { 
                    { "timeout", 10000 }, // Should override
                    { "logging", true }, // Should add
                    { "debug", false } // Should add
                } 
            },
            { "environment", "production" }
        };

        // Act
        var queryBuilder = QueryBuilder
            .CreateDefaultBuilder("nestedMetadataTest")
            .WithMetadata(initialMetadata)
            .WithMetadata(additionalMetadata)
            .AddField("users", ["id"]);
        var result = queryBuilder.Definition;

        // Assert
        result.Should().NotBeNull();
        result.Metadata.Should().NotBeNull().And.HaveCount(3);
        result.Metadata!["version"].Should().Be("1.0");
        result.Metadata!["environment"].Should().Be("production");
        
        var configMetadata = result.Metadata!["config"] as Dictionary<string, object?>;
        configMetadata.Should().NotBeNull().And.HaveCount(5);
        configMetadata!["caching"].Should().Be(true); // Preserved from initial
        configMetadata!["timeout"].Should().Be(10000); // Overridden by additional
        configMetadata!["retries"].Should().Be(3); // Preserved from initial
        configMetadata!["logging"].Should().Be(true); // Added from additional
        configMetadata!["debug"].Should().Be(false); // Added from additional
    }

    [Fact]
    public void WithMetadata_Should_Handle_Empty_Metadata()
    {
        // Arrange
        var emptyMetadata = new Dictionary<string, object>();

        // Act
        var queryBuilder = QueryBuilder
            .CreateDefaultBuilder("emptyMetadataTest")
            .WithMetadata(emptyMetadata)
            .AddField("users", ["id"]);
        var result = queryBuilder.Definition;

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
            { "description", "Query with null values" },
            { "nullable_field", null! }, // Explicit null
            { "empty_string", "" },
            { "zero_value", 0 }
        };

        // Act
        var queryBuilder = QueryBuilder
            .CreateDefaultBuilder("nullValuesTest")
            .WithMetadata(metadata)
            .AddField("users", ["id"]);
        var result = queryBuilder.Definition;

        // Assert
        result.Should().NotBeNull();
        result.Metadata.Should().NotBeNull().And.HaveCount(4);
        result.Metadata!["description"].Should().Be("Query with null values");
        result.Metadata!["nullable_field"].Should().BeNull();
        result.Metadata!["empty_string"].Should().Be("");
        result.Metadata!["zero_value"].Should().Be(0);
    }

    [Fact]
    public void WithMetadata_Should_Support_Complex_Object_Values()
    {
        // Arrange
        var complexObject = new
        {
            QueryInfo = new
            {
                Name = "Complex User Query",
                Parameters = new[] { "id", "name", "email" },
                Filters = new { Active = true, MinAge = 18 }
            },
            Performance = new
            {
                ExpectedLatency = "< 100ms",
                CacheEnabled = true
            }
        };
        var metadata = new Dictionary<string, object>
        {
            { "query_details", complexObject },
            { "simple_flag", true }
        };

        // Act
        var queryBuilder = QueryBuilder
            .CreateDefaultBuilder("complexObjectTest")
            .WithMetadata(metadata)
            .AddField("users", ["id", "name"]);
        var result = queryBuilder.Definition;

        // Assert
        result.Should().NotBeNull();
        result.Metadata.Should().NotBeNull().And.HaveCount(2);
        result.Metadata!["query_details"].Should().Be(complexObject);
        result.Metadata!["simple_flag"].Should().Be(true);
    }

    [Fact]
    public void WithMetadata_Should_Be_Chainable_With_Other_Methods()
    {
        // Arrange
        var metadata = new Dictionary<string, object>
        {
            { "description", "Chainable query builder test" },
            { "priority", "high" },
            { "estimated_cost", 0.05 }
        };

        // Act
        var queryBuilder = QueryBuilder
            .CreateDefaultBuilder("chainableTest")
            .WithMetadata(metadata)
            .AddField("users", new Dictionary<string, object?> { { "limit", 10 } }, ["id", "name"])
            .AddField("posts", ["title", "content"]);
        var result = queryBuilder.Definition;

        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be("chainableTest");
        result.Fields.Should().ContainKeys("users", "posts");
        result.Metadata.Should().NotBeNull().And.HaveCount(3);
        result.Metadata!["description"].Should().Be("Chainable query builder test");
        result.Metadata!["priority"].Should().Be("high");
        result.Metadata!["estimated_cost"].Should().Be(0.05);
    }

    [Fact]
    public void WithMetadata_Should_Handle_Case_Insensitive_Keys()
    {
        // Arrange
        var initialMetadata = new Dictionary<string, object>
        {
            { "Description", "Initial query description" },
            { "VERSION", "1.0" },
            { "Author", "John Doe" }
        };
        var additionalMetadata = new Dictionary<string, object>
        {
            { "description", "Updated query description" }, // Different case
            { "version", "2.0" }, // Different case
            { "TAGS", new[] { "query", "test" } } // Different case, new field
        };

        // Act
        var queryBuilder = QueryBuilder
            .CreateDefaultBuilder("caseInsensitiveTest")
            .WithMetadata(initialMetadata)
            .WithMetadata(additionalMetadata);
        var result = queryBuilder.Definition;

        // Assert
        result.Should().NotBeNull();
        result.Metadata.Should().NotBeNull().And.HaveCount(4);
        
        // Check that keys exist (case insensitive)
        var hasDescription = result.Metadata!.Keys.Any(k => string.Equals(k, "description", StringComparison.OrdinalIgnoreCase));
        var hasVersion = result.Metadata!.Keys.Any(k => string.Equals(k, "version", StringComparison.OrdinalIgnoreCase));
        var hasAuthor = result.Metadata!.Keys.Any(k => string.Equals(k, "author", StringComparison.OrdinalIgnoreCase));
        var hasTags = result.Metadata!.Keys.Any(k => string.Equals(k, "tags", StringComparison.OrdinalIgnoreCase));
        
        hasDescription.Should().BeTrue();
        hasVersion.Should().BeTrue();
        hasAuthor.Should().BeTrue();
        hasTags.Should().BeTrue();
        
        // Values should be updated
        var descriptionValue = result.Metadata!.Where(kvp => 
            string.Equals(kvp.Key, "description", StringComparison.OrdinalIgnoreCase))
            .Select(kvp => kvp.Value).FirstOrDefault();
        var versionValue = result.Metadata!.Where(kvp => 
            string.Equals(kvp.Key, "version", StringComparison.OrdinalIgnoreCase))
            .Select(kvp => kvp.Value).FirstOrDefault();
            
        descriptionValue.Should().Be("Updated query description");
        versionValue.Should().Be("2.0");
    }

    [Fact]
    public void WithMetadata_Multiple_Calls_Should_Accumulate_Metadata()
    {
        // Arrange
        var metadata1 = new Dictionary<string, object> { { "step1", "first metadata" } };
        var metadata2 = new Dictionary<string, object> { { "step2", "second metadata" } };
        var metadata3 = new Dictionary<string, object> { { "step3", "third metadata" } };
        var metadata4 = new Dictionary<string, object> { { "step1", "overridden first metadata" } }; // Override

        // Act
        var queryBuilder = QueryBuilder
            .CreateDefaultBuilder("accumulateTest")
            .WithMetadata(metadata1)
            .WithMetadata(metadata2)
            .WithMetadata(metadata3)
            .WithMetadata(metadata4)
            .AddField("users", ["id"]);
        var result = queryBuilder.Definition;

        // Assert
        result.Should().NotBeNull();
        result.Metadata.Should().NotBeNull().And.HaveCount(3);
        result.Metadata!["step1"].Should().Be("overridden first metadata"); // Overridden
        result.Metadata!["step2"].Should().Be("second metadata");
        result.Metadata!["step3"].Should().Be("third metadata");
    }

    [Fact]
    public async Task WithMetadata_Should_Not_Affect_Query_Generation()
    {
        // Arrange
        var metadata = new Dictionary<string, object>
        {
            { "description", "This metadata should not appear in query text" },
            { "version", "1.0" },
            { "tags", new[] { "test", "metadata" } }
        };

        // Act
        var queryBuilder = QueryBuilder
            .CreateDefaultBuilder("queryWithExtraInfo")
            .WithMetadata(metadata)
            .AddField("users", ["id", "name", "email"]);

        // Assert
        await queryBuilder.Verify("WithMetadata_Should_Not_Affect_Query_Generation");
        
        // Verify metadata is preserved but doesn't affect query text
        var definition = queryBuilder.Definition;
        definition.Metadata.Should().NotBeNull().And.HaveCount(3);
        
        // Convert to string and verify no metadata appears in query text
        var queryText = queryBuilder.ToString();
        queryText.Should().NotContain("description");
        queryText.Should().NotContain("version");
        queryText.Should().NotContain("tags");
        queryText.Should().NotContain("metadata");
        queryText.Should().Contain("queryWithExtraInfo");
        queryText.Should().Contain("users");
        queryText.Should().Contain("id");
        queryText.Should().Contain("name");
        queryText.Should().Contain("email");
    }

    [Fact]
    public void WithMetadata_Should_Work_With_Different_Merging_Strategies()
    {
        // Arrange
        var metadata = new Dictionary<string, object>
        {
            { "strategy", "never_merge" },
            { "test_case", "merging_strategy_compatibility" }
        };

        // Act & Assert - Test with NeverMerge strategy
        var neverMergeBuilder = QueryBuilder
            .CreateDefaultBuilder("neverMergeTest", MergingStrategy.NeverMerge)
            .WithMetadata(metadata)
            .AddField("users", ["id"]);
        var neverMergeResult = neverMergeBuilder.Definition;

        neverMergeResult.Metadata.Should().NotBeNull().And.HaveCount(2);
        neverMergeResult.Metadata!["strategy"].Should().Be("never_merge");

        // Act & Assert - Test with MergeByFieldPath strategy
        var mergeByPathBuilder = QueryBuilder
            .CreateDefaultBuilder("mergeByPathTest", MergingStrategy.MergeByFieldPath)
            .WithMetadata(metadata)
            .AddField("users", ["id"]);
        var mergeByPathResult = mergeByPathBuilder.Definition;

        mergeByPathResult.Metadata.Should().NotBeNull().And.HaveCount(2);
        mergeByPathResult.Metadata!["strategy"].Should().Be("never_merge");
    }

    [Fact]
    public void WithMetadata_Should_Preserve_Metadata_During_Query_Include_Operations()
    {
        // Arrange
        var rootMetadata = new Dictionary<string, object>
        {
            { "query_type", "root" },
            { "description", "Root query with metadata" }
        };
        var childMetadata = new Dictionary<string, object>
        {
            { "query_type", "child" },
            { "description", "Child query with metadata" }
        };

        var rootBuilder = QueryBuilder
            .CreateDefaultBuilder("rootQuery", MergingStrategy.MergeByFieldPath)
            .WithMetadata(rootMetadata)
            .AddField("users", ["id", "name"]);

        var childBuilder = QueryBuilder
            .CreateDefaultBuilder("childQuery")
            .WithMetadata(childMetadata)
            .AddField("users", ["email"]);

        // Act
        rootBuilder.Include(childBuilder);
        var result = rootBuilder.Definition;

        // Assert
        result.Should().NotBeNull();
        result.Metadata.Should().NotBeNull().And.HaveCount(2);
        result.Metadata!["query_type"].Should().Be("root"); // Root metadata preserved
        result.Metadata!["description"].Should().Be("Root query with metadata");
        
        // Verify the child query was included properly
        result.Fields["users"].Fields.Should().ContainKeys("id", "name", "email");
    }
}
