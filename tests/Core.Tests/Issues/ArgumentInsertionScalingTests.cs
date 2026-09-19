using System;
using FluentAssertions;
using NGql.Core.Abstractions;
using Xunit;

namespace NGql.Core.Tests.Issues;

public class ArgumentInsertionScalingTests
{
    [Fact]
    public void AddArgument_NewKeys_DoesNotEnumerateExistingKeys()
    {
        var keys = new string[4000];
        for (var i = 0; i < keys.Length; i++) keys[i] = "argument" + i;
        Insert(keys);

        var before = GC.GetAllocatedBytesForCurrentThread();
        Insert(keys);
        var bytesPerKey = (GC.GetAllocatedBytesForCurrentThread() - before) / keys.Length;

        // A tree node costs about 56 B per key. Scanning the stored keys on every insert adds a
        // traversal stack per call (over 200 B per key at this size) and makes insertion quadratic.
        bytesPerKey.Should().BeLessThan(120);
    }

    private static void Insert(string[] keys)
    {
        var block = new QueryBlock("Q");
        for (var i = 0; i < keys.Length; i++) block.AddArgument(keys[i], i);
    }
}
