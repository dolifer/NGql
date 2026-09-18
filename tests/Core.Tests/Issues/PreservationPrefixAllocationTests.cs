using System;
using System.Linq;
using FluentAssertions;
using NGql.Core.Builders;
using Xunit;

namespace NGql.Core.Tests.Issues;

public class PreservationPrefixAllocationTests
{
    private static readonly string[] DeepPaths = Enumerable.Range(0, 512)
        .Select(i => $"user.profile.settings.privacy.option{i}")
        .ToArray();

    [Fact]
    public void Preserve_DeepPathsAfterShortPath_AllocatesNoPrefixStrings()
    {
        var source = QueryBuilder.CreateDefaultBuilder("Source").AddField("id");
        Measure(source, null);
        Measure(source, "id");

        var withoutShortPath = Measure(source, null);
        var withShortPath = Measure(source, "id");

        // Four ancestor prefixes per path would add roughly 512 * 4 * 50 B = 100 KB.
        (withShortPath - withoutShortPath).Should().BeLessThan(16 * 1024);
    }

    [Fact]
    public void Preserve_DescendantAfterAncestorWithShortPath_StillPrunesAncestor()
    {
        var source = QueryBuilder.CreateDefaultBuilder("Source")
            .AddField("id")
            .AddField("user.profile.name")
            .AddField("user.profile.email");

        var result = PreservationBuilder.Create(source)
            .Preserve("id", "USER.profile", "user.profile.name")
            .Build()
            .ToString();

        result.Should().Contain("name").And.Contain("id").And.NotContain("email");
    }

    private static long Measure(QueryBuilder source, string? shortPath)
    {
        var builder = PreservationBuilder.Create(source);
        if (shortPath is not null) builder.Preserve(shortPath);

        var before = GC.GetAllocatedBytesForCurrentThread();
        builder.Preserve(DeepPaths);
        return GC.GetAllocatedBytesForCurrentThread() - before;
    }
}
