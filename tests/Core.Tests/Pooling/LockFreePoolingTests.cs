using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using NGql.Core.Builders;
using NGql.Core.Pooling;
using Xunit;

namespace NGql.Core.Tests.Pooling;

public class LockFreePoolingTests
{
    [Fact]
    public async Task QueryBuilder_Should_Work_Correctly_Under_Concurrent_Access()
    {
        const int threadCount = 8;
        const int operationsPerThread = 100;
        var tasks = new Task[threadCount];
        var exceptions = new ConcurrentBag<Exception>();

        for (int t = 0; t < threadCount; t++)
        {
            int threadId = t;
            tasks[t] = Task.Run(() =>
            {
                try
                {
                    for (int i = 0; i < operationsPerThread; i++)
                    {
                        var query = QueryBuilder
                            .CreateDefaultBuilder($"Thread{threadId}_Query{i}")
                            .AddField("users", new Dictionary<string, object?> 
                            { 
                                ["threadId"] = threadId,
                                ["iteration"] = i,
                                ["timestamp"] = DateTimeOffset.UtcNow.Ticks
                            })
                            .AddField("users.profile.name")
                            .AddField("users.posts.title");

                        var queryString = query.ToString();
                        queryString.Should().NotBeNullOrEmpty();
                        queryString.Should().Contain($"Thread{threadId}_Query{i}");
                    }
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            });
        }

        await Task.WhenAll(tasks);
        exceptions.Should().BeEmpty("No exceptions should occur during concurrent access");
    }

    [Fact]
    public async Task QueryBuilder_StringBuilder_Pooling_Should_Work_Under_Concurrent_Access()
    {
        const int threadCount = 4;
        const int operationsPerThread = 50;
        var tasks = new Task[threadCount];
        var exceptions = new ConcurrentBag<Exception>();
        var results = new ConcurrentBag<string>();

        for (int t = 0; t < threadCount; t++)
        {
            int threadId = t;
            tasks[t] = Task.Run(() =>
            {
                try
                {
                    for (int i = 0; i < operationsPerThread; i++)
                    {
                        // Create queries that will exercise StringBuilder pooling through ToString()
                        var query = QueryBuilder
                            .CreateDefaultBuilder($"StringBuilderTest{threadId}_{i}")
                            .AddField("user.profile.personalInfo.firstName")
                            .AddField("user.profile.personalInfo.lastName")
                            .AddField("user.profile.contactInfo.email")
                            .AddField("user.posts.title")
                            .AddField("user.posts.content");
                        
                        var result = query.ToString();
                        result.Should().Contain($"StringBuilderTest{threadId}_{i}");
                        results.Add(result);
                    }
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            });
        }

        await Task.WhenAll(tasks);
        exceptions.Should().BeEmpty("No exceptions should occur during concurrent StringBuilder usage");
        results.Should().HaveCount(threadCount * operationsPerThread);
    }

    [Fact]
    public async Task QueryBuilder_Arguments_Pooling_Should_Work_Under_Concurrent_Access()
    {
        const int threadCount = 4;
        const int queriesPerThread = 25;
        var tasks = new Task[threadCount];
        var exceptions = new ConcurrentBag<Exception>();
        var results = new ConcurrentBag<string>();

        for (int t = 0; t < threadCount; t++)
        {
            int threadId = t;
            tasks[t] = Task.Run(() =>
            {
                try
                {
                    for (int i = 0; i < queriesPerThread; i++)
                    {
                        // Create queries with various arguments to exercise argument pooling
                        var query = QueryBuilder
                            .CreateDefaultBuilder($"ArgumentsTest{threadId}_{i}")
                            .AddField("users", new Dictionary<string, object?> { 
                                ["first"] = 10 + threadId, 
                                ["threadId"] = threadId,
                                ["active"] = i % 2 == 0,
                                ["metadata"] = new Dictionary<string, object?> 
                                {
                                    ["source"] = "test",
                                    ["iteration"] = i
                                }
                            })
                            .AddField("users.posts", new Dictionary<string, object?> { 
                                ["limit"] = 5 + i, 
                                ["filter"] = $"thread{threadId}" 
                            })
                            .AddField("users.profile.name")
                            .AddField("users.posts.title");
                        
                        var queryString = query.ToString();
                        queryString.Should().NotBeNullOrEmpty();
                        queryString.Should().Contain($"ArgumentsTest{threadId}_{i}");
                        results.Add(queryString);
                    }
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            });
        }

        await Task.WhenAll(tasks);
        exceptions.Should().BeEmpty("No exceptions should occur during concurrent QueryBuilder usage with arguments");
        results.Should().HaveCount(threadCount * queriesPerThread);
    }

    [Fact]
    public void QueryBuilder_Complex_Nested_Should_Handle_Pooling_Correctly()
    {
        // Test complex scenarios that exercise multiple pooling systems
        const int iterations = 100;
        
        for (int i = 0; i < iterations; i++)
        {
            var query = QueryBuilder
                .CreateDefaultBuilder($"ComplexPoolingTest{i}")
                // Arguments pooling
                .AddField("user", new Dictionary<string, object?> 
                { 
                    ["id"] = $"user{i}",
                    ["metadata"] = new Dictionary<string, object?> { ["test"] = true }
                })
                // Deep nesting (path building / StringBuilder pooling)
                .AddField("user.profile.personalInfo.contact.email.primary")
                .AddField("user.profile.personalInfo.contact.phone.mobile")
                .AddField("user.posts.edges.node.comments.edges.node.author.name")
                // More arguments pooling
                .AddField("user.posts", new Dictionary<string, object?> 
                { 
                    ["first"] = 10, 
                    ["after"] = $"cursor{i}" 
                });

            var result = query.ToString();
            result.Should().NotBeNullOrEmpty();
            result.Should().Contain($"ComplexPoolingTest{i}");
        }
    }

    [Fact]
    public void QueryBuilder_Should_Handle_High_Frequency_Operations()
    {
        // Stress test to ensure pooling doesn't cause issues under high load
        const int iterations = 1000;
        
        var action = () =>
        {
            for (int i = 0; i < iterations; i++)
            {
                var query = QueryBuilder
                    .CreateDefaultBuilder($"HighFreq{i}")
                    .AddField("data", new Dictionary<string, object?> { ["id"] = i })
                    .AddField("data.value");
                
                _ = query.ToString();
            }
        };

        action.Should().NotThrow("High frequency operations should not cause pooling issues");
    }

    [Theory]
    [InlineData("factory")]
    [InlineData("reset")]
    public void ThreadLocalPool_Constructor_NullDelegate_Throws(string nullParam)
    {
        Func<StringBuilder>? factory = nullParam == "factory" ? null : () => new StringBuilder();
        Action<StringBuilder>? reset = nullParam == "reset" ? null : sb => sb.Clear();
        var act = () => new ThreadLocalPool<StringBuilder>(factory!, reset!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task ThreadLocalPool_Return_FromUnseenThread_AllocatesThreadLocalCache()
    {
        // ThreadLocalPool.Return uses `_threadLocalCache.Value ??= new ThreadLocalCache()`.
        // On a thread that has never called Get on this pool instance, the Value is null
        // and the allocation arm fires. A fresh Task thread + an externally-created item
        // forces this code path.
        var pool = new ThreadLocalPool<StringBuilder>(
            factory: () => new StringBuilder(),
            reset: sb => sb.Clear(),
            poolName: "fresh_thread_return");

        var act = async () => await Task.Run(() =>
        {
            // This thread has never touched `pool`, so `_threadLocalCache.Value` is null
            // when Return runs.
            pool.Return(new StringBuilder("seed"));
        });

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public void ThreadLocalPool_Different_Generic_Types_Should_Not_Corrupt_Each_Other()
    {
        // ARRANGE
        var pool1 = new ThreadLocalPool<StringBuilder>(
            factory: () => new StringBuilder(),
            reset: sb => sb.Clear(),
            poolName: "StringBuilderPool"
        );

        var pool2 = new ThreadLocalPool<Dictionary<string, object>>(
            factory: () => new Dictionary<string, object>(),
            reset: dict => dict.Clear(),
            poolName: "DictionaryPool"
        );

        var errors = new List<string>();

        // ACT: Interleave operations from different pools on same thread
        var t1 = new Thread(() =>
        {
            try
            {
                // Get from pool1
                var sb = pool1.Get();
                sb.Should().NotBeNull();
                sb.Should().BeOfType<StringBuilder>();
                sb.Append("thread1");
                sb.ToString().Should().Be("thread1");

                Thread.Yield(); // Context switch point

                // Get from pool2 - should not corrupt pool1 cache
                var dict = pool2.Get();
                dict.Should().NotBeNull();
                dict.Should().BeOfType<Dictionary<string, object>>();

                Thread.Yield();

                // Return to pool1
                pool1.Return(sb);

                Thread.Yield();

                // Get from pool1 again - should still be same object with same identity (reused from pool)
                var sb2 = pool1.Get();
                ReferenceEquals(sb2, sb).Should().BeTrue("Should reuse same StringBuilder from pool");
                sb2.Should().BeOfType<StringBuilder>();
                
                // The StringBuilder will be cleared after return, so it should be empty now
                sb2.ToString().Should().BeEmpty("Should be cleared by reset function");
                
                // Verify we can use it again
                sb2.Append("thread1_reused");
                sb2.ToString().Should().Be("thread1_reused");
            }
            catch (Exception ex)
            {
                errors.Add($"Thread 1: {ex.Message}");
            }
        });

        var t2 = new Thread(() =>
        {
            try
            {
                // Let t1 start first — use SpinWait briefly instead of Thread.Sleep in tests.
                System.Threading.SpinWait.SpinUntil(() => false, TimeSpan.FromMilliseconds(10));

                // Get from pool2
                var dict = pool2.Get();
                dict.Should().NotBeNull();
                dict.Should().BeOfType<Dictionary<string, object>>();
                dict.Add("key1", "value1");

                Thread.Yield();

                // Get from pool1 - should not corrupt pool2 cache
                var sb = pool1.Get();
                sb.Should().NotBeNull();
                sb.Should().BeOfType<StringBuilder>();

                Thread.Yield();

                // Return to pool2
                pool2.Return(dict);

                Thread.Yield();

                // Get from pool2 again - should still be same object (reused from pool)
                var dict2 = pool2.Get();
                ReferenceEquals(dict2, dict).Should().BeTrue("Should reuse same Dictionary from pool");
                dict2.Should().BeOfType<Dictionary<string, object>>();
                
                // Dictionary should be cleared after return
                dict2.Should().BeEmpty("Should be cleared by reset function");
                
                // Verify we can use it again
                dict2.Add("key2", "value2");
                dict2.Should().HaveCount(1);
            }
            catch (Exception ex)
            {
                errors.Add($"Thread 2: {ex.Message}");
            }
        });

        t1.Start();
        t2.Start();

        // ASSERT
        t1.Join(5000);
        t2.Join(5000);

        // If there are errors, the bug is demonstrated
        errors.Should().BeEmpty("Concurrent access to different generic pools should not cause errors");
    }

    [Fact]
    public async Task ThreadLocalPool_High_Contention_Multiple_Types_Should_Maintain_Integrity()
    {
        // ARRANGE - Create multiple pools
        var sbPool = new ThreadLocalPool<StringBuilder>(
            factory: () => new StringBuilder(),
            reset: sb => sb.Clear(),
            poolName: "StringBuilderPool"
        );

        var dictPool = new ThreadLocalPool<Dictionary<string, int>>(
            factory: () => new Dictionary<string, int>(),
            reset: dict => dict.Clear(),
            poolName: "DictionaryPool"
        );

        var listPool = new ThreadLocalPool<List<int>>(
            factory: () => [],
            reset: list => list.Clear(),
            poolName: "ListPool"
        );

        var errors = new ConcurrentBag<string>();
        var tasks = new List<Task>();

        // ACT: High contention across threads and types
        const int threadCount = 8;
        const int operationsPerThread = 50;

        for (int t = 0; t < threadCount; t++)
        {
            tasks.Add(Task.Run(() =>
            {
                try
                {
                    for (int i = 0; i < operationsPerThread; i++)
                    {
                        // Interleaved operations
                        var sb = sbPool.Get();
                        var dict = dictPool.Get();
                        var list = listPool.Get();

                        // Verify types haven't been corrupted
                        if (sb is not StringBuilder)
                            errors.Add($"SB corruption: got {sb?.GetType().Name ?? "null"}");
                        if (dict is not Dictionary<string, int>)
                            errors.Add($"Dict corruption: got {dict?.GetType().Name ?? "null"}");
                        if (list is not List<int>)
                            errors.Add($"List corruption: got {list?.GetType().Name ?? "null"}");

                        // Use the objects
                        sb?.Append("test");
                        dict?.Add("key", i);
                        list?.Add(i);

                        // Return them
                        sbPool.Return(sb);
                        dictPool.Return(dict);
                        listPool.Return(list);

                        // Get again
                        var sb2 = sbPool.Get();
                        var dict2 = dictPool.Get();
                        var list2 = listPool.Get();

                        // Verify we're getting correct types
                        if (sb2 is not StringBuilder)
                            errors.Add("Second SB get returned wrong type");
                        if (dict2 is not Dictionary<string, int>)
                            errors.Add("Second Dict get returned wrong type");
                        if (list2 is not List<int>)
                            errors.Add("Second List get returned wrong type");

                        sbPool.Return(sb2);
                        dictPool.Return(dict2);
                        listPool.Return(list2);
                    }
                }
                catch (Exception ex)
                {
                    errors.Add($"Error: {ex.Message}");
                }
            }));
        }

        // ASSERT
        await Task.WhenAll(tasks);
        errors.Should().BeEmpty("No type corruption should occur under concurrent access");
    }

    [Fact]
    public void ThreadLocalPool_ExhaustThreadLocalCache_UsesGlobalPool()
    {
        var pool = new ThreadLocalPool<StringBuilder>(
            factory: () => new StringBuilder(),
            reset: sb => sb.Clear(),
            poolName: "TestPool"
        );

        // Get many items to fill beyond thread-local cache (typically small)
        var items = new List<StringBuilder>();
        for (int i = 0; i < 200; i++)
        {
            items.Add(pool.Get());
        }

        // Return all items - this will fill both thread-local and global pool
        foreach (var item in items)
        {
            pool.Return(item);
        }

        // Now get again from global pool
        var retrieved = pool.Get();
        retrieved.Should().NotBeNull();
    }

    [Fact]
    public void ThreadLocalPool_GlobalPool_RetrievedFromDifferentThread()
    {
        // The "thread-local empty, global has items" path on Get is reachable when one thread
        // populates the global pool and another thread retrieves an item. Explicit Thread (not
        // Task.Run) guarantees the consumer runs on a fresh thread with an empty thread-local cache.
        var pool = new ThreadLocalPool<StringBuilder>(
            factory: () => new StringBuilder(),
            reset: sb => sb.Clear(),
            poolName: "GlobalRetrievePool");

        var producer = new Thread(() =>
        {
            var produced = new List<StringBuilder>();
            for (int i = 0; i < 64; i++) produced.Add(pool.Get());
            foreach (var sb in produced) pool.Return(sb);
        });
        producer.Start();
        producer.Join();

        StringBuilder? retrieved = null;
        var consumer = new Thread(() => retrieved = pool.Get());
        consumer.Start();
        consumer.Join();

        retrieved.Should().NotBeNull();
    }

    [Fact]
    public void ThreadLocalPool_ReturnRejectedByValidator_NotReturned()
    {
        // Exercises Return when the validator rejects the item (item is dropped, not pooled).
        var usedItems = new HashSet<StringBuilder>();
        
        var pool = new ThreadLocalPool<StringBuilder>(
            factory: () => new StringBuilder(),
            reset: sb => sb.Clear(),
            validateForReturn: sb => !usedItems.Contains(sb),
            poolName: "TestPool"
        );
        
        var item1 = pool.Get();
        usedItems.Add(item1);
        pool.Return(item1); // Validator should reject this
        
        var item2 = pool.Get();
        item2.Should().NotBeNull();
    }

    [Fact]
    public void ThreadLocalPool_MultipleReturnsAndGets_MaintainsPoolInvariant()
    {
        // Covers global pool operations and item reuse
        var pool = new ThreadLocalPool<List<int>>(
            factory: () => [],
            reset: x => x.Clear(),
            poolName: "ListPool"
        );
        
        // Get and return multiple times
        for (int i = 0; i < 50; i++)
        {
            var list = pool.Get();
            list.Should().NotBeNull();
            list.Should().BeEmpty(); // Should be cleared
            
            list.Add(i);
            pool.Return(list);
        }
    }

    [Fact]
    public void LockFreeHashSetPool_GetPooledFromHashSetSource_PopulatesCorrectly()
    {
        var source = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "field1", "field2", "field3" };

        using var pooled = LockFreeHashSetPool.GetPooled(source);

        pooled.Set.Should().HaveCount(3);
        pooled.Set.Should().Contain("field1");
        pooled.Set.Should().Contain("field2");
        pooled.Set.Should().Contain("field3");
    }

    [Fact]
    public void ThreadLocalPool_GlobalPoolFallback_WhenThreadLocalFull()
    {
        var pool = new ThreadLocalPool<List<int>>(
            factory: () => [],
            reset: x => x.Clear(),
            poolName: "FallbackTestPool"
        );

        var items = new List<List<int>>();
        for (int i = 0; i < 100; i++)
        {
            items.Add(pool.Get());
        }

        foreach (var item in items)
        {
            pool.Return(item);
        }

        pool.ApproximateCount.Should().BeGreaterThan(0);
    }
}