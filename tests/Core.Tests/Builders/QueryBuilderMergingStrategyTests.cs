using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using NGql.Core.Abstractions;
using NGql.Core.Tests.Extensions;
using Xunit;
using Xunit.Abstractions;
using static NGql.Core.Builders.QueryBuilder;

namespace NGql.Core.Tests.Builders;

public class QueryBuilderMergingStrategyTests(ITestOutputHelper testOutputHelper)
{
    [Theory]
    [InlineData(MergingStrategy.NeverMerge, MergingStrategy.MergeByFieldPath)]
    [InlineData(MergingStrategy.NeverMerge, MergingStrategy.MergeByDefault)]
    [InlineData(MergingStrategy.NeverMerge, MergingStrategy.NeverMerge)]
    public async Task RootNeverMerge_ShouldAlwaysReturnNeverMerge(MergingStrategy rootStrategy, MergingStrategy childStrategy)
    {
        // Arrange
        var rootBuilder = CreateDefaultBuilder("root", rootStrategy);
        var childBuilder = CreateDefaultBuilder("UserProfile", childStrategy)
            .AddField("tables.users.edges.node", ["id", "name"]);

        // Act
        rootBuilder.Include(childBuilder);

        // Assert
        rootBuilder.GetQueryPath("root").Should().Be("tables"); // We keep map for all builders, including self
        rootBuilder.GetQueryPath("UserProfile").Should().Be("tables"); // Should be separate definition

        rootBuilder.DefinitionsCount.Should().Be(1);

        await rootBuilder.Verify($"RootNeverMerge_{childStrategy}");
    }

    [Fact]
    public async Task ChildNeverMerge_ShouldAlwaysCreateSeparateDefinition()
    {
        // Arrange
        var rootBuilder = CreateDefaultBuilder("MergedQuery", MergingStrategy.MergeByFieldPath);
        var emailQueryBuilder = CreateDefaultBuilder("emailQuery")
            .AddField("EmailQuery:tables.users.edges.node", ["email"]); // Same root path, no arguments
        var idNameQueryBuilder = CreateDefaultBuilder("idName", MergingStrategy.NeverMerge)
            .AddField("UserProfileQuery:tables.users.edges.node", ["id", "name"]);

        // Act
        rootBuilder
            .Include(emailQueryBuilder)
            .Include(idNameQueryBuilder);

        // Assert
        rootBuilder.GetQueryPath("emailQuery").Should().Be("EmailQuery"); // Not changed
        rootBuilder.GetQueryPath("idName").Should().Be("UserProfileQuery"); // Should be separate definition

        rootBuilder.DefinitionsCount.Should().Be(2);

        await rootBuilder.Verify();
    }

    [Theory]
    [InlineData(MergingStrategy.MergeByFieldPath, MergingStrategy.MergeByFieldPath)]
    [InlineData(MergingStrategy.NeverMerge, MergingStrategy.NeverMerge)]
    public async Task RootMergeByDefault_ShouldUseChildStrategy(MergingStrategy childStrategy, MergingStrategy expectedBehavior)
    {
        // Arrange
        var rootBuilder = CreateDefaultBuilder("root")
            .AddField("tables.users", ["id"]);
        var childBuilder = CreateDefaultBuilder("child", childStrategy)
            .AddField("tables.users", ["id", "name"]);

        // Act
        rootBuilder.Include(childBuilder);

        // Assert
        switch (expectedBehavior)
        {
            case MergingStrategy.MergeByFieldPath:
                // Should merge due to field path compatibility
                rootBuilder.GetQueryPath("root").Should().Be("tables");
                rootBuilder.GetQueryPath("child").Should().Be("tables");

                rootBuilder.DefinitionsCount.Should().Be(1); // Root only (child merged)
                break;
            case MergingStrategy.NeverMerge:
                rootBuilder.GetQueryPath("root").Should().Be("tables");
                rootBuilder.GetQueryPath("child").Should().Be("tables_1");

                rootBuilder.DefinitionsCount.Should().Be(2); // Root (1) + separate child (1) = 2 definitions
                break;
        }
        
        await rootBuilder.Verify($"RootMergeByDefault_{childStrategy}_{expectedBehavior}");
    }

    [Fact]
    public void FieldPathBasedMerging_ShouldGroupByCommonPathsAndArguments()
    {
        // Arrange
        var rootBuilder = CreateDefaultBuilder("root", MergingStrategy.MergeByFieldPath)
            .AddField("tables.users", ["id", "name"]); // No arguments

        var childSamePath = CreateDefaultBuilder("samePath")
            .AddField("tables.users", ["email"]); // Same root path, no arguments

        var childWithArgs = CreateDefaultBuilder("withArgs")
            .AddField("tables.users.settings", new Dictionary<string, object?> { ["filter"] = "active" }, ["status"]);
        
        var childWithArgs2 = CreateDefaultBuilder("withArgs2")
            .AddField("tables.users.settings", new Dictionary<string, object?> { ["filter"] = "active" }, ["status"]);

        var childDifferentPath = CreateDefaultBuilder("differentPath")
            .AddField("tables.users.profiles", ["id", "bio"]);

        // Act
        rootBuilder.Include(childSamePath);
        rootBuilder.Include(childWithArgs);
        rootBuilder.Include(childWithArgs2);
        rootBuilder.Include(childDifferentPath);

        // Assert
        rootBuilder.GetQueryPath("samePath").Should().Be("tables"); // Same path, no args - should merge to main
        rootBuilder.GetQueryPath("withArgs").Should().Be("tables_1"); // Different argument pattern
        rootBuilder.GetQueryPath("withArgs2").Should().Be("tables_1"); // Different argument pattern
        rootBuilder.GetQueryPath("differentPath").Should().Be("tables"); // Same path prefix, no conflicting args - should merge

        rootBuilder.DefinitionsCount.Should().Be(2); // Root + withArgs = 2 separate definitions (differentPath merges with root)
    }

    [Fact]
    public async Task DotNotationMerging_SimpleNestedPaths_ShouldMergeCorrectly()
    {
        // Arrange
        var rootBuilder = CreateDefaultBuilder("root", MergingStrategy.MergeByFieldPath)
            .AddField("user.profile", ["name", "bio"]);

        var child1 = CreateDefaultBuilder("child1")
            .AddField("user.profile", ["email"]); // Same path segment

        var child2 = CreateDefaultBuilder("child2")
            .AddField("user.settings", ["theme"]); // Same root, different branch

        var child3 = CreateDefaultBuilder("child3")
            .AddField("company.info", ["name"]); // Completely different root

        // Act
        rootBuilder.Include(child1);
        rootBuilder.Include(child2);
        rootBuilder.Include(child3);

        // Assert basic merging behavior
        // child1 should merge - exact same path
        rootBuilder.GetQueryPath("child1").Should().Be("user");

        // child2 should not merge - same root, different branch
        rootBuilder.GetQueryPath("child2").Should().Be("user");

        // child3 should not merge - completely different root
        rootBuilder.GetQueryPath("child3").Should().Be("company");

        await rootBuilder.Verify("DotNotationMerging_SimpleNestedPaths");
    }

    [Fact]
    public void DotNotationMerging_DeepNestedPaths_ShouldAnalyzeDepth()
    {
        // Arrange
        var rootBuilder = CreateDefaultBuilder("root", MergingStrategy.MergeByFieldPath)
            .AddField("organization.department.team.members", ["id", "name"]);

        var child1 = CreateDefaultBuilder("child1")
            .AddField("organization.department.team.members", ["role"]); // Exact same deep path

        var child2 = CreateDefaultBuilder("child2")
            .AddField("organization.department.team.lead", ["name"]); // Same path to team level

        var child3 = CreateDefaultBuilder("child3")
            .AddField("organization.department.budget", ["amount"]); // Same to department level

        var child4 = CreateDefaultBuilder("child4")
            .AddField("organization.locations", ["address"]); // Same to organization level

        var child5 = CreateDefaultBuilder("child5")
            .AddField("company.departments", ["name"]); // Completely different root

        // Act
        rootBuilder.Include(child1);
        rootBuilder.Include(child2);
        rootBuilder.Include(child3);
        rootBuilder.Include(child4);
        rootBuilder.Include(child5);

        // Assert
        rootBuilder.GetQueryPath("root").Should().Be("organization");
        rootBuilder.GetQueryPath("child1").Should().Be("organization"); // Exact path match: organization.department.team.members
        rootBuilder.GetQueryPath("child2").Should().Be("organization"); // 3 segment match: organization.department.team
        rootBuilder.GetQueryPath("child3").Should().Be("organization"); // 2 segment match: organization.department
        rootBuilder.GetQueryPath("child4").Should().Be("organization"); // 1 segment match: organization
        rootBuilder.GetQueryPath("child5").Should().Be("company"); // 0 segment match: different root (company vs organization)

        rootBuilder.DefinitionsCount.Should().Be(2); // Root (1) + child5 separate (1) = 2 definitions
        rootBuilder.Definition.Fields.Should().HaveCount(2); // Only organization field

        // Verify the nested structure is properly merged
        var orgField = rootBuilder.Definition.Fields["organization"];
        orgField.Fields.Should().HaveCount(2); // department and locations

        var deptField = orgField.Fields["department"];
        deptField.Fields.Should().HaveCount(2); // team and budget

        var teamField = deptField.Fields["team"];
        teamField.Fields.Should().HaveCount(2); // members and lead
    }

    [Fact]
    public async Task DotNotationMerging_WithArguments_ShouldConsiderArgumentCompatibility()
    {
        // Arrange
        var rootBuilder = CreateDefaultBuilder("root", MergingStrategy.MergeByFieldPath)
            .AddField("user.posts", new Dictionary<string, object?> { ["limit"] = 10 }, ["title", "date"]);

        var childSamePathNoArgs = CreateDefaultBuilder("noArgs")
            .AddField("user.posts", ["content"]); // Same path, no arguments

        var childSamePathWithArgs = CreateDefaultBuilder("withArgs")
            .AddField("user.posts", new Dictionary<string, object?> { ["filter"] = "published" }, ["likes"]);

        // Act
        rootBuilder.Include(childSamePathNoArgs);
        rootBuilder.Include(childSamePathWithArgs);

        // Assert
        rootBuilder.GetQueryPath("root").Should().Be("user");
        rootBuilder.GetQueryPath("noArgs").Should().Be("user_1");
        rootBuilder.GetQueryPath("withArgs").Should().Be("user_2");

        rootBuilder.DefinitionsCount.Should().Be(3); // all the same path start - user
        
        await rootBuilder.Verify("DotNotationMerging_WithArguments");
    }

    [Fact]
    public async Task ComplexDotNotationScenario_MixedStrategies_ShouldRespectHierarchy()
    {
        // Arrange
        var rootBuilder = CreateDefaultBuilder("root", MergingStrategy.MergeByFieldPath)
            .AddField("app.user.profile.personal", ["firstName", "lastName"]);

        var childNeverMerge = CreateDefaultBuilder("neverMerge", MergingStrategy.NeverMerge)
            .AddField("app.user.profile.personal", ["middleName"]); // Exact same path but never merge

        var childDefault = CreateDefaultBuilder("default")
            .AddField("app.user.settings.preferences", ["theme", "language"]); // Same user branch

        // Act
        rootBuilder.Include(childNeverMerge);
        rootBuilder.Include(childDefault);

        // Assert
        rootBuilder.GetQueryPath("root").Should().Be("app");
        rootBuilder.GetQueryPath("neverMerge").Should().Be("app_1"); // Never merge overrides path similarity
        rootBuilder.GetQueryPath("default").Should().Be("app");

        rootBuilder.DefinitionsCount.Should().Be(2);
        
        await rootBuilder.Verify("ComplexDotNotationScenario_MixedStrategies");
    }

    [Fact]
    public void FieldPathMerging_WithAliases_ShouldUseOriginalFieldNames()
    {
        // Arrange
        var rootBuilder = CreateDefaultBuilder("root", MergingStrategy.MergeByFieldPath);

        // Add field with alias using shorthand string format
        rootBuilder.AddField("user.profile", ["String FullName:name", "Int age"]);

        var child1 = CreateDefaultBuilder("child1");
        // Same field path, same original field name but different alias - should merge
        child1.AddField("user.profile", ["String EmailAddress:email", "String FirstName:name"]);

        var child2 = CreateDefaultBuilder("child2");
        // Same field path, different original field name - should still merge due to path similarity
        child2.AddField("user.profile", ["String Biography:bio"]);

        // Act
        rootBuilder.Include(child1);
        rootBuilder.Include(child2);

        // Assert
        // Both children should merge because they have the same field path (user.profile)
        // regardless of aliases - the merging logic should look at original field names and paths
        rootBuilder.GetQueryPath("child1").Should().Be("user");
        rootBuilder.GetQueryPath("child2").Should().Be("user");

        rootBuilder.DefinitionsCount.Should().Be(1); // Root only (both children merged)
    }

    [Fact]
    public void MultiLevelPath_ShouldMergeByPathSegments()
    {
        // Arrange
        var rootBuilder = CreateDefaultBuilder("root", MergingStrategy.MergeByFieldPath)
            .AddField("user", ["id", "name"]); // Single level

        var childBuilder = CreateDefaultBuilder("child")
            .AddField("user.profile", ["bio"]); // Should merge under user field

        // Act
        rootBuilder.Include(childBuilder);

        // Assert
        rootBuilder.GetQueryPath("child").Should().Be("user");
        rootBuilder.Definition.Fields["user"].Fields.Should().ContainKey("profile");
    }

    [Fact]
    public async Task ComplexArguments_ShouldCompareRecursively()
    {
        // Arrange
        var complexArgs1 = new Dictionary<string, object?> {
            ["filter"] = new { status = "active", metadata = new { priority = 1 } }
        };
        var complexArgs2 = new Dictionary<string, object?> {
            ["filter"] = new { status = "active", metadata = new { priority = 1 } }
        };
        var complexArgs3 = new Dictionary<string, object?> {
            ["filter"] = new { status = "active", metadata = new { priority = 2 } }
        };
        var complexArgs4 = new Dictionary<string, object?> {
            ["filter"] = new { status = "active", metadata = new { priority = 3 } }
        };

        var rootBuilder = CreateDefaultBuilder("root", MergingStrategy.MergeByFieldPath)
            .AddField("users.profile", complexArgs1, ["id"]);
    
        var child1 = CreateDefaultBuilder("child1")
            .AddField("users.profile", complexArgs2, ["name"]); // Should merge - same complex args
    
        var child2 = CreateDefaultBuilder("child2")
            .AddField("users.profile", complexArgs3, ["email"]); // Should not merge - different nested value

        var child2Again = CreateDefaultBuilder("child2")
            .AddField("users", complexArgs4, ["email"]); // Should not merge - different nested value

        // Act
        rootBuilder.Include(child1);
        rootBuilder.Include(child2);
        rootBuilder.Include(child2Again);
    
        // Assert
        rootBuilder.GetQueryPath("root").Should().Be("users");
        rootBuilder.GetQueryPath("child1").Should().Be("users");
        rootBuilder.GetQueryPath("child2").Should().NotBe("users");

        await rootBuilder.Verify();
    }

    [Theory]
    [InlineData(10, "10", false)] // int vs string
    [InlineData(10, 10.0, false)] // int vs double
    [InlineData("true", true, false)] // string vs bool
    [InlineData(null, "", false)] // null vs empty string
    public void ArgumentValueTypes_ShouldMatterForMerging(object? value1, object? value2, bool shouldMerge)
    {
        var args1 = new Dictionary<string, object?> { ["param"] = value1 };
        var args2 = new Dictionary<string, object?> { ["param"] = value2 };
    
        var rootBuilder = CreateDefaultBuilder("root", MergingStrategy.MergeByFieldPath)
            .AddField("data", args1, ["field1"]);
    
        var childBuilder = CreateDefaultBuilder("child")
            .AddField("data", args2, ["field2"]);
    
        // Act
        rootBuilder.Include(childBuilder);
    
        // Assert
        if (shouldMerge)
            rootBuilder.GetQueryPath("child").Should().Be("root");
        else
            rootBuilder.GetQueryPath("child").Should().NotBe("root");
    }

    [Fact]
    public void FieldTypeConflict_ShouldThrowException()
    {
        // Arrange
        var rootBuilder = CreateDefaultBuilder("root", MergingStrategy.MergeByFieldPath);
        rootBuilder.AddField("user", ["String name"]);

        var childBuilder = CreateDefaultBuilder("child");
        childBuilder.AddField("user", ["Int name"]); // Same name, different type

        // Act & Assert
        var exception = Assert.Throws<QueryMergeException>(() => rootBuilder.Include(childBuilder));
        exception.Message.Should().Contain("Cannot merge query 'child' due to type conflicts in field 'user'");
    }

    [Fact]
    public void NestedFieldTypeConflict_ShouldPreventParentMerging()
    {
        // Arrange
        var rootBuilder = CreateDefaultBuilder("root", MergingStrategy.MergeByFieldPath)
            .AddField("user.profile.settings", ["theme"]);

        // Add conflicting field deep in the path
        rootBuilder.AddField("user.profile", ["String id"]);

        var childBuilder = CreateDefaultBuilder("child");
        childBuilder.AddField("user.profile", ["Int id"]); // Type conflict
        childBuilder.AddField("user.profile.settings", ["language"]);

        // Act & Assert
        var exception = Assert.Throws<QueryMergeException>(() => rootBuilder.Include(childBuilder));
        exception.Message.Should().Contain("Cannot merge query 'child' due to type conflicts in field 'user'");
    }

    [Theory]
    [InlineData(true)]  // null arguments
    [InlineData(false)] // empty dictionary
    public void NullVsEmptyArguments_ShouldBeEquivalent(bool useNull)
    {
        var rootBuilder = CreateDefaultBuilder("root", MergingStrategy.MergeByFieldPath);
        if (useNull)
            rootBuilder.AddField("users", ["id"]);
        else
            rootBuilder.AddField("users", new Dictionary<string, object?>(), ["id"]);
    
        var childBuilder = CreateDefaultBuilder("child");
        if (!useNull)
            childBuilder.AddField("users", ["name"]);
        else
            childBuilder.AddField("users", new Dictionary<string, object?>(), ["name"]);
    
        // Act
        rootBuilder.Include(childBuilder);
    
        // Assert
        rootBuilder.GetQueryPath("root").Should().Be("users");
        rootBuilder.GetQueryPath("child").Should().Be("users"); // Should merge - null == empty
        rootBuilder.DefinitionsCount.Should().Be(1); // Root only (child merged)
    }
}
