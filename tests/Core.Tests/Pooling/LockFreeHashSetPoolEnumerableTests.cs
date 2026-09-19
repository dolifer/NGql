using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using NGql.Core.Pooling;
using Xunit;

namespace NGql.Core.Tests.Pooling;

public class LockFreeHashSetPoolEnumerableTests
{
    [Fact]
    public void GetPooled_FromEnumerableSource_PopulatesSetCaseInsensitively()
    {
        IEnumerable<string> source = ["alpha", "BETA", "beta"];

        using var pooled = LockFreeHashSetPool.GetPooled(source);

        pooled.Set.Should().HaveCount(2);
        pooled.Set.Should().Contain("Alpha");
    }

    [Fact]
    public void GetPooled_FromEmptyEnumerableSource_ReturnsEmptySet()
    {
        using var pooled = LockFreeHashSetPool.GetPooled(Enumerable.Empty<string>());

        pooled.Set.Should().BeEmpty();
    }
}
