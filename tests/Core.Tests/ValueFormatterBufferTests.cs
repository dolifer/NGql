using System;
using System.Globalization;
using System.Text;
using FluentAssertions;
using Xunit;

namespace NGql.Core.Tests;

public class ValueFormatterBufferTests
{
    [Fact]
    public void AppendSpanFormatted_BufferLargeEnough_WritesFormattedValue()
    {
        // Arrange
        var builder = new StringBuilder();
        Span<char> buffer = stackalloc char[ValueFormatter.StackBufferLength];

        // Act
        ValueFormatter.AppendSpanFormatted(builder, 1234.5m, default, buffer);

        // Assert
        builder.ToString().Should().Be("1234.5");
    }

    [Fact]
    public void AppendSpanFormatted_BufferTooSmall_FallsBackToToStringWithoutFormat()
    {
        // Arrange: a one-char buffer cannot hold the value, forcing the allocating fallback.
        var builder = new StringBuilder();
        Span<char> buffer = stackalloc char[1];

        // Act
        ValueFormatter.AppendSpanFormatted(builder, 1234.5m, default, buffer);

        // Assert
        builder.ToString().Should().Be("1234.5");
    }

    [Fact]
    public void AppendSpanFormatted_BufferTooSmallWithFormat_FallsBackPreservingFormat()
    {
        // Arrange
        var builder = new StringBuilder();
        var moment = new DateTime(2024, 5, 17, 13, 45, 30, 123, DateTimeKind.Utc);
        Span<char> buffer = stackalloc char[2];

        // Act
        ValueFormatter.AppendSpanFormatted(builder, moment, ValueFormatter.DateFormat, buffer);

        // Assert
        builder.ToString().Should().Be(moment.ToString(ValueFormatter.DateFormat, CultureInfo.InvariantCulture));
    }

    [Fact]
    public void AppendSpanFormatted_BufferLargeEnoughWithFormat_WritesFormattedValue()
    {
        // Arrange
        var builder = new StringBuilder();
        var moment = new DateTime(2024, 5, 17, 13, 45, 30, 123, DateTimeKind.Utc);
        Span<char> buffer = stackalloc char[ValueFormatter.StackBufferLength];

        // Act
        ValueFormatter.AppendSpanFormatted(builder, moment, ValueFormatter.DateFormat, buffer);

        // Assert
        builder.ToString().Should().Be(moment.ToString(ValueFormatter.DateFormat, CultureInfo.InvariantCulture));
    }

    [Fact]
    public void AppendSpanFormatted_EveryProductionValueType_HandlesBothBufferSizes()
    {
        // Arrange & Act & Assert: the helper is generic, so each value type it is instantiated
        // with in production needs both the span-write and the fallback path exercised.
        AssertBothBufferSizes(1234567, "1234567");
        AssertBothBufferSizes(1234567890123L, "1234567890123");
        AssertBothBufferSizes(1234.5f, "1234.5");
        AssertBothBufferSizes(1234.5d, "1234.5");
        AssertBothBufferSizes(1234.5m, "1234.5");
    }

    [Fact]
    public void AppendSpanFormatted_DateTimeOffset_HandlesBothBufferSizes()
    {
        // Arrange
        var moment = new DateTimeOffset(2024, 5, 17, 13, 45, 30, 123, TimeSpan.Zero);
        var expected = moment.ToString(ValueFormatter.DateFormat, CultureInfo.InvariantCulture);

        // Act
        var large = new StringBuilder();
        Span<char> largeBuffer = stackalloc char[ValueFormatter.StackBufferLength];
        ValueFormatter.AppendSpanFormatted(large, moment, ValueFormatter.DateFormat, largeBuffer);

        var small = new StringBuilder();
        Span<char> smallBuffer = stackalloc char[2];
        ValueFormatter.AppendSpanFormatted(small, moment, ValueFormatter.DateFormat, smallBuffer);

        // Assert
        large.ToString().Should().Be(expected);
        small.ToString().Should().Be(expected);
    }

    private static void AssertBothBufferSizes<T>(T value, string expected)
        where T : struct, ISpanFormattable
    {
        var large = new StringBuilder();
        Span<char> largeBuffer = stackalloc char[ValueFormatter.StackBufferLength];
        ValueFormatter.AppendSpanFormatted(large, value, default, largeBuffer);

        var small = new StringBuilder();
        Span<char> smallBuffer = stackalloc char[1];
        ValueFormatter.AppendSpanFormatted(small, value, default, smallBuffer);

        large.ToString().Should().Be(expected);
        small.ToString().Should().Be(expected);
    }
}
