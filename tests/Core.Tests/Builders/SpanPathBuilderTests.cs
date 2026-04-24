using System;
using FluentAssertions;
using NGql.Core.Builders;
using Xunit;

namespace NGql.Core.Tests.Builders;

public class SpanPathBuilderTests
{
    [Fact]
    public void FirstAppend_NoSeparatorPrepended()
    {
        Span<char> buffer = stackalloc char[512];
        var builder = new SpanPathBuilder(buffer);

        builder.Append("users");
        builder.ToString().Should().Be("users");
    }

    [Fact]
    public void MultipleAppends_JoinedWithSeparators()
    {
        Span<char> buffer = stackalloc char[512];
        var builder = new SpanPathBuilder(buffer);

        builder.Append("users");
        builder.Append("profile");
        builder.Append("name");

        builder.ToString().Should().Be("users.profile.name");
    }

    [Fact]
    public void GradualFill_SmallSegments_Succeeds()
    {
        Span<char> buffer = stackalloc char[512];
        var builder = new SpanPathBuilder(buffer);

        for (int i = 0; i < 50; i++)
        {
            builder.Append("a");
        }

        builder.ToString().Length.Should().BeLessThan(512);
    }

    [Fact]
    public void BufferFull_SegmentOverflow_Throws()
    {
        Span<char> buffer = stackalloc char[30];
        var builder = new SpanPathBuilder(buffer);
        builder.Append("12345678901234567890"); // 20 chars

        try
        {
            builder.Append("123456789012345"); // Won't fit
            throw new Xunit.Sdk.XunitException("Should throw");
        }
        catch (InvalidOperationException ex)
        {
            ex.Message.Should().Contain("segment");
        }
    }

    [Fact]
    public void SeparatorOverflow_BufferAtCapacity_ThrowsWithSeparatorMessage()
    {
        Span<char> buffer = stackalloc char[20];
        var builder = new SpanPathBuilder(buffer);
        builder.Append("12345678901234567890"); // Exactly 20 chars

        try
        {
            builder.Append("x");
            throw new Xunit.Sdk.XunitException("Should throw for separator overflow");
        }
        catch (InvalidOperationException ex)
        {
            ex.Message.Should().Contain("separator");
        }
    }

    [Fact]
    public void SegmentOverflow_LargeSegmentWithLimitedCapacity_ThrowsWithSegmentMessage()
    {
        Span<char> buffer = stackalloc char[25];
        var builder = new SpanPathBuilder(buffer);
        builder.Append("12345678901234567890"); // 20 chars, 5 remaining

        try
        {
            builder.Append("123456"); // 6 chars won't fit (need sep+seg)
            throw new Xunit.Sdk.XunitException("Should throw for segment overflow");
        }
        catch (InvalidOperationException ex)
        {
            ex.Message.Should().Contain("segment");
        }
    }

    [Fact]
    public void ToString_ConvertSpanToString_ReturnsCorrectValue()
    {
        Span<char> buffer = stackalloc char[100];
        var builder = new SpanPathBuilder(buffer);
        
        builder.Append("a");
        builder.Append("b");
        builder.Append("c");
        
        var result = builder.ToString();
        
        result.Should().Be("a.b.c");
        result.Should().BeOfType<string>();
    }
}
