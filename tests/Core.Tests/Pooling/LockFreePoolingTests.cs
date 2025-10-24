using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using NGql.Core.Builders;
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
}