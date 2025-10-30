using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using FluentAssertions;
using NGql.Core.Builders;
using Xunit;

namespace NGql.Core.Tests.Builders;

public class ExpressionFieldExtractorTests
{
    // Test model classes
    private class TestModel
    {
        public UserData user { get; set; } = null!;
        public MetricsData metrics { get; set; } = null!;
    }

    private class UserData
    {
        public ProfileData profile { get; set; } = null!;
        public SettingsData settings { get; set; } = null!;
        public int age { get; set; }
        public string? email { get; set; }
        public bool isActive { get; set; }
    }

    private class ProfileData
    {
        public string? name { get; set; }
        public string? email { get; set; }
        public string? phone { get; set; }
        public int age { get; set; }
    }

    private class SettingsData
    {
        public string? theme { get; set; }
        public string? language { get; set; }
    }

    private class MetricsData
    {
        public RealtimeData realtime { get; set; } = null!;
        public OnceADayData onceADay { get; set; } = null!;
    }

    private class RealtimeData
    {
        public DepositsData deposits { get; set; } = null!;
    }

    private class DepositsData
    {
        public decimal firstDepositAmount { get; set; }
        public DateTime? firstDepositDate { get; set; }
        public string? firstDepositPaymentSystem { get; set; }
    }

    private class OnceADayData
    {
        public SportData sport { get; set; } = null!;
    }

    private class SportData
    {
        public List<PreferenceData> preferences { get; set; } = new();
    }

    private class PreferenceData
    {
        public string? sport { get; set; }
        public int totalBetsCount { get; set; }
    }

    [Fact]
    public void ExtractFieldPaths_SimplePropertyAccess_SingleLevel()
    {
        // Arrange
        Expression<Func<TestModel, bool>> expr = x => x.user.age > 18;

        // Act
        var paths = ExpressionFieldExtractor.ExtractFieldPaths(expr);

        // Assert
        paths.Should().ContainSingle()
            .Which.Should().Be("user.age");
    }

    [Fact]
    public void ExtractFieldPaths_NestedPropertyChain()
    {
        // Arrange
        Expression<Func<TestModel, bool>> expr = x => x.user.profile.age > 10;

        // Act
        var paths = ExpressionFieldExtractor.ExtractFieldPaths(expr);

        // Assert
        paths.Should().ContainSingle()
            .Which.Should().Be("user.profile.age");
    }

    [Fact]
    public void ExtractFieldPaths_DeepNestedProperty()
    {
        // Arrange
        Expression<Func<TestModel, bool>> expr = x => x.metrics.realtime.deposits.firstDepositAmount > 100;

        // Act
        var paths = ExpressionFieldExtractor.ExtractFieldPaths(expr);

        // Assert
        paths.Should().ContainSingle()
            .Which.Should().Be("metrics.realtime.deposits.firstDepositAmount");
    }

    [Fact]
    public void ExtractFieldPaths_NullCheck()
    {
        // Arrange
        Expression<Func<TestModel, bool>> expr = x => x.user.profile.email != null;

        // Act
        var paths = ExpressionFieldExtractor.ExtractFieldPaths(expr);

        // Assert
        paths.Should().ContainSingle()
            .Which.Should().Be("user.profile.email");
    }

    [Fact]
    public void ExtractFieldPaths_BooleanProperty()
    {
        // Arrange
        Expression<Func<TestModel, bool>> expr = x => x.user.isActive;

        // Act
        var paths = ExpressionFieldExtractor.ExtractFieldPaths(expr);

        // Assert
        paths.Should().ContainSingle()
            .Which.Should().Be("user.isActive");
    }

    [Fact]
    public void ExtractFieldPaths_BooleanNegation()
    {
        // Arrange
        Expression<Func<TestModel, bool>> expr = x => !x.user.isActive;

        // Act
        var paths = ExpressionFieldExtractor.ExtractFieldPaths(expr);

        // Assert
        paths.Should().ContainSingle()
            .Which.Should().Be("user.isActive");
    }

    [Fact]
    public void ExtractFieldPaths_AndCondition_MultipleFields()
    {
        // Arrange
        Expression<Func<TestModel, bool>> expr = x => x.user.age > 18 && x.user.email != null;

        // Act
        var paths = ExpressionFieldExtractor.ExtractFieldPaths(expr);

        // Assert
        paths.Should().HaveCount(2)
            .And.Contain("user.age")
            .And.Contain("user.email");
    }

    [Fact]
    public void ExtractFieldPaths_OrCondition_MultipleFields()
    {
        // Arrange
        Expression<Func<TestModel, bool>> expr = x => x.user.age < 18 || x.user.profile.age < 18;

        // Act
        var paths = ExpressionFieldExtractor.ExtractFieldPaths(expr);

        // Assert
        paths.Should().HaveCount(2)
            .And.Contain("user.age")
            .And.Contain("user.profile.age");
    }

    [Fact]
    public void ExtractFieldPaths_ComplexBooleanLogic()
    {
        // Arrange
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

    [Fact]
    public void ExtractFieldPaths_TernaryOperator()
    {
        // Arrange
        Expression<Func<TestModel, bool>> expr = x =>
            x.user.isActive ? x.user.age > 18 : x.user.profile.age > 21;

        // Act
        var paths = ExpressionFieldExtractor.ExtractFieldPaths(expr);

        // Assert
        paths.Should().HaveCount(3)
            .And.Contain("user.isActive")
            .And.Contain("user.age")
            .And.Contain("user.profile.age");
    }

    [Fact]
    public void ExtractFieldPaths_WithLinqFirst()
    {
        // Arrange
        Expression<Func<TestModel, bool>> expr = x =>
            x.metrics.onceADay.sport.preferences.First().totalBetsCount > 0;

        // Act
        var paths = ExpressionFieldExtractor.ExtractFieldPaths(expr);

        // Assert
        paths.Should().Contain("metrics.onceADay.sport.preferences.totalBetsCount");
    }

    [Fact]
    public void ExtractFieldPaths_WithLinqFirstAndPredicate()
    {
        // Arrange
        Expression<Func<TestModel, bool>> expr = x =>
            x.metrics.onceADay.sport.preferences.First(p => p.sport == "F").totalBetsCount > 0;

        // Act
        var paths = ExpressionFieldExtractor.ExtractFieldPaths(expr);

        // Assert
        paths.Should().Contain("metrics.onceADay.sport.preferences.totalBetsCount");
        paths.Should().Contain("metrics.onceADay.sport.preferences.sport"); // CRITICAL: Needed for lambda filtering to work
    }

    [Fact]
    public void ExtractFieldPaths_WithLinqAny()
    {
        // Arrange
        Expression<Func<TestModel, bool>> expr = x =>
            x.metrics.onceADay.sport.preferences.Any(p => p.sport == "F");

        // Act
        var paths = ExpressionFieldExtractor.ExtractFieldPaths(expr);

        // Assert
        paths.Should().Contain("metrics.onceADay.sport.preferences");
        paths.Should().Contain("metrics.onceADay.sport.preferences.sport"); // CRITICAL: Needed for lambda filtering
    }

    [Fact]
    public void ExtractFieldPaths_WithLinqWhere()
    {
        // Arrange
        Expression<Func<TestModel, bool>> expr = x =>
            x.metrics.onceADay.sport.preferences.Where(p => p.sport == "F").Any();

        // Act
        var paths = ExpressionFieldExtractor.ExtractFieldPaths(expr);

        // Assert
        paths.Should().Contain("metrics.onceADay.sport.preferences");
        paths.Should().Contain("metrics.onceADay.sport.preferences.sport"); // CRITICAL: Needed for lambda filtering
    }

    [Fact]
    public void ExtractFieldPaths_AnonymousTypeSelector()
    {
        // Arrange
        Expression<Func<TestModel, object>> expr = x => new
        {
            x.user.profile.name,
            x.user.profile.email
        };

        // Act
        var paths = ExpressionFieldExtractor.ExtractFieldPaths(expr);

        // Assert
        paths.Should().HaveCount(2)
            .And.Contain("user.profile.name")
            .And.Contain("user.profile.email");
    }

    [Fact]
    public void ExtractFieldPaths_AnonymousTypeWithMultipleLevels()
    {
        // Arrange
        Expression<Func<TestModel, object>> expr = x => new
        {
            x.user.age,
            x.user.profile.name,
            x.metrics.realtime.deposits.firstDepositAmount
        };

        // Act
        var paths = ExpressionFieldExtractor.ExtractFieldPaths(expr);

        // Assert
        paths.Should().HaveCount(3)
            .And.Contain("user.age")
            .And.Contain("user.profile.name")
            .And.Contain("metrics.realtime.deposits.firstDepositAmount");
    }

    [Fact]
    public void ExtractFieldPaths_CaseInsensitive()
    {
        // Arrange
        Expression<Func<TestModel, bool>> expr = x => x.user.profile.name != null;

        // Act
        var paths = ExpressionFieldExtractor.ExtractFieldPaths(expr);

        // Assert - HashSet should be case-insensitive
        paths.Should().ContainSingle();
        paths.Should().Contain("user.profile.name");
        paths.Should().Contain("USER.PROFILE.NAME"); // Case-insensitive
    }

    [Fact]
    public void ExtractFieldPaths_EmptyExpression_ReturnsEmpty()
    {
        // Arrange
        Expression<Func<TestModel, bool>> expr = x => true;

        // Act
        var paths = ExpressionFieldExtractor.ExtractFieldPaths(expr);

        // Assert
        paths.Should().BeEmpty();
    }

    [Fact]
    public void ExtractFieldPaths_OnlyConstants_ReturnsEmpty()
    {
        // Arrange
        Expression<Func<TestModel, bool>> expr = x => 10 > 5;

        // Act
        var paths = ExpressionFieldExtractor.ExtractFieldPaths(expr);

        // Assert
        paths.Should().BeEmpty();
    }

    [Fact]
    public void ExtractFieldPaths_StringContains()
    {
        // Arrange
        Expression<Func<TestModel, bool>> expr = x => x.user.profile.name!.Contains("test");

        // Act
        var paths = ExpressionFieldExtractor.ExtractFieldPaths(expr);

        // Assert
        paths.Should().ContainSingle()
            .Which.Should().Be("user.profile.name");
    }

    [Fact]
    public void ExtractFieldPaths_StringStartsWith()
    {
        // Arrange
        Expression<Func<TestModel, bool>> expr = x => x.user.email!.StartsWith("admin");

        // Act
        var paths = ExpressionFieldExtractor.ExtractFieldPaths(expr);

        // Assert
        paths.Should().ContainSingle()
            .Which.Should().Be("user.email");
    }

    [Fact]
    public void ExtractFieldPaths_MultipleMethodCalls()
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
}
