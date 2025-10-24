using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Jobs;
using NGql.Core.Builders;

namespace Benchmarks.Benchmarks;

/// <summary>
/// Benchmarks to measure lock-free pooling performance improvements in multi-threaded scenarios.
/// Note: This tests the overall performance impact through QueryBuilder usage, 
/// since the pooling classes are internal implementation details.
/// </summary>
[Config(typeof(Config))]
[MemoryDiagnoser]
[ThreadingDiagnoser]
public class LockFreePoolingBenchmark
{
#pragma warning disable S1144 // Remove unused constructor of private type 'Config'
    private sealed class Config : ManualConfig
    {
        public Config()
        {
            // Test single-threaded vs multi-threaded scenarios
            AddJob(Job.Default.WithId("SingleThread"));
            AddJob(Job.Default.WithUnrollFactor(4).WithInvocationCount(250).WithId("MultiThread"));
            
            AddDiagnoser(MemoryDiagnoser.Default);
            AddDiagnoser(ThreadingDiagnoser.Default);
            WithOption(ConfigOptions.JoinSummary, true);
        }
    }
#pragma warning restore S1144

    [Params(1, 4, 8)]
    public int ThreadCount { get; set; }

    [Params(100, 500)]
    public int OperationsPerThread { get; set; }

    [Benchmark(Baseline = true)]
    public void SingleThreaded_QueryBuilding()
    {
        for (int i = 0; i < OperationsPerThread; i++)
        {
            var query = QueryBuilder
                .CreateDefaultBuilder($"Query{i}")
                .AddField("users", new Dictionary<string, object?> { ["first"] = 10, ["skip"] = i })
                .AddField("users.profile.name")
                .AddField("users.profile.email")
                .AddField("users.posts", new Dictionary<string, object?> { ["limit"] = 5 })
                .AddField("users.posts.title")
                .AddField("users.posts.publishedAt");
            
            _ = query.ToString();
        }
    }

    [Benchmark]
    public void MultiThreaded_QueryBuilding()
    {
        if (ThreadCount == 1)
        {
            SingleThreaded_QueryBuilding();
            return;
        }

        var tasks = new Task[ThreadCount];
        
        for (int t = 0; t < ThreadCount; t++)
        {
            int threadId = t;
            tasks[t] = Task.Run(() =>
            {
                for (int i = 0; i < OperationsPerThread; i++)
                {
                    var query = QueryBuilder
                        .CreateDefaultBuilder($"Thread{threadId}_Query{i}")
                        .AddField("users", new Dictionary<string, object?> { 
                            ["first"] = 10 + threadId, 
                            ["skip"] = i * threadId,
                            ["filter"] = $"thread{threadId}"
                        })
                        .AddField("users.profile.name")
                        .AddField("users.profile.email")
                        .AddField("users.profile.avatar")
                        .AddField("users.posts", new Dictionary<string, object?> { 
                            ["limit"] = 5 + threadId,
                            ["sortBy"] = "publishedAt"
                        })
                        .AddField("users.posts.title")
                        .AddField("users.posts.publishedAt")
                        .AddField("users.posts.author.name");
                    
                    _ = query.ToString();
                }
            });
        }

        Task.WaitAll(tasks);
    }

    [Benchmark]
    public void HighContention_Scenario()
    {
        // Test high contention scenario with many threads accessing pools simultaneously
        var threadCount = Math.Max(ThreadCount, Environment.ProcessorCount);
        var tasks = new Task[threadCount];
        
        for (int t = 0; t < threadCount; t++)
        {
            int threadId = t;
            tasks[t] = Task.Run(() =>
            {
                for (int i = 0; i < OperationsPerThread / 2; i++)
                {
                    // Create multiple queries rapidly to stress the internal pooling system
                    var query1 = QueryBuilder
                        .CreateDefaultBuilder($"HighContention{threadId}_{i}_A")
                        .AddField("data", new Dictionary<string, object?> { 
                            ["threadId"] = threadId,
                            ["iteration"] = i,
                            ["timestamp"] = DateTimeOffset.UtcNow.Ticks
                        });

                    var query2 = QueryBuilder
                        .CreateDefaultBuilder($"HighContention{threadId}_{i}_B")
                        .AddField("metadata.info")
                        .AddField("metadata.tags");

                    _ = query1.ToString();
                    _ = query2.ToString();
                }
            });
        }

        Task.WaitAll(tasks);
    }

    [Benchmark]
    public void ComplexQueryBuilding_Stress()
    {
        // Test complex query building that exercises multiple pool types
        var tasks = new Task[Math.Min(ThreadCount, 4)]; // Limit for stress test
        
        for (int t = 0; t < tasks.Length; t++)
        {
            int threadId = t;
            tasks[t] = Task.Run(() =>
            {
                for (int i = 0; i < OperationsPerThread; i++)
                {
                    var query = QueryBuilder
                        .CreateDefaultBuilder($"ComplexThread{threadId}_{i}")
                        // Multiple nested fields to stress argument pooling
                        .AddField("user", new Dictionary<string, object?> 
                        { 
                            ["id"] = $"user{threadId}_{i}",
                            ["active"] = true,
                            ["metadata"] = new Dictionary<string, object?> 
                            { 
                                ["source"] = "benchmark",
                                ["threadId"] = threadId 
                            }
                        })
                        .AddField("user.profile.personalInfo.firstName")
                        .AddField("user.profile.personalInfo.lastName")
                        .AddField("user.profile.contactInfo.email")
                        .AddField("user.profile.contactInfo.phone")
                        // Arrays and collections to test different path building
                        .AddField("user.posts", new Dictionary<string, object?> 
                        { 
                            ["first"] = 20,
                            ["orderBy"] = "createdAt",
                            ["filter"] = new Dictionary<string, object?> 
                            { 
                                ["status"] = "published" 
                            }
                        })
                        .AddField("user.posts.edges.node.title")
                        .AddField("user.posts.edges.node.content")
                        .AddField("user.posts.edges.node.author.name");
                    
                    _ = query.ToString();
                }
            });
        }

        Task.WaitAll(tasks);
    }
}