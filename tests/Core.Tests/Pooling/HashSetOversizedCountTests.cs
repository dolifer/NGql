using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using NGql.Core.Pooling;
using Xunit;

namespace NGql.Core.Tests.Pooling;

public class HashSetOversizedCountTests
{
    [Fact]
    public void Return_DoesNotRetainSetStillHoldingMoreThanMaxItems()
    {
        var pooled = LockFreeHashSetPool.GetPooled();
        var oversized = pooled.Set;
        for (var i = 0; i < 500; i++) oversized.Add(i.ToString());
        oversized.Count.Should().BeGreaterThan(128);
        pooled.Dispose();

        using var next = LockFreeHashSetPool.GetPooled();

        next.Set.Should().NotBeSameAs(oversized);
    }
}
