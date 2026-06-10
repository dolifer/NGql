using System;
using System.Collections.Generic;
using FluentAssertions;
using NGql.Core.Builders;
using Xunit;

namespace NGql.Core.Tests.Issues;

/// <summary>
/// Regression: <c>AddField(field, arguments, string[] subFields, …)</c> declares subFields
/// non-nullable; passing null used to throw <see cref="ArgumentNullException"/> eagerly (via
/// the LINQ Select source check). A perf refactor briefly made the call silently add the field
/// as a bare leaf instead of surfacing the caller's contract violation.
/// </summary>
public class AddFieldNullSubFieldsTests
{
    [Fact]
    public void AddField_ArgsWithNullStringSubFields_Throws()
    {
        // Arrange
        var builder = QueryBuilder.CreateDefaultBuilder("Q");
        var args = new Dictionary<string, object?> { ["id"] = 1 };

        // Act
        var act = () => builder.AddField("user", args, (string[])null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddField_NullableStringSubFieldsOverload_StillAcceptsNull()
    {
        // Arrange — the (field, string[]?, metadata) overload is declared nullable and
        // has always accepted null, adding the field as a leaf
        var builder = QueryBuilder.CreateDefaultBuilder("Q");

        // Act
        builder.AddField("user", (string[]?)null);

        // Assert
        builder.ToString().Should().Contain("user");
    }
}
