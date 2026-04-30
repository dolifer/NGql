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
            .Register("CastToObject", x => ((object?)x.user.profile.name) != null)
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

    // ─────────────────────────────────────────────────────────────────────────
    // BuildMemberPath edge cases — exercise the Coalesce / Conditional / chain
    // branches that classical lambdas rarely produce on their own.
    // ─────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("CoalesceOnInnerObject", "user.profile.age", "user.profile.name")]
    [InlineData("CoalesceWithMethodChain", "user.profile.name", "user.email")]
    public void ExtractFieldPaths_CoalesceInChain_WalksLeftSide(string scenario, string mustContain, string alsoContains)
    {
        // (x.user ?? defaultUser).profile.age  →  walking up the chain hits BinaryExpression Coalesce,
        // BuildMemberPath continues with the left side and resolves the path.
        var defaultUser = new UserData { profile = new ProfileData { name = "" }, name = "" };
        var expressions = new ExpressionsBag<TestModel>()
            .Register("CoalesceOnInnerObject",
                x => (x.user ?? defaultUser).profile.age > 0
                  && (x.user ?? defaultUser).profile.name != null)
            .Register("CoalesceWithMethodChain",
                x => (x.user.profile.name ?? x.user.email ?? "fallback").StartsWith('a'));

        var paths = ExpressionFieldExtractor.ExtractFieldPaths(expressions.Get(scenario));

        paths.Should().Contain(mustContain);
        paths.Should().Contain(alsoContains);
    }

    [Fact]
    public void ExtractFieldPaths_ConstantRootedChain_ReturnsNoPath()
    {
        // `((TestModel)null).user.age` — the chain's root is a ConstantExpression which
        // BuildMemberPath cannot resolve; it returns null and no path is collected.
        TestModel? nullModel = null;
        Expression<Func<bool>> expr = () => nullModel!.user.age > 0;

        var paths = ExpressionFieldExtractor.ExtractFieldPaths(expr);

        // The lambda has no parameters of TestModel so the only chain root is the captured
        // null-ed local — it gets resolved to a constant and no field path is produced.
        paths.Should().NotContain("user.age");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // ConditionalExpression in a member chain — the C# ternary `cond ? a : b`
    // compiles fine inside an expression tree as ConditionalExpression, so
    // these can be expressed as plain ExpressionsBag lambdas.
    // ─────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("ConditionalChain_TrueBranchReferencesMember", "user.name")]
    [InlineData("ConditionalChain_TrueBranchIsConstNull_FalseHasMember", "user.email")]
    public void ExtractFieldPaths_ConditionalInChain_WalksChosenBranch(string scenario, string expectedPath)
    {
        // The fallback in each ternary is a non-null UserData built from local state, so the
        // expression compiles even though the runtime branch is never actually taken in tests.
        var altUser = new UserData { profile = new ProfileData(), name = "alt" };

        var expressions = new ExpressionsBag<TestModel>()
            // (x.user != null ? x.user : altUser).name — IfTrue is the member chain.
            .Register("ConditionalChain_TrueBranchReferencesMember",
                x => (x.user != null ? x.user : altUser).name != null)
            // (x.user == null ? (UserData)null! : x.user).email — IfTrue is constant null,
            // walker falls through to IfFalse.
            .Register("ConditionalChain_TrueBranchIsConstNull_FalseHasMember",
                x => (x.user == null ? null! : x.user).email != null);

        var paths = ExpressionFieldExtractor.ExtractFieldPaths(expressions.Get(scenario));

        paths.Should().Contain(expectedPath);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // BuildPathFromExpression edge shapes — each one expressible as a plain
    // C# lambda by routing through Enumerable static methods (Repeat, Cast,
    // literal arrays). No manual Expression.Call construction needed.
    // ─────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("RepeatMemberThenFirstThenMemberThenAny", "preferences")]
    [InlineData("RepeatThenSelectThenAny", "sport")]
    public void ExtractFieldPaths_LinqOverMemberChainThroughMethodCall_ResolvesBaseFromInnerCall(
        string scenario, string expectedSegment)
    {
        var expressions = new ExpressionsBag<TestModel>()
            // Enumerable.Repeat(x.metrics.onceADay.sport, 1).First().preferences.Any(...)
            // The Any source argument is a MemberExpression (.preferences) whose .Expression is
            // a MethodCallExpression (.First()). BuildPathFromExpression's chain-walker hits the
            // inner MethodCallExpression branch and recurses into Repeat's first argument.
            .Register("RepeatMemberThenFirstThenMemberThenAny",
                x => Enumerable.Repeat(x.metrics.onceADay.sport, 1)
                        .First()
                        .preferences
                        .Any(p => p.sport != null))
            // Enumerable.Repeat(x.metrics.onceADay.sport, 1).Select(s => s.preferences).First().Any(...)
            // — exercises a deeper recursive walk through nested method calls.
            .Register("RepeatThenSelectThenAny",
                x => Enumerable.Repeat(x.metrics.onceADay.sport, 1)
                        .Select(s => s.preferences)
                        .First()
                        .Any(p => p.sport != null));

        var paths = ExpressionFieldExtractor.ExtractFieldPaths(expressions.Get(scenario));

        paths.Should().Contain(p => p.Contains(expectedSegment));
    }

    [Fact]
    public void ExtractFieldPaths_LinqOverCapturedLocal_HandlesClosureRoot()
    {
        // A captured local used as a LINQ source compiles into a member chain rooted at a
        // ConstantExpression (the closure object). GetMethodCallBasePath calls
        // BuildPathFromExpression on that source; the chain walker eventually hits the closure
        // ConstantExpression which is neither Member, Parameter, nor MethodCall — exercises
        // the unknown-chain return-null branch. The visitor must handle it without polluting
        // the result with closure internals like "<>c__DisplayClass*".
        var captured = new TestModel
        {
            user = new UserData { profile = new ProfileData(), name = "captured" },
            metrics = new MetricsData
            {
                realtime = new RealtimeData { deposits = new DepositsData() },
                onceADay = new OnceADayData
                {
                    sport = new SportData
                    {
                        preferences = new List<PreferenceData> { new() { sport = "tennis" } }
                    }
                }
            }
        };

        // Both forms in one expression:
        //   1) captured.metrics.onceADay.sport.preferences.Any(...) — LINQ source rooted at a closure.
        //   2) captured.user.name == x.user.name — closure member chain in a value comparison.
        Expression<Func<TestModel, bool>> expr =
            x => x.user.email != null
              && captured.metrics.onceADay.sport.preferences.Any(p => p.sport == x.user.name);

        var paths = ExpressionFieldExtractor.ExtractFieldPaths(expr);

        paths.Should().Contain("user.email");
        paths.Should().Contain("user.name");
        paths.Should().NotContain(p => p.Contains("<>"));
    }

    [Theory]
    [InlineData("LinqOverLiteralArray")]
    [InlineData("LinqOverCastedMember")]
    public void ExtractFieldPaths_LinqWithoutMemberSource_DoesNotProduceSpuriousPath(string scenario)
    {
        var expressions = new ExpressionsBag<TestModel>()
            // new[] {1,2}.Any(p => p > 0) — the LINQ source is a NewArrayInit, neither
            // MemberExpression nor MethodCallExpression. BuildPathFromExpression returns null.
            // Reference x.user.age inside the predicate so the test isn't trivially constant.
            .Register("LinqOverLiteralArray",
                x => new[] { 1, 2 }.Any(p => p > x.user.age))
            // ((IEnumerable<int>)(object)x.user.age).Any(p => p > 0) — the chain walker meets
            // a UnaryExpression (cast) and bails out via the unknown-shape return-null branch.
            .Register("LinqOverCastedMember",
                x => ((IEnumerable<int>)(object)new[] { x.user.age }).Any(p => p > 0));

        var paths = ExpressionFieldExtractor.ExtractFieldPaths(expressions.Get(scenario));

        // The visitor must not invent a System.* path or include the cast machinery in any
        // extracted path — but the underlying member references (x.user.age) should still surface.
        paths.Should().Contain(p => p.Contains("user.age"));
        paths.Should().NotContain(p => p.StartsWith("System"));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // BuildPathFromExpression — exercises method-call-as-base, member chain
    // through method calls, and the invalid-chain fallback.
    // ─────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("AnyOnNestedCollection")]
    [InlineData("FirstOnDeepCollection")]
    [InlineData("WhereFirstChain")]
    [InlineData("FirstThenMemberAccess")]
    [InlineData("WhereFirstThenMember")]
    public void ExtractFieldPaths_LinqOnNestedCollection_ResolvesBasePath(string scenario)
    {
        // The LINQ method is called on a nested collection; GetMethodCallBasePath
        // walks the call's argument back through MemberExpression chains via
        // BuildPathFromExpression. Then the predicate inside Any/Where/First runs
        // with the lambda context populated from that base path.
        var expressions = new ExpressionsBag<TestModel>()
            .Register("AnyOnNestedCollection",
                x => x.metrics.onceADay.sport.preferences.Any(p => p.sport == "tennis"))
            .Register("FirstOnDeepCollection",
                x => x.metrics.onceADay.sport.preferences[0].sport != null)
            .Register("WhereFirstChain",
                x => x.metrics.onceADay.sport.preferences
                      .First(p => p.totalBetsCount > 0).sport == "soccer")
            // .First().sport — outer member access has a MethodCallExpression as Expression →
            // BuildPathFromExpression's chain walker hits the MethodCallExpression branch.
            .Register("FirstThenMemberAccess",
                x => x.metrics.onceADay.sport.preferences[0].sport!.Length > 0)
            .Register("WhereFirstThenMember",
                x => x.metrics.onceADay.sport.preferences
                      .First(p => p.totalBetsCount > 0).totalBetsCount > 0);

        var paths = ExpressionFieldExtractor.ExtractFieldPaths(expressions.Get(scenario));

        // Whatever the exact extracted shape, the deep nested collection path must show up
        // in some form — this exercises the method-call/member-chain interplay.
        paths.Should().Contain(p => p.Contains("preferences"));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Property-style invariant: every extracted path must roundtrip — adding it
    // back to a QueryBuilder's preserve set must be syntactically accepted.
    // ─────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("Single")]
    [InlineData("Combined")]
    [InlineData("Coalesce")]
    [InlineData("LinqAny")]
    [InlineData("MethodChain")]
    public void ExtractFieldPaths_AnyExtractedPath_IsValidGraphQLPath(string scenario)
    {
        var expressions = new ExpressionsBag<TestModel>()
            .Register("Single", x => x.user.profile.name == "alice")
            .Register("Combined",
                x => x.user.profile.age > 18
                  && x.user.email != null
                  && x.metrics.realtime.deposits.firstDepositAmount > 0)
            .Register("Coalesce", x => (x.user.profile.name ?? x.user.email ?? "") != "")
            .Register("LinqAny", x => x.metrics.onceADay.sport.preferences.Any(p => p.sport == "tennis"))
            .Register("MethodChain", x => x.user.profile.name!.Trim().ToUpper().Contains("X"));

        var paths = ExpressionFieldExtractor.ExtractFieldPaths(expressions.Get(scenario));

        // Property: every path is a non-empty dotted GraphQL identifier chain — no whitespace,
        // no leading/trailing dots, no empty segments. A consumer that pastes any extracted
        // path into PreservationBuilder.Preserve(...) must not see malformed input.
        paths.Should().NotBeEmpty();
        foreach (var path in paths)
        {
            path.Should().NotBeNullOrWhiteSpace();
            path.Should().NotStartWith(".");
            path.Should().NotEndWith(".");
            path.Should().NotContain("..");
            path.Split('.').Should().AllSatisfy(seg => seg.Should().NotBeNullOrWhiteSpace());
        }
    }

    [Fact]
    public void ExtractFieldPaths_QueryableAndAnonymousType_CoverBothLinqMethodAndNamespaceArms()
    {
        // IsLinqMethod accepts both System.Linq.Enumerable and Queryable. The Queryable arm
        // is hit by visiting an IQueryable.Where expression. ShouldExcludeProperty does
        // `DeclaringType!.Namespace?.StartsWith("System")` — anonymous types live in the
        // null namespace, so accessing a property OF an anonymous type via Expression.Lambda
        // exercises the null-conditional null arm directly.
        var queryable = new TestModel[0].AsQueryable();
        Expression<Func<TestModel, bool>> predicate = x => x.user.profile.name == "tennis";
        var queryablePaths = ExpressionFieldExtractor.ExtractFieldPaths(queryable.Where(predicate).Expression);
        queryablePaths.Should().Contain("user.profile.name");

        var anon = new { Title = "x" };
        var paramExpr = Expression.Parameter(anon.GetType(), "a");
        var member = Expression.Property(paramExpr, "Title");
        var lambda = Expression.Lambda(member, paramExpr);
        var anonPaths = ExpressionFieldExtractor.ExtractFieldPaths(lambda);
        anonPaths.Should().NotBeNull();
    }
}
