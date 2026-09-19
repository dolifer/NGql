using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using FluentAssertions;
using NGql.Core.Builders;
using Xunit;
using Xunit.Abstractions;

namespace NGql.Core.Tests.Pooling;

public class BuilderPoolBudgetTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    public void LargeBurst_RespectsPerThreadCapacityBudget(int threadCount)
    {
        var capacities = new int[threadCount];
        var errors = new Exception?[threadCount];
        var threads = new Thread[threadCount];
        for (var i = 0; i < threadCount; i++)
        {
            var index = i;
            threads[i] = new Thread(() =>
            {
                try
                {
                    var builders = Borrow(200000);
                    foreach (var builder in builders) QueryTextBuilder.ReturnToPool(builder);
                    capacities[index] = RetainedCapacity();
                }
                catch (Exception error) { errors[index] = error; }
            });
            threads[i].Start();
        }
        foreach (var thread in threads) thread.Join();
        errors.Should().OnlyContain(error => error == null);
        output.WriteLine($"Retained character capacities on {threadCount} threads: {string.Join(", ", capacities)}");
        capacities.Should().OnlyContain(capacity => capacity <= 262144);
    }

    [Fact]
    public void FourMediumBuilders_AreAllReused()
    {
        var original = Borrow(32768);
        foreach (var builder in original) QueryTextBuilder.ReturnToPool(builder);
        var reused = new QueryTextBuilder[4];
        try
        {
            for (var i = 0; i < reused.Length; i++) reused[i] = QueryTextBuilder.GetFromPool();
            reused.Should().OnlyContain(builder => original.Contains(builder));
        }
        finally
        {
            foreach (var builder in reused) QueryTextBuilder.ReturnToPool(builder);
        }
    }

    [Theory]
    [InlineData(80000, 200000)]
    [InlineData(200000, 80000)]
    public void NestedRender_OverBudget_RetainsLargerBuilderOnTop(int firstReturned, int secondReturned)
    {
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try
            {
                var builders = Borrow(1024);
                SetCapacity(builders[0], firstReturned);
                SetCapacity(builders[1], secondReturned);
                var larger = firstReturned > secondReturned ? builders[0] : builders[1];

                QueryTextBuilder.ReturnToPool(builders[0]);
                QueryTextBuilder.ReturnToPool(builders[1]);

                RetainedCapacity().Should().Be(200000);
                QueryTextBuilder.GetFromPool().Should().BeSameAs(larger);
            }
            catch (Exception caught) { error = caught; }
        });
        thread.Start();
        thread.Join();

        error.Should().BeNull();
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Major Code Smell", "S3011",
        Justification = "Test-only capacity measurement preserves the production API.")]
    private static void SetCapacity(QueryTextBuilder builder, int capacity)
    {
        var field = typeof(QueryTextBuilder).GetField("_stringBuilder", BindingFlags.Instance | BindingFlags.NonPublic)!;
        ((StringBuilder)field.GetValue(builder)!).Capacity = capacity;
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Major Code Smell", "S3011",
        Justification = "Test-only capacity measurement preserves the production API.")]
    private static QueryTextBuilder[] Borrow(int capacity)
    {
        var field = typeof(QueryTextBuilder).GetField("_stringBuilder", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var builders = new QueryTextBuilder[4];
        for (var i = 0; i < builders.Length; i++)
        {
            builders[i] = QueryTextBuilder.GetFromPool();
            ((StringBuilder)field.GetValue(builders[i])!).Capacity = capacity;
        }
        return builders;
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Major Code Smell", "S3011",
        Justification = "Test-only capacity measurement preserves the production API.")]
    private static int RetainedCapacity()
    {
        var stackField = typeof(QueryTextBuilder).GetField("SharedBuilderStack", BindingFlags.Static | BindingFlags.NonPublic)!;
        var builderField = typeof(QueryTextBuilder).GetField("_stringBuilder", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var local = (ThreadLocal<Stack<QueryTextBuilder>>)stackField.GetValue(null)!;
        return local.Value!.Sum(builder => ((StringBuilder)builderField.GetValue(builder)!).Capacity);
    }
}
