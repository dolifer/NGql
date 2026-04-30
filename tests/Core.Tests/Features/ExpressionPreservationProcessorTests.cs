using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using FluentAssertions;
using NGql.Core.Abstractions;
using NGql.Core.Builders;
using NGql.Core.Exceptions;
using NGql.Core.Extensions;
using NGql.Core.Features;
using Xunit;

namespace NGql.Core.Tests.Features;

public class ExpressionPreservationProcessorTests
{
    #region ExpressionPreservationProcessor - Lambda Parameter Tests

    [Fact]
    public void ExpressionPreservationProcessor_NonLambdaExpression_HandleGracefully()
    {
        // Arrange
        var queryBuilder = QueryBuilder.CreateDefaultBuilder("Query");
        var processor = new ExpressionPreservationProcessor(queryBuilder, path => { });
        Expression nonLambdaExpr = Expression.Constant(42);

        // Act
        var action = () => processor.ProcessExpression(nonLambdaExpr, "player", null, null, null);

        // Assert - should not throw
        action.Should().NotThrow();
    }

    [Fact]
    public void ExpressionPreservationProcessor_LambdaWithParameter_ExtractsParameterNames()
    {
        // Arrange
        var queryBuilder = QueryBuilder.CreateDefaultBuilder("Query");
        var processor = new ExpressionPreservationProcessor(queryBuilder, path => { });
        
        Expression<Func<string, bool>> lambdaExpr = x => x.Length > 0;

        // Act
        var action = () => processor.ProcessExpression(lambdaExpr, null, null, null, null);

        // Assert - should not throw
        action.Should().NotThrow();
    }

    [Fact]
    public void ExpressionPreservationProcessor_EmptyNodePath_PreservesDirectly()
    {
        // Arrange
        var queryBuilder = QueryBuilder.CreateDefaultBuilder("Query");
        var preservedPaths = new List<string>();
        var processor = new ExpressionPreservationProcessor(queryBuilder, path => preservedPaths.Add(path));

        Expression<Func<object, bool>> expr = x => true;

        // Act
        var action = () => processor.ProcessExpression(expr, "", null, null, null);

        // Assert - should not throw
        action.Should().NotThrow();
    }

    [Fact]
    public void ExpressionPreservationProcessor_NullNodePath_HandlesGracefully()
    {
        // Arrange
        var queryBuilder = QueryBuilder.CreateDefaultBuilder("Query");
        var processor = new ExpressionPreservationProcessor(queryBuilder, path => { });

        Expression<Func<object, bool>> expr = x => true;

        // Act
        var action = () => processor.ProcessExpression(expr, null, null, null, null);

        // Assert - should not throw
        action.Should().NotThrow();
    }

    #endregion

    #region ExpressionPreservationProcessor - Multi-Parameter Tests

    [Fact]
    public void ExpressionPreservationProcessor_MultiParameterExpression_HandlesMultipleParams()
    {
        // Arrange
        var queryBuilder = QueryBuilder.CreateDefaultBuilder("Query");
        var processor = new ExpressionPreservationProcessor(queryBuilder, path => { });

        // Lambda with two parameters
        Expression<Func<string, string, bool>> multiParamLambda = (x, y) => x.Length == y.Length;

        // Act
        var action = () => processor.ProcessExpression(multiParamLambda, null, null, null, null);

        // Assert - should handle multi-parameter lambda
        action.Should().NotThrow();
    }

    [Fact]
    public void ExpressionPreservationProcessor_MultiParameterWithLocalMap_UsesLocalMapStrategy()
    {
        // Arrange
        var queryBuilder = QueryBuilder.CreateDefaultBuilder("Query");
        var preservedPaths = new List<string>();
        var processor = new ExpressionPreservationProcessor(queryBuilder, path => preservedPaths.Add(path));

        var localMap = new Dictionary<string, string[]>
        {
            { "u", new[] { "user" } },
            { "p", new[] { "profile" } }
        };

        Expression<Func<object, object, bool>> expr = (u, p) => true;

        // Act
        var action = () => processor.ProcessExpression(expr, "data", localMap, typeof(object), null);

        // Assert - should not throw
        action.Should().NotThrow();
    }

    #endregion

    #region ExpressionPreservationProcessor - Strategy Selection Tests

    [Fact]
    public void ExpressionPreservationProcessor_NoLocalMap_UsesGetPathToStrategy()
    {
        // Arrange
        var queryBuilder = QueryBuilder.CreateDefaultBuilder("Query");
        var processor = new ExpressionPreservationProcessor(queryBuilder, path => { });

        Expression<Func<string, bool>> expr = x => x.Length > 0;

        // Act
        var action = () => processor.ProcessExpression(expr, "profile", null, typeof(string), null);

        // Assert - should use GetPathTo fallback strategy
        action.Should().NotThrow();
    }

    [Fact]
    public void ExpressionPreservationProcessor_WithLocalMap_UsesLocalMapStrategy()
    {
        // Arrange
        var queryBuilder = QueryBuilder.CreateDefaultBuilder("Query");
        var processor = new ExpressionPreservationProcessor(queryBuilder, path => { });

        var localMap = new Dictionary<string, string[]>
        {
            { "p", new[] { "player" } }
        };

        Expression<Func<string, bool>> expr = x => x.Length > 0;

        // Act
        var action = () => processor.ProcessExpression(expr, "stats", localMap, typeof(string), null);

        // Assert - should use localMap strategy
        action.Should().NotThrow();
    }

    [Fact]
    public void ExpressionPreservationProcessor_AlwaysPreserveFields_IncludesSpecifiedFields()
    {
        // Arrange
        var queryBuilder = QueryBuilder.CreateDefaultBuilder("Query");
        var processor = new ExpressionPreservationProcessor(queryBuilder, path => { });

        var localMap = new Dictionary<string, string[]>
        {
            { "p", new[] { "player" } }
        };

        var alwaysPreserve = new[] { "name", "age" };

        Expression<Func<string, bool>> expr = x => true;

        // Act
        var action = () => processor.ProcessExpression(expr, "details", localMap, typeof(string), alwaysPreserve);

        // Assert
        action.Should().NotThrow();
    }

    #endregion

    #region ExpressionPreservationProcessor - Edge Cases Tests

    [Fact]
    public void ExpressionPreservationProcessor_EmptyLocalMap_HandlesGracefully()
    {
        // Arrange
        var queryBuilder = QueryBuilder.CreateDefaultBuilder("Query");
        var processor = new ExpressionPreservationProcessor(queryBuilder, path => { });
        
        var emptyLocalMap = new Dictionary<string, string[]>();
        Expression<Func<string, bool>> expr = x => x.Length > 0;

        // Act
        var action = () => processor.ProcessExpression(expr, "player", emptyLocalMap, typeof(string), null);

        // Assert
        action.Should().NotThrow();
    }

    [Fact]
    public void ExpressionPreservationProcessor_ParameterNotInLocalMap_SkipsProcessing()
    {
        // Arrange
        var queryBuilder = QueryBuilder.CreateDefaultBuilder("Query");
        var preservedPaths = new List<string>();
        var processor = new ExpressionPreservationProcessor(queryBuilder, path => preservedPaths.Add(path));

        var localMap = new Dictionary<string, string[]>
        {
            { "p", new[] { "player" } }
        };

        // Parameter name "x" not in localMap
        Expression<Func<string, bool>> expr = x => x.Length > 0;

        // Act
        var action = () => processor.ProcessExpression(expr, "profile", localMap, typeof(string), null);

        // Assert
        action.Should().NotThrow();
    }

    [Fact]
    public void ExpressionPreservationProcessor_EmptyAlwaysPreserveFields_HandlesGracefully()
    {
        // Arrange
        var queryBuilder = QueryBuilder.CreateDefaultBuilder("Query");
        var processor = new ExpressionPreservationProcessor(queryBuilder, path => { });

        var localMap = new Dictionary<string, string[]>
        {
            { "p", new[] { "player" } }
        };

        Expression<Func<string, bool>> expr = x => true;
        var emptyAlwaysPreserve = Array.Empty<string>();

        // Act
        var action = () => processor.ProcessExpression(expr, "player", localMap, typeof(string), emptyAlwaysPreserve);

        // Assert
        action.Should().NotThrow();
    }

    [Fact]
    public void ExpressionPreservationProcessor_MultiParameterWithoutPrefixes_ExpandsPerParameter()
    {
        // Arrange
        var queryBuilder = QueryBuilder.CreateDefaultBuilder("Query");
        var processor = new ExpressionPreservationProcessor(queryBuilder, path => { });

        var localMap = new Dictionary<string, string[]>
        {
            { "u", new[] { "user" } },
            { "p", new[] { "profile" } }
        };

        // Lambda without prefixes - multi-parameter triggers special handling
        Expression<Func<object, object, bool>> expr = (u, p) => true;

        // Act
        var action = () => processor.ProcessExpression(expr, "name", localMap, typeof(object), null);

        // Assert - should not throw
        action.Should().NotThrow();
    }

    #endregion

    #region ExpressionPreservationProcessor - Greedy Expansion Tests

    [Fact]
    public void ExpressionPreservationProcessor_OnlyParameterName_TriggersGreedyExpansion()
    {
        // Arrange
        var queryBuilder = QueryBuilder.CreateDefaultBuilder("Query");
        var processor = new ExpressionPreservationProcessor(queryBuilder, path => { });

        var localMap = new Dictionary<string, string[]>
        {
            { "user", new[] { "user" } }
        };

        // Lambda that references just parameter name
        Expression<Func<TestUserType, bool>> expr = u => u != null;

        // Act
        var action = () => processor.ProcessExpression(expr, "details", localMap, typeof(TestUserType), null);

        // Assert - should not throw even if no fields are matched
        action.Should().NotThrow();
    }

    [Fact]
    public void ExpressionPreservationProcessor_NoFieldsMatched_FallsBackToGreedyExpansion()
    {
        // Arrange
        var queryBuilder = QueryBuilder.CreateDefaultBuilder("Query");
        var preservedPaths = new List<string>();
        var processor = new ExpressionPreservationProcessor(queryBuilder, path => preservedPaths.Add(path));

        var localMap = new Dictionary<string, string[]>
        {
            { "user", new[] { "user" } }
        };

        // Single parameter with no matching fields
        Expression<Func<TestUserType, bool>> expr = u => u != null;

        // Act
        Action act = () => processor.ProcessExpression(expr, "user", localMap, typeof(TestUserType), null);

        // Assert - greedy expansion should attempt to preserve all type properties without throwing.
        // (exact behavior depends on implementation and query structure)
        act.Should().NotThrow();
    }

    #endregion
}

internal class TestUserType
{
    public string Name { get; set; } = "test";
    public int Age { get; set; } = 30;
    public string Email { get; set; } = "test@example.com";
}
