using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using NGql.Core.Pooling;
using Xunit;

namespace NGql.Core.Tests.Pooling;

/// <summary>
/// Tests for MonitoredLockFreeStack edge cases.
/// Covers thread safety, concurrent access, and LIFO ordering.
/// </summary>
public class ThreadLocalMemoryEdgeCasesTests
{
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
}
