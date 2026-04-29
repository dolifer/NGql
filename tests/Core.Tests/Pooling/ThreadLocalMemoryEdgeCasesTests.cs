using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using NGql.Core.Pooling;
using Xunit;

namespace NGql.Core.Tests.Pooling;

/// <summary>
/// Tests for ThreadLocalMemoryManager and MonitoredLockFreeStack edge cases.
/// Covers thread safety, cache statistics, concurrent access, LIFO ordering, and pool metrics.
/// </summary>
public class ThreadLocalMemoryEdgeCasesTests
{
    [Fact]
    public void ThreadLocalMemoryManager_CacheStats_CalculatesHitRatio_Correctly()
    {
        // Arrange
        var stats = new ThreadLocalMemoryManager.CacheStats(
            threadLocalHits: 80,
            globalPoolHits: 10,
            allocations: 10);

        // Act & Assert
        stats.ThreadLocalHits.Should().Be(80);
        stats.GlobalPoolHits.Should().Be(10);
        stats.Allocations.Should().Be(10);
        stats.ThreadLocalHitRatio.Should().Be(0.8); // 80 / 100
    }

    [Fact]
    public void ThreadLocalMemoryManager_CacheStats_HandleZeroTotalRequests()
    {
        // Arrange
        var stats = new ThreadLocalMemoryManager.CacheStats(0, 0, 0);

        // Act & Assert
        stats.ThreadLocalHitRatio.Should().Be(0.0);
        stats.ThreadLocalHits.Should().Be(0);
        stats.GlobalPoolHits.Should().Be(0);
        stats.Allocations.Should().Be(0);
    }

    [Fact]
    public void ThreadLocalMemoryManager_CacheStats_CalculatesRatio_WithOnlyGlobalPoolHits()
    {
        // Arrange
        var stats = new ThreadLocalMemoryManager.CacheStats(0, 100, 0);

        // Act & Assert
        stats.ThreadLocalHitRatio.Should().Be(0.0); // No thread-local hits
        stats.GlobalPoolHits.Should().Be(100);
    }

    [Fact]
    public void ThreadLocalMemoryManager_CacheStats_CalculatesRatio_WithAllocationDominance()
    {
        // Arrange
        var stats = new ThreadLocalMemoryManager.CacheStats(10, 10, 80);

        // Act & Assert
        stats.ThreadLocalHitRatio.Should().Be(0.1); // 10 / 100
    }

    [Fact]
    public void ThreadLocalMemoryManager_RecordThreadLocalHit_IncrementsCounter()
    {
        // Arrange - Reset and get baseline
        ThreadLocalMemoryManager.ResetThreadStats();
        ThreadLocalMemoryManager.RecordThreadLocalHit("test_pool");
        var firstRead = ThreadLocalMemoryManager.GetThreadStats();

        // Act
        ThreadLocalMemoryManager.RecordThreadLocalHit("test_pool");

        // Assert
        var secondRead = ThreadLocalMemoryManager.GetThreadStats();
        secondRead.ThreadLocalHits.Should().Be(firstRead.ThreadLocalHits + 1);
    }

    [Fact]
    public void ThreadLocalMemoryManager_RecordGlobalPoolHit_IncrementsCounter()
    {
        // Arrange
        ThreadLocalMemoryManager.ResetThreadStats();
        ThreadLocalMemoryManager.RecordGlobalPoolHit("test_pool", 5);
        var firstRead = ThreadLocalMemoryManager.GetThreadStats();

        // Act
        ThreadLocalMemoryManager.RecordGlobalPoolHit("test_pool", 5);

        // Assert
        var secondRead = ThreadLocalMemoryManager.GetThreadStats();
        secondRead.GlobalPoolHits.Should().Be(firstRead.GlobalPoolHits + 1);
    }

    [Fact]
    public void ThreadLocalMemoryManager_RecordAllocation_IncrementsCounter()
    {
        // Arrange
        ThreadLocalMemoryManager.ResetThreadStats();
        ThreadLocalMemoryManager.RecordAllocation("test_pool");
        var firstRead = ThreadLocalMemoryManager.GetThreadStats();

        // Act
        ThreadLocalMemoryManager.RecordAllocation("test_pool");

        // Assert
        var secondRead = ThreadLocalMemoryManager.GetThreadStats();
        secondRead.Allocations.Should().Be(firstRead.Allocations + 1);
    }

    [Fact]
    public void ThreadLocalMemoryManager_ResetThreadStats_ClearsAllCounters()
    {
        // Arrange
        ThreadLocalMemoryManager.RecordThreadLocalHit("pool1");
        ThreadLocalMemoryManager.RecordGlobalPoolHit("pool1", 1);
        ThreadLocalMemoryManager.RecordAllocation("pool1");

        // Act
        ThreadLocalMemoryManager.ResetThreadStats();
        var stats = ThreadLocalMemoryManager.GetThreadStats();

        // Assert
        stats.ThreadLocalHits.Should().Be(0);
        stats.GlobalPoolHits.Should().Be(0);
        stats.Allocations.Should().Be(0);
    }

    [Fact]
    public void ThreadLocalMemoryManager_GetThreadStats_ReturnsCoreectStats()
    {
        // Arrange
        ThreadLocalMemoryManager.ResetThreadStats();
        ThreadLocalMemoryManager.RecordThreadLocalHit("pool");
        ThreadLocalMemoryManager.RecordThreadLocalHit("pool");
        ThreadLocalMemoryManager.RecordGlobalPoolHit("pool", -1);
        ThreadLocalMemoryManager.RecordAllocation("pool");

        // Act
        var stats = ThreadLocalMemoryManager.GetThreadStats();

        // Assert
        stats.ThreadLocalHits.Should().Be(2);
        stats.GlobalPoolHits.Should().Be(1);
        stats.Allocations.Should().Be(1);
        stats.ThreadLocalHitRatio.Should().Be(0.5); // 2 / 4
    }

    [Fact]
    public void ThreadLocalMemoryManager_ReportPoolEfficiency_CompletesWithoutError()
    {
        // Arrange
        ThreadLocalMemoryManager.ResetThreadStats();
        ThreadLocalMemoryManager.RecordThreadLocalHit("pool");
        ThreadLocalMemoryManager.RecordGlobalPoolHit("pool", -1);

        // Act & Assert (should not throw)
        var action = () => ThreadLocalMemoryManager.ReportPoolEfficiency("test_pool");
        action.Should().NotThrow();
    }

    [Fact]
    public void ThreadLocalMemoryManager_WarmupThreadLocalCaches_CompletesSuccessfully()
    {
        // Arrange & Act & Assert (should not throw)
        var action = () => ThreadLocalMemoryManager.WarmupThreadLocalCaches();
        action.Should().NotThrow();
    }

    [Fact]
    public async Task ThreadLocalMemoryManager_ThreadStats_AreIsolatedPerThread()
    {
        // Arrange
        var thread1Stats = default(ThreadLocalMemoryManager.CacheStats);
        var thread2Stats = default(ThreadLocalMemoryManager.CacheStats);

        // Act
        var task1 = Task.Run(() =>
        {
            ThreadLocalMemoryManager.ResetThreadStats();
            ThreadLocalMemoryManager.RecordThreadLocalHit("pool");
            ThreadLocalMemoryManager.RecordThreadLocalHit("pool");
            thread1Stats = ThreadLocalMemoryManager.GetThreadStats();
        });

        var task2 = Task.Run(() =>
        {
            ThreadLocalMemoryManager.ResetThreadStats();
            ThreadLocalMemoryManager.RecordAllocation("pool");
            thread2Stats = ThreadLocalMemoryManager.GetThreadStats();
        });

        await Task.WhenAll(task1, task2);

        // Assert - Stats should be isolated per thread
        thread1Stats.ThreadLocalHits.Should().Be(2);
        thread1Stats.Allocations.Should().Be(0);
        thread2Stats.ThreadLocalHits.Should().Be(0);
        thread2Stats.Allocations.Should().Be(1);
    }

    [Fact]
    public void MonitoredLockFreeStack_Push_StoresItem()
    {
        // Arrange
        var stack = new MonitoredLockFreeStack<string>();
        var item = "test_item";

        // Act
        stack.Push(item);

        // Assert
        stack.TryPop(out var popped).Should().BeTrue();
        popped.Should().Be(item);
    }

    [Fact]
    public void MonitoredLockFreeStack_TryPop_OnEmptyStack_ReturnsFalse()
    {
        // Arrange
        var stack = new MonitoredLockFreeStack<string>();

        // Act
        var result = stack.TryPop(out _);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void MonitoredLockFreeStack_PushMultiple_PopsInLIFOOrder()
    {
        // Arrange
        var stack = new MonitoredLockFreeStack<string>();
        stack.Push("item1");
        stack.Push("item2");
        stack.Push("item3");

        // Act & Assert (LIFO order)
        stack.TryPop(out var first).Should().BeTrue();
        first.Should().Be("item3");

        stack.TryPop(out var second).Should().BeTrue();
        second.Should().Be("item2");

        stack.TryPop(out var third).Should().BeTrue();
        third.Should().Be("item1");

        stack.TryPop(out _).Should().BeFalse();
    }

    [Fact]
    public async Task MonitoredLockFreeStack_ThreadSafePush_ConcurrentOperations()
    {
        // Arrange
        var stack = new MonitoredLockFreeStack<string>();
        var threadCount = 4;
        var itemsPerThread = 25;
        var tasks = new Task[threadCount];

        // Act
        for (int t = 0; t < threadCount; t++)
        {
            int threadId = t;
            tasks[t] = Task.Run(() =>
            {
                for (int i = 0; i < itemsPerThread; i++)
                {
                    stack.Push($"item_{threadId}_{i}");
                }
            });
        }

        await Task.WhenAll(tasks);

        // Assert - All items should be retrievable
        int popCount = 0;
        while (stack.TryPop(out _))
        {
            popCount++;
        }

        popCount.Should().Be(threadCount * itemsPerThread);
    }

    [Fact]
    public async Task MonitoredLockFreeStack_ThreadSafeMixedOperations_PushAndPop()
    {
        // Arrange
        var stack = new MonitoredLockFreeStack<string>();
        var itemsAdded = 0;
        var itemsRemoved = 0;
        var itemsLock = new object();

        // Act
        var pushTask = Task.Run(() =>
        {
            for (int i = 0; i < 500; i++)
            {
                stack.Push($"item_{i}");
                lock (itemsLock) itemsAdded++;
                Thread.Yield();
            }
        });

        var popTask = Task.Run(() =>
        {
            Thread.Yield();
            while (itemsRemoved < 500)
            {
                if (stack.TryPop(out _))
                {
                    lock (itemsLock) itemsRemoved++;
                }
                else
                {
                    Thread.Yield();
                }
            }
        });

        await Task.WhenAll(pushTask, popTask);

        // Assert
        itemsAdded.Should().Be(500);
        itemsRemoved.Should().Be(500);
    }

    [Fact]
    public void MonitoredLockFreeStack_RecordsPoolingMetrics_OnTryPop()
    {
        // Arrange
        var stack = new MonitoredLockFreeStack<string>();
        stack.Push("item1");
        ThreadLocalMemoryManager.ResetThreadStats();

        // Act
        var result = stack.TryPop(out _);

        // Assert
        result.Should().BeTrue();
        var stats = ThreadLocalMemoryManager.GetThreadStats();
        stats.GlobalPoolHits.Should().Be(1); // TryPop records a global pool hit
    }

    [Theory]
    [InlineData("String", 100)]
    [InlineData("Int", 50)]
    [InlineData("Boolean", 200)]
    public void ThreadLocalMemoryManager_RecordGlobalPoolHit_WithVariousPoolSizes(string poolType, int poolSize)
    {
        // Arrange
        ThreadLocalMemoryManager.ResetThreadStats();

        // Act
        ThreadLocalMemoryManager.RecordGlobalPoolHit(poolType, poolSize);

        // Assert
        var stats = ThreadLocalMemoryManager.GetThreadStats();
        stats.GlobalPoolHits.Should().Be(1);
    }

    [Theory]
    [InlineData("StringBuilder")]
    [InlineData("Dictionary")]
    [InlineData("HashSet")]
    [InlineData("List")]
    public void ThreadLocalMemoryManager_MultiplePoolTypes_TrackIndependently(string poolType)
    {
        // Arrange
        ThreadLocalMemoryManager.ResetThreadStats();

        // Act
        ThreadLocalMemoryManager.RecordThreadLocalHit(poolType);
        ThreadLocalMemoryManager.RecordThreadLocalHit(poolType);
        ThreadLocalMemoryManager.RecordGlobalPoolHit(poolType, -1);

        // Assert
        var stats = ThreadLocalMemoryManager.GetThreadStats();
        stats.ThreadLocalHits.Should().Be(2);
        stats.GlobalPoolHits.Should().Be(1);
    }

    [Fact]
    public void ThreadLocalMemoryManager_CacheStats_EdgeCase_VeryLargeNumbers()
    {
        // Arrange
        var stats = new ThreadLocalMemoryManager.CacheStats(
            threadLocalHits: 100,
            globalPoolHits: 100,
            allocations: 100);

        // Act & Assert
        stats.ThreadLocalHitRatio.Should().Be(1.0 / 3.0); // 100 / 300
        stats.ThreadLocalHits.Should().Be(100);
    }
}
