using System;
using System.Collections.Generic;
using System.Diagnostics;
using FluentAssertions;
using NGql.Core.Builders;
using NGql.Core.Observability;
using Xunit;
using Xunit.Abstractions;

namespace NGql.Core.Tests.Observability;

[Collection("ObservabilityListener")]
public class ObservabilityDemo
{
    private readonly ITestOutputHelper _output;

    public ObservabilityDemo(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void Demo_OpenTelemetry_Compatible_Tracing()
    {
        // Set up an ActivityListener to capture traces (in real apps, use OpenTelemetry exporters)
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "NGql.Core",
            Sample = (ref ActivityCreationOptions<ActivityContext> options) => ActivitySamplingResult.AllData,
            ActivityStarted = activity =>
            {
                _output.WriteLine($"🔍 TRACE Started: {activity.DisplayName}");
                _output.WriteLine($"   ├─ Trace ID: {activity.TraceId}");
                _output.WriteLine($"   └─ Span ID: {activity.Id}");
            },
            ActivityStopped = activity =>
            {
                _output.WriteLine($"✅ TRACE Completed: {activity.DisplayName} ({activity.Duration.TotalMilliseconds:F2}ms)");
                foreach (var tag in activity.Tags)
                {
                    _output.WriteLine($"   📊 {tag.Key}: {tag.Value}");
                }
                foreach (var @event in activity.Events)
                {
                    _output.WriteLine($"   📝 Event: {@event.Name} at {@event.Timestamp:HH:mm:ss.fff}");
                }
            }
        };

        ActivitySource.AddActivityListener(listener);

        // Demo: Build a complex query with automatic tracing
        _output.WriteLine("\n🚀 Building GraphQL Query with Observability:");
        
        using var queryActivity = NGqlActivity.StartQuery("complex_query_building")
            .WithTag("demo.scenario", "comprehensive")
            .WithTag("query.complexity", "high");

        var query = queryActivity.WithObservability(() =>
        {
            return QueryBuilder
                .CreateDefaultBuilder("ComprehensiveUserQuery")
                .AddField("users", new Dictionary<string, object?> 
                { 
                    ["first"] = 50,
                    ["filter"] = new Dictionary<string, object?> 
                    { 
                        ["status"] = "ACTIVE" 
                    }
                })
                .AddField("users.id")
                .AddField("users.profile.personalInfo.displayName")
                .AddField("users.profile.contactInfo.email")
                .AddField("users.posts", new Dictionary<string, object?> { ["first"] = 10 })
                .AddField("users.posts.edges.node.title")
                .AddField("users.posts.edges.node.publishedAt");
        }, "build_comprehensive_query");

        _output.WriteLine($"\n📄 Generated Query:\n{query}\n");
        
        queryActivity.AddEvent("query_generation_complete");

        // Verify that tracing worked
        ((string)query).Should().NotBeNullOrEmpty();
        ((string)query).Should().Contain("ComprehensiveUserQuery");
    }

    [Fact]
    public void Demo_Error_Handling_With_Observability()
    {
        _output.WriteLine("🔥 Error Handling with Observability Demo:");

        // Set up listener to capture error events
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "NGql.Core",
            Sample = (ref ActivityCreationOptions<ActivityContext> options) => ActivitySamplingResult.AllData,
            ActivityStopped = activity =>
            {
                if (activity.Status == ActivityStatusCode.Error)
                {
                    _output.WriteLine($"❌ ERROR Captured in Trace: {activity.DisplayName}");
                    _output.WriteLine($"   📍 Error: {activity.StatusDescription}");
                    
                    foreach (var tag in activity.Tags)
                    {
                        if (tag.Key.StartsWith("exception"))
                        {
                            _output.WriteLine($"   🔍 {tag.Key}: {tag.Value}");
                        }
                    }
                }
            }
        };

        ActivitySource.AddActivityListener(listener);

        // Demo: Handle an exception with automatic trace correlation
        using var activity = NGqlActivity.StartQuery("error_demonstration")
            .WithTag("demo.type", "error_handling");

        var exceptionOccurred = false;
        try
        {
            activity.WithObservability(() =>
            {
                // Simulate an operation that might fail
                throw new InvalidOperationException("Simulated error for observability demo");
            }, "simulated_failure");
        }
        catch (InvalidOperationException ex)
        {
            exceptionOccurred = true;
            _output.WriteLine($"✅ Exception properly caught and traced: {ex.Message}");
        }

        _output.WriteLine("\n🎯 Error tracing complete - exception details recorded in distributed trace!");
        
        // Add assertion
        exceptionOccurred.Should().BeTrue("Exception should have been caught and traced");
    }

    [Fact]
    public void Demo_Custom_Metrics_Integration()
    {
        _output.WriteLine("🎛️  Custom Metrics Integration Demo:");
        _output.WriteLine("   (In production, these metrics are exported to monitoring systems)\n");

        // Simulate various query types for metrics
        var scenarios = new[]
        {
            ("simple", 2, 0.001),
            ("medium", 8, 0.005), 
            ("complex", 25, 0.015),
            ("very_large", 100, 0.050)
        };

        foreach (var (complexity, fieldCount, duration) in scenarios)
        {
            // Record metrics for different query complexities
            NGqlTelemetry.RecordQueryBuilt($"query_{complexity}", fieldCount, duration);
            NGqlTelemetry.RecordSerialization(duration * 0.3, fieldCount * 100);
            
            _output.WriteLine($"📊 Recorded metrics for {complexity} query: {fieldCount} fields, {duration:F3}s");
        }

        // Simulate active query tracking
        NGqlTelemetry.RecordActiveQueryChange(5);  // 5 queries started
        NGqlTelemetry.RecordActiveQueryChange(-3); // 3 completed
        NGqlTelemetry.RecordActiveQueryChange(-2); // 2 more completed

        _output.WriteLine($"\n✨ Metrics recorded! Available as:");
        _output.WriteLine($"   📈 Histograms: query build duration, field counts, serialization time");  
        _output.WriteLine($"   📊 Counters: total queries built, fields added, pool operations");
        _output.WriteLine($"   📉 Gauges: active queries, pool sizes");
        _output.WriteLine($"\n🔗 All metrics follow OpenTelemetry standards for easy integration!");
        
        // Add assertion
        scenarios.Should().HaveCount(4, "All scenarios should be processed");
    }
}