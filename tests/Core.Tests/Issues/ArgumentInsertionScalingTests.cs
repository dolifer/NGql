using System.Diagnostics;
using FluentAssertions;
using NGql.Core.Abstractions;
using Xunit;

namespace NGql.Core.Tests.Issues;

public class ArgumentInsertionScalingTests
{
    [Fact]
    public void AddArgument_NewKeys_ScalesNearLinearly()
    {
        Insert(2000);
        var small = Measure(2000);
        var large = Measure(16000);

        // Eight times the keys should cost far less than the 64x of a per-insert key scan.
        large.Should().BeLessThan(small * 24);
    }

    private static long Measure(int count)
    {
        var best = long.MaxValue;
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var watch = Stopwatch.StartNew();
            Insert(count);
            best = System.Math.Min(best, watch.ElapsedTicks);
        }

        return best;
    }

    private static void Insert(int count)
    {
        var block = new QueryBlock("Q");
        for (var i = 0; i < count; i++) block.AddArgument("argument" + i, i);
    }
}
