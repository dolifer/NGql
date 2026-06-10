using System;
using System.Collections.Generic;
using FluentAssertions;
using Xunit;

namespace NGql.Core.Tests.Issues;

/// <summary>
/// Regression: argument sorting treats keys case-insensitively. Two keys (or decomposed
/// object properties) that collide under OrdinalIgnoreCase must fail fast with
/// <see cref="ArgumentException"/> — silently keeping the last-enumerated value would send
/// the server different data than the caller specified, nondeterministically for reflected
/// properties.
/// </summary>
public class ArgumentKeyCollisionTests
{
    [Fact]
    public void Where_DuplicateCaseInsensitiveKeys_Throws()
    {
        // Arrange
        var query = new Query("q").Select("id");
        var conflicting = new Dictionary<string, object?> { ["id"] = 1, ["ID"] = 2 };

        // Act
        var act = () => query.Where("filter", conflicting);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Where_DecomposedObjectWithCaseCollidingProperties_Throws()
    {
        // Arrange
        var query = new Query("q").Select("id");

        // Act
        var act = () => query.Where("filter", new CollidingArgs());

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Where_DistinctKeys_StillSortsAndRenders()
    {
        // Arrange
        var query = new Query("q").Select("id");

        // Act
        query.Where("filter", new Dictionary<string, object?> { ["b"] = 2, ["a"] = 1 });

        // Assert
        query.ToString().Should().Contain("filter:{a:1, b:2}");
    }

    private sealed class CollidingArgs
    {
        public int Value { get; set; } = 1;
        public int VALUE { get; set; } = 2;
    }
}
