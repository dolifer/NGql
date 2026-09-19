using System;
using System.IO;
using FluentAssertions;
using Xunit;

namespace NGql.Core.Tests.Issues;

public class SingleArgumentRenderTests
{
    [Theory]
    [InlineData("Argument")]
    [InlineData("Variable")]
    public void WarmSingleEntryRendering_DoesNotAllocate(string scenario)
    {
        var query = new Query("Q").Select("id");
        if (scenario == "Argument") query.Where("first", "value");
        else query.Variable("$first", "Int");
        for (var i = 0; i < 100; i++) query.WriteTo(TextWriter.Null);

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 100; i++) query.WriteTo(TextWriter.Null);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        allocated.Should().Be(0);
    }

    [Fact]
    public void SingleArgument_AfterRejectedCaseCollision_StillRendersOriginalKey()
    {
        var query = new Query("Q").Select("id").Where("First", 1);

        var collide = () => query.Where("first", 2);

        collide.Should().Throw<ArgumentException>();
        query.ToString().Should().Contain("(First:1)");
    }

    [Fact]
    public void SecondArgument_AfterSingleRender_RendersBothInOrdinalOrder()
    {
        var query = new Query("Q").Select("id").Where("beta", 1);
        query.ToString().Should().Contain("(beta:1)");

        query.Where("alpha", 2);

        query.ToString().Should().Contain("(alpha:2, beta:1)");
    }
}
