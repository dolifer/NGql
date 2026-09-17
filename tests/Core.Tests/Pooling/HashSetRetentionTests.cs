using FluentAssertions;
using NGql.Core.Pooling;
using Xunit;

namespace NGql.Core.Tests.Pooling;

public class HashSetRetentionTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Return_DoesNotRetainOversizedStorageAfterItemsAreRemoved(bool clear)
    {
        var pooled = LockFreeHashSetPool.GetPooled();
        var oversized = pooled.Set;
        for (var i = 0; i < 2000; i++) oversized.Add(i.ToString());
        if (clear) oversized.Clear();
        else
        {
            for (var i = 0; i < 2000; i++) oversized.Remove(i.ToString());
        }
        oversized.EnsureCapacity(0).Should().BeGreaterThan(2000);
        pooled.Dispose();

        using var next = LockFreeHashSetPool.GetPooled();
        next.Set.Should().NotBeSameAs(oversized);
        next.Set.EnsureCapacity(0).Should().BeLessThanOrEqualTo(256);
    }

    [Fact]
    public void Return_ReusesNormallySizedStorage()
    {
        var pooled = LockFreeHashSetPool.GetPooled();
        var set = pooled.Set;
        for (var i = 0; i < 128; i++) set.Add(i.ToString());
        pooled.Dispose();

        using var next = LockFreeHashSetPool.GetPooled();
        next.Set.Should().BeSameAs(set).And.BeEmpty();
    }
}
