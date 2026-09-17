using System;
using System.IO;
using FluentAssertions;
using NGql.Core.Builders;
using Xunit;

namespace NGql.Core.Tests.Issues;

public class SortedRenderAllocationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void WarmSortedRendering_DoesNotAllocateComparisonAdapters(bool nested)
    {
        var query = QueryBuilder.CreateDefaultBuilder("Q");
        for (var i = 0; i < 10; i++) query.AddField(nested ? $"parent.field{i}" : $"field{i}");
        for (var i = 0; i < 100; i++) query.WriteTo(TextWriter.Null);

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 100; i++) query.WriteTo(TextWriter.Null);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        allocated.Should().Be(0);
    }
}
