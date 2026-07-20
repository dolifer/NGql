using System;
using System.Collections.Generic;
using FluentAssertions;
using NGql.Core.Builders;
using Xunit;

namespace NGql.Core.Tests.Issues;

/// <summary>
/// Regression tests: argument VALUES must never render as invalid GraphQL.
/// Covers non-finite floats/doubles (NaN/Infinity — no valid GraphQL FloatValue) and
/// enum values that are not a single defined Name (undefined numeric or unnamed [Flags]
/// combination — no valid GraphQL EnumValue).
/// </summary>
public class InvalidArgumentValueRenderingTests
{
    // A [Flags] enum with NO named member for the Read|Write combination (value 3),
    // so Enum.IsDefined(typeof(Access), Access.Read | Access.Write) is false.
    [Flags]
    private enum Access
    {
        None = 0,
        Read = 1,
        Write = 2,
        Execute = 4
    }

    private enum Sort
    {
        Ascending = 1,
        Descending = 2
    }

    private static string RenderArg(object? value)
        => QueryBuilder.CreateDefaultBuilder("Q")
            .AddField("field", new Dictionary<string, object?> { ["arg"] = value })
            .ToString();

    #region Non-finite float / double values must throw (invalid GraphQL FloatValue)

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void RenderArg_NonFiniteDouble_Throws(double value)
    {
        // Arrange
        var act = () => RenderArg(value);

        // Act & Assert
        act.Should().Throw<FormatException>()
            .WithMessage("*NaN/Infinity*");
    }

    [Theory]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    public void RenderArg_NonFiniteFloat_Throws(float value)
    {
        // Arrange
        var act = () => RenderArg(value);

        // Act & Assert
        act.Should().Throw<FormatException>()
            .WithMessage("*NaN/Infinity*");
    }

    [Fact]
    public void RenderArg_FiniteDouble_RendersInvariant()
    {
        // Arrange & Act
        var result = RenderArg(2.5d);

        // Assert
        result.Should().Contain("arg:2.5");
    }

    [Fact]
    public void RenderArg_FiniteFloat_RendersInvariant()
    {
        // Arrange & Act
        var result = RenderArg(3.14f);

        // Assert
        result.Should().Contain("arg:3.14");
    }

    [Fact]
    public void RenderArg_Decimal_RendersInvariant()
    {
        // Arrange & Act
        var result = RenderArg(3.14m);

        // Assert
        result.Should().Contain("arg:3.14");
    }

    #endregion

    #region Enum arguments must be a single defined Name (invalid GraphQL EnumValue)

    [Fact]
    public void RenderArg_SingleDefinedEnum_RendersName()
    {
        // Arrange & Act
        var result = RenderArg(Sort.Ascending);

        // Assert
        result.Should().Contain("arg:Ascending");
    }

    [Fact]
    public void RenderArg_SingleDefinedFlagsEnumMember_RendersName()
    {
        // Arrange & Act
        var result = RenderArg(Access.Read);

        // Assert
        result.Should().Contain("arg:Read");
    }

    [Fact]
    public void RenderArg_UnnamedFlagsCombination_Throws()
    {
        // Arrange
        var act = () => RenderArg(Access.Read | Access.Write);

        // Act & Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*single defined enum member*");
    }

    [Fact]
    public void RenderArg_UndefinedEnumValue_Throws()
    {
        // Arrange
        var act = () => RenderArg((Sort)999);

        // Act & Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*single defined enum member*");
    }

    #endregion

    #region EnumValue parity — same guard on the EnumValue(object) path

    [Fact]
    public void EnumValue_SingleDefinedEnum_RendersName()
    {
        // Arrange & Act
        var enumValue = new EnumValue(Sort.Descending);

        // Assert
        enumValue.Value.Should().Be("Descending");
    }

    [Fact]
    public void EnumValue_SingleDefinedFlagsEnumMember_RendersName()
    {
        // Arrange & Act
        var enumValue = new EnumValue(Access.Write);

        // Assert
        enumValue.Value.Should().Be("Write");
    }

    [Fact]
    public void EnumValue_UnnamedFlagsCombination_Throws()
    {
        // Arrange
        var act = () => new EnumValue(Access.Read | Access.Write);

        // Act & Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*single defined enum member*");
    }

    [Fact]
    public void EnumValue_UndefinedEnumValue_Throws()
    {
        // Arrange
        var act = () => new EnumValue((Sort)999);

        // Act & Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*single defined enum member*");
    }

    #endregion
}
