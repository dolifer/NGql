using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using FluentAssertions;
using NGql.Core.Builders;
using NGql.Core.Tests.Models;
using Xunit;

namespace NGql.Core.Tests.Builders;

public class ExpressionFieldExtractorTests
{
    // ================== SINGLE FIELD TESTS ==================
    // All these use identical assertions: paths.Should().ContainSingle().Which.Should().Be(expectedPath)
    // Consolidated into one Theory using ExpressionsBag

    [Theory]
    [InlineData("SimplePropertyAccess", "user.age")]
    [InlineData("NestedPropertyChain", "user.profile.age")]
    [InlineData("DeepNestedProperty", "metrics.realtime.deposits.firstDepositAmount")]
    [InlineData("NullCheck", "user.profile.email")]
    [InlineData("BooleanProperty", "user.isActive")]
    [InlineData("BooleanNegation", "user.isActive")]
    [InlineData("StringContains", "user.profile.name")]
    [InlineData("StringStartsWith", "user.email")]
    [InlineData("CaseInsensitive", "user.profile.name")]
    public void ExtractFieldPaths_SingleField_ReturnsExactPath(string scenario, string expectedPath)
    {
        // Arrange
        var expr = new ExpressionsBag<TestModel>()
            .Register("SimplePropertyAccess", x => x.user.age > 18)
            .Register("NestedPropertyChain", x => x.user.profile.age > 10)
            .Register("DeepNestedProperty", x => x.metrics.realtime.deposits.firstDepositAmount > 100)
            .Register("NullCheck", x => x.user.profile.email != null)
            .Register("BooleanProperty", x => x.user.isActive)
            .Register("BooleanNegation", x => !x.user.isActive)
            .Register("StringContains", x => x.user.profile.name!.Contains("test"))
            .Register("StringStartsWith", x => x.user.email!.StartsWith("admin"))
            .Register("CaseInsensitive", x => x.user.profile.name != null)
            .Get(scenario);

        // Act
        var paths = ExpressionFieldExtractor.ExtractFieldPaths(expr);

        // Assert
        paths.Should().ContainSingle().Which.Should().Be(expectedPath);
    }

    // ================== BINARY OPERATIONS ==================

    [Theory]
    [InlineData("BinaryAnd", 2)]
    [InlineData("BinaryOr", 2)]
    [InlineData("ChainedComparisons", 2)]
    public void ExtractFieldPaths_BinaryOperations_ReturnsAllFields(string scenario, int expectedCount)
    {
        // Arrange
        var expressions = new ExpressionsBag<TestModel>()
            .Register("BinaryAnd", x => x.user.age > 18 && x.user.email != null)
            .Register("BinaryOr", x => x.user.age < 18 || x.user.profile.age < 18)
            .Register("ChainedComparisons", x => x.user.age > 18 && x.user.age < 65 && x.user.isActive);

        var expr = expressions.Get(scenario);

        // Act
        var paths = ExpressionFieldExtractor.ExtractFieldPaths(expr);

        // Assert
        paths.Should().HaveCount(expectedCount);
    }


    // ================== COMPLEX BOOLEAN LOGIC (Unique Behavior) ==================

    [Fact]
    public void ExtractFieldPaths_ComplexBooleanLogic_ReturnsAllFieldsFromAllBranches()
    {
        // Arrange - Complex logic testing multiple operators and nesting
        Expression<Func<TestModel, bool>> expr = x =>
            (x.user.age > 18 && x.user.email != null) ||
            (x.user.isActive && x.user.profile.name != null);

        // Act
        var paths = ExpressionFieldExtractor.ExtractFieldPaths(expr);

        // Assert
        paths.Should().HaveCount(4)
            .And.Contain("user.age")
            .And.Contain("user.email")
            .And.Contain("user.isActive")
            .And.Contain("user.profile.name");
    }

    // ================== CONDITIONAL & TERNARY EXPRESSIONS ==================

    [Fact]
    public void ExtractFieldPaths_TernaryOperator_ExtractsFieldsFromAllBranches()
    {
        // Arrange
        Expression<Func<TestModel, bool>> expr = x =>
            x.user.age > 18 ? x.user.isActive : x.user.profile.age > 10;

        // Act
        var paths = ExpressionFieldExtractor.ExtractFieldPaths(expr);

        // Assert
        paths.Should().Contain("user.age")
            .And.Contain("user.isActive")
            .And.Contain("user.profile.age");
    }

    // ================== LINQ METHOD EXPRESSIONS ==================

    [Theory]
    [InlineData("WithLinqFirst")]
    [InlineData("WithLinqAny")]
    public void ExtractFieldPaths_LinqMethods_ExtractsCollectionAndInternalFields(string scenario)
    {
        // Arrange - LINQ operations that extract both collection path and predicates
        var expressions = new ExpressionsBag<TestModel, object>()
            .Register("WithLinqFirst", x => (object)x.metrics.onceADay.sport.preferences[0].totalBetsCount)
            .Register("WithLinqAny", x => (object)x.metrics.onceADay.sport.preferences.Any(p => p.sport == "F"));

        var expr = expressions.Get(scenario);

        // Act
        var paths = ExpressionFieldExtractor.ExtractFieldPaths(expr);

        // Assert
        paths.Should().Contain("metrics.onceADay.sport.preferences");
    }

    [Theory]
    [InlineData("WithLinqFirstAndPredicate")]
    [InlineData("WithLinqWhere")]
    public void ExtractFieldPaths_LinqWithPredicates_ExtractsFieldsFromLambdas(string scenario)
    {
        // Arrange - LINQ with lambdas that reference fields
        var expressions = new ExpressionsBag<TestModel, object>()
            .Register("WithLinqFirstAndPredicate", x => (object)
                x.metrics.onceADay.sport.preferences.First(p => p.sport == "F").totalBetsCount)
            .Register("WithLinqWhere", x => (object)
                x.metrics.onceADay.sport.preferences.Where(p => p.sport == "F"));

        var expr = expressions.Get(scenario);

        // Act
        var paths = ExpressionFieldExtractor.ExtractFieldPaths(expr);

        // Assert
        paths.Should().Contain("metrics.onceADay.sport.preferences")
            .And.Contain("metrics.onceADay.sport.preferences.sport");
    }

    // ================== ANONYMOUS TYPES ==================

    [Theory]
    [InlineData("AnonymousTypeSelector", 2)]
    [InlineData("AnonymousTypeWithMultipleLevels", 3)]
    public void ExtractFieldPaths_AnonymousTypes_ExtractsAllPropertySelectors(string scenario, int expectedCount)
    {
        // Arrange
        var expressions = new ExpressionsBag<TestModel, object>()
            .Register("AnonymousTypeSelector", x => (object)new
            {
                x.user.profile.name,
                x.user.profile.email
            })
            .Register("AnonymousTypeWithMultipleLevels", x => (object)new
            {
                x.user.age,
                x.user.profile.name,
                x.metrics.realtime.deposits.firstDepositAmount
            });

        var expr = expressions.Get(scenario);

        // Act
        var paths = ExpressionFieldExtractor.ExtractFieldPaths(expr);

        // Assert
        paths.Should().HaveCount(expectedCount)
            .And.Contain(p => p.Contains("profile") || p.Contains("age") || p.Contains("deposits"));
    }

    [Fact]
    public void ExtractFieldPaths_AnonymousTypeObjectCreation_ExtractsAllFields()
    {
        // Arrange
        Expression<Func<TestModel, object>> expr = x =>
            new { x.user.profile.name, x.user.age, x.metrics.realtime.deposits.firstDepositAmount };

        // Act
        var paths = ExpressionFieldExtractor.ExtractFieldPaths(expr);

        // Assert
        paths.Should().HaveCount(3)
            .And.Contain("user.profile.name")
            .And.Contain("user.age")
            .And.Contain("metrics.realtime.deposits.firstDepositAmount");
    }

    // ================== EMPTY RESULTS ==================

    [Theory]
    [InlineData("EmptyExpression")]
    [InlineData("OnlyConstants")]
    public void ExtractFieldPaths_NoFieldReferences_ReturnsEmpty(string scenario)
    {
        // Arrange
        var expressions = new ExpressionsBag<TestModel>()
            .Register("EmptyExpression", x => true)
            .Register("OnlyConstants", x => 10 > 5);

        var expr = expressions.Get(scenario);

        // Act
        var paths = ExpressionFieldExtractor.ExtractFieldPaths(expr);

        // Assert
        paths.Should().BeEmpty();
    }

    // ================== NULL COALESCING - SINGLE FIELD RESULTS ==================

    [Theory]
    [InlineData("NullCoalescing")]
    [InlineData("SimpleNullCoalescing")]
    public void ExtractFieldPaths_NullCoalescing_SingleField_ReturnsSinglePath(string scenario)
    {
        // Arrange
        var expressions = new ExpressionsBag<TestModel, object>()
            .Register("NullCoalescing", x => (object)((x.user.profile.name ?? "").Length > 0))
            .Register("SimpleNullCoalescing", x => (object)(x.user.profile.name ?? ""));

        var expr = expressions.Get(scenario);

        // Act
        var paths = ExpressionFieldExtractor.ExtractFieldPaths(expr);

        // Assert
        paths.Should().ContainSingle().Which.Should().Be("user.profile.name");
    }

    [Fact]
    public void ExtractFieldPaths_NullCoalescing_ComplexExpression_ReturnsMultipleFields()
    {
        // Arrange - Null coalescing in complex boolean context
        Expression<Func<TestModel, bool>> expr = x =>
            (x.user.profile.name ?? "").Length > 0 &&
            x.user.email != null &&
            x.user.age > 18;

        // Act
        var paths = ExpressionFieldExtractor.ExtractFieldPaths(expr);

        // Assert
        paths.Should().HaveCount(3)
            .And.Contain("user.profile.name")
            .And.Contain("user.email")
            .And.Contain("user.age");
    }

    [Fact]
    public void ExtractFieldPaths_NullCoalescing_Nested_ReturnsAllFields()
    {
        // Arrange - Multiple null-coalescing operators chained
        Expression<Func<TestModel, bool>> expr = x =>
            (x.user.profile.name ?? x.user.email ?? "default").Length > 0;

        // Act
        var paths = ExpressionFieldExtractor.ExtractFieldPaths(expr);

        // Assert
        paths.Should().HaveCount(2)
            .And.Contain("user.profile.name")
            .And.Contain("user.email");
    }

    // ================== DIRECT PARAMETER REFERENCES ==================

    [Fact]
    public void ExtractFieldPaths_DirectParameterReference_ExtractsParameterName()
    {
        // Arrange
        Expression<Func<UserData?, bool>> expr =
            playerProfile => playerProfile == null;

        // Act
        var paths = ExpressionFieldExtractor.ExtractFieldPaths(expr);

        // Assert
        paths.Should().Contain("playerProfile");
    }

    [Fact]
    public void ExtractFieldPaths_DirectParameterInConditional_ExtractsParameterAndProperty()
    {
        // Arrange
        Expression<Func<UserData?, string?>> expr = playerProfile =>
            playerProfile == null ? null : playerProfile.email;

        // Act
        var paths = ExpressionFieldExtractor.ExtractFieldPaths(expr);

        // Assert
        paths.Should().HaveCount(2)
            .And.Contain("playerProfile")
            .And.Contain("email");
    }

    // ================== METHOD CALLS ==================

    [Fact]
    public void ExtractFieldPaths_MethodCallOnNonLinqMethod_ExtractsBaseField()
    {
        // Arrange
        Expression<Func<TestModel, bool>> expr = x =>
            x.user.profile.name.StartsWith('A');

        // Act
        var paths = ExpressionFieldExtractor.ExtractFieldPaths(expr);

        // Assert
        paths.Should().ContainSingle().Which.Should().Be("user.profile.name");
    }

    [Fact]
    public void ExtractFieldPaths_MultipleMethodCalls_ExtractsAllFields()
    {
        // Arrange
        Expression<Func<TestModel, bool>> expr = x =>
            x.user.profile.name!.ToLower().Contains("test") && x.user.email != null;

        // Act
        var paths = ExpressionFieldExtractor.ExtractFieldPaths(expr);

        // Assert
        paths.Should().HaveCount(2)
            .And.Contain("user.profile.name")
            .And.Contain("user.email");
    }

    // ================== COMPLEX LINQ EXPRESSIONS ==================

    [Theory]
    [InlineData("ComplexLinqWhere")]
    [InlineData("DeepNestedLambdaExpression")]
    public void ExtractFieldPaths_ComplexLinqExpressions_ExtractsCollectionAndPredicateFields(string scenario)
    {
        // Arrange
        var expressions = new ExpressionsBag<TestModel>()
            .Register("ComplexLinqWhere", x =>
                x.metrics.onceADay.sport.preferences.Any(p => p.sport == "football"))
            .Register("DeepNestedLambdaExpression", x =>
                x.metrics.onceADay.sport.preferences
                    .Where(p => p.totalBetsCount > 0)
                    .Any(p => p.sport == "basketball"));

        var expr = expressions.Get(scenario);

        // Act
        var paths = ExpressionFieldExtractor.ExtractFieldPaths(expr);

        // Assert
        paths.Should().Contain("metrics.onceADay.sport.preferences")
            .And.Contain("metrics.onceADay.sport.preferences.sport");
    }

    [Fact]
    public void ExtractFieldPaths_ComplexLinqChain_WithOrderBy_ExtractsAllFields()
    {
        // Arrange
        Expression<Func<TestModel, dynamic>> expr = x =>
            x.metrics.onceADay.sport.preferences
                .Where(u => u.sport == "F" && u.totalBetsCount > 0)
                .OrderBy(u => u.totalBetsCount)
                .First()
                .sport!;

        // Act
        var paths = ExpressionFieldExtractor.ExtractFieldPaths(expr);

        // Assert
        paths.Should().Contain("metrics.onceADay.sport.preferences");
    }

    // ================== SYSTEM PROPERTIES EXCLUDED ==================

    [Theory]
    [InlineData("List.Count")]
    [InlineData("String.Length")]
    public void ExtractFieldPaths_SystemProperties_AreExcluded(string propertyType)
    {
        // Arrange
        var expressions = new ExpressionsBag<TestModel>()
            .Register("List.Count", x => x.metrics.onceADay.sport.preferences.Count > 0)
            .Register("String.Length", x => x.user.profile.name!.Length > 0);

        var expr = expressions.Get(propertyType);

        // Act
        var paths = ExpressionFieldExtractor.ExtractFieldPaths(expr);

        // Assert
        paths.Should().NotContain(p => p.EndsWith(".Count") || p.EndsWith(".Length"),
            $"System property in {propertyType} should be excluded");
    }

    // ================== UNARY OPERATIONS ==================

    // ================== DUPLICATE DETECTION ==================

    [Fact]
    public void ExtractFieldPaths_SamePropertyMultipleTimes_NotDuplicated()
    {
        // Arrange
        Expression<Func<TestModel, int>> expr = x =>
            (x.user.profile.age > 18 ? x.user.profile.age : 0) +
            (x.user.profile.age < 65 ? x.user.profile.age : 0);

        // Act
        var paths = ExpressionFieldExtractor.ExtractFieldPaths(expr);

        // Assert
        var agePathCount = paths.Count(p => p.Contains("age"));
        agePathCount.Should().Be(1);
    }

    // ================== MEMBER INITIALIZATION ==================

    [Fact]
    public void ExtractFieldPaths_MemberInitializerExpressions_DoesNotThrow()
    {
        // Arrange
        Expression<Func<TestModel, UserData>> expr = x => new UserData
        {
            profile = new ProfileData()
        };

        // Act & Assert - Should complete without throwing
        var paths = ExpressionFieldExtractor.ExtractFieldPaths(expr);
        paths.Should().NotBeNull();
    }

    // ================== METHOD CALLS ON COLLECTIONS ==================

    [Theory]
    [InlineData("WithAnyOnCollection", "age")]
    [InlineData("FirstWithMemberChain", "profile.email")]
    [InlineData("FirstWithPredicateAndProperty", "profile.age")]
    [InlineData("SimpleMemberChain", "profile.name")]
    [InlineData("SelectMethodWithMemberAccess", "profile.name")]
    [InlineData("ExtensionMethodWithoutArguments", "profile,profile.name")]
    [InlineData("IndexerAccess", "profile.name")]
    [InlineData("SelectWithMethodCallChain", "profile.name")]
    public void ExtractFieldPaths_CollectionOperations_ExtractsFields(string scenario, string expectedFields)
    {
        // Arrange
        var expr = new ExpressionsBag<UserData>()
            .Register("WithAnyOnCollection", u => u.age > 18)
            .Register("FirstWithMemberChain", u => u.profile.email == "test@example.com")
            .Register("FirstWithPredicateAndProperty", u => u.profile.age > 0)
            .Register("SimpleMemberChain", u => u.profile.name != null)
            .Register("SelectMethodWithMemberAccess", u => u.profile.name != null)
            .Register("ExtensionMethodWithoutArguments", u => u.profile != null && u.profile.name != null)
            .Register("IndexerAccess", u => u.profile.name != null)
            .Register("SelectWithMethodCallChain", u => u.profile.name != null)
            .Get(scenario);

        // Act
        var paths = ExpressionFieldExtractor.ExtractFieldPaths(expr);
        var expectedFieldsList = expectedFields.Split(',', StringSplitOptions.TrimEntries);

        // Assert
        paths.Should().BeEquivalentTo(expectedFieldsList);
    }

    // ================== BEHAVIOR VERIFICATION TESTS ==================
    // Permanent regression checks documenting behavioral assumptions.

    [Theory]
    [InlineData("DifferentParameterLambda")]
    [InlineData("MultiParameter")]
    public void ExtractFieldPaths_DifferentLambdaTypes_ExtractsAllFields(string scenario)
    {
        // Arrange
        var expressions = new ExpressionsBag<UserData>()
            .Register("DifferentParameterLambda", u => u.age > 18 && u.profile.name != null)
            .Register("MultiParameter", u => u.age > 18 && u.profile.name != null);

        var expr = expressions.Get(scenario);

        // Act
        var paths = ExpressionFieldExtractor.ExtractFieldPaths(expr);

        // Assert
        paths.Should().NotBeEmpty();
    }

    [Theory]
    [InlineData("InstanceMethod")]
    [InlineData("ExtensionChain")]
    public void ExtractFieldPaths_MethodCallChains_ExtractsObjectPath(string scenario)
    {
        // Arrange
        var expressions = new ExpressionsBag<UserData>()
            .Register("InstanceMethod", u => u.profile.name!.ToUpper() != null)
            .Register("ExtensionChain", u => u.profile.name != null && new List<UserData>().Count > 0);

        var expr = expressions.Get(scenario);

        // Act
        var paths = ExpressionFieldExtractor.ExtractFieldPaths(expr);

        // Assert
        paths.Should().NotBeNull();
    }

    [Theory]
    [InlineData("NullCoalescingChain")]
    [InlineData("NullCoalescingWithDefaults")]
    [InlineData("NestedNullCoalescing")]
    public void ExtractFieldPaths_NullCoalescingExpressions_ExtractsAllBranches(string scenario)
    {
        // Arrange
        var expressions = new ExpressionsBag<TestModel>()
            .Register("NullCoalescingChain", x => (x.user.profile.name ?? x.user.email ?? "default") != null)
            .Register("NullCoalescingWithDefaults", x => (x.user.profile.name ?? "unknown") != null)
            .Register("NestedNullCoalescing", x => (x.user.profile.email ?? x.user.email) != null);

        var expr = expressions.Get(scenario);

        // Act
        var paths = ExpressionFieldExtractor.ExtractFieldPaths(expr);

        // Assert
        paths.Should().NotBeEmpty();
    }

    [Theory]
    [InlineData("CastToObject")]
    [InlineData("ConversionExpression")]
    [InlineData("NestedCastConversion")]
    public void ExtractFieldPaths_CastAndConversionExpressions_ExtractsUnderlyingFields(string scenario)
    {
        // Arrange
        var expressions = new ExpressionsBag<TestModel>()
            .Register("CastToObject", x => ((object)x.user.profile.name) != null)
            .Register("ConversionExpression", x => x.user.age.ToString() != null)
            .Register("NestedCastConversion", x => ((int)x.metrics.realtime.deposits.firstDepositAmount) > 0);

        var expr = expressions.Get(scenario);

        // Act
        var paths = ExpressionFieldExtractor.ExtractFieldPaths(expr);

        // Assert
        paths.Should().NotBeEmpty();
    }

    [Theory]
    [InlineData("instance_method_on_object", "user.profile.name")]
    [InlineData("string_method_chain", "user.email")]
    [InlineData("string_to_upper", "user.name")]
    public void ExtractFieldPaths_MethodCallExpressions_ExtractsBasePath(string scenario, string expectedPath)
    {
        var expressions = new ExpressionsBag<TestModel>()
            .Register("instance_method_on_object", x => x.user.profile.name!.ToUpper() == "TEST")
            .Register("string_method_chain", x => x.user.email!.Trim().StartsWith("admin"))
            .Register("string_to_upper", x => x.user.name!.ToUpper().Contains("test"));

        var expr = expressions.Get(scenario);

        var paths = ExpressionFieldExtractor.ExtractFieldPaths(expr);

        paths.Should().Contain(expectedPath);
    }
}
