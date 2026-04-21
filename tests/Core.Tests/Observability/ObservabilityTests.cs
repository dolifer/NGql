using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Threading.Tasks;
using FluentAssertions;
using NGql.Core.Builders;
using NGql.Core.Observability;
using NGql.Core.Pooling;
using Xunit;

namespace NGql.Core.Tests.Observability;

public class ObservabilityTests
{
    [Fact]
    public void NGqlActivity_Should_Create_Scoped_Activities()
    {
        // Arrange & Act & Assert
        // Activity creation should not throw and should be properly scoped
        var action = () =>
        {
            using var activity = NGqlActivity.StartQuery("test_operation")
                .WithTag("test.key", "test.value");
            // Test that we can use the activity
            _ = activity.IsRecording;
        };
        
        action.Should().NotThrow();
    }

    [Fact]
    public void NGqlActivity_Should_Handle_Exceptions_Gracefully()
    {
        var action = () =>
        {
            using var activity = NGqlActivity.StartQuery("exception_test");
            activity.WithObservability(() =>
            {
                throw new InvalidOperationException("Test exception");
            }, "test_operation");
        };

        action.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ThreadLocalMemoryManager_Should_Record_Statistics_With_Observability()
    {
        // Reset stats for clean test
        ThreadLocalMemoryManager.ResetThreadStats();
        
        // Perform operations that will trigger observability
        for (int i = 0; i < 5; i++)
        {
            using var pooled = LockFreeArgumentsPool.GetPooled(new Dictionary<string, object?> { ["test"] = i });
            _ = pooled.Dictionary.Count;
        }
        
        var stats = ThreadLocalMemoryManager.GetThreadStats();
        
        // Should have recorded operations with observability
        var totalOperations = stats.ThreadLocalHits + stats.GlobalPoolHits + stats.Allocations;
        totalOperations.Should().BeGreaterThan(0);
    }

    [Fact]
    public void QueryBuilder_Should_Work_With_Activity_Context()
    {
        // Test that QueryBuilder operations work normally even with activity context
        var result = "";
        var action = () =>
        {
            using var activity = NGqlActivity.StartQuery("query_building_test")
                .WithTag("test.scenario", "integration");

            result = activity.WithObservability(() =>
            {
                return QueryBuilder
                    .CreateDefaultBuilder("ObservabilityTest")
                    .AddField("users", new Dictionary<string, object?> { ["limit"] = 10 })
                    .AddField("users.name")
                    .AddField("users.email")
                    .ToString();
            }, "build_query");
        };

        action.Should().NotThrow();
        result.Should().NotBeNullOrEmpty();
        result.Should().Contain("ObservabilityTest");
    }

    [Fact]
    public void PoolingObservability_Should_Track_Cache_Performance()
    {
        // Reset and warm up caches
        ThreadLocalMemoryManager.ResetThreadStats();
        ThreadLocalMemoryManager.WarmupThreadLocalCaches();

        // Perform operations to generate telemetry
        const int operationCount = 10;
        for (int i = 0; i < operationCount; i++)
        {
            using var sb = LockFreeStringBuilderPool.GetPooled();
            sb.StringBuilder.Append($"test{i}");
            _ = sb.StringBuilder.ToString();
        }

        // Report efficiency (this will generate observability data)
        ThreadLocalMemoryManager.ReportPoolEfficiency("stringbuilder");

        // Verify statistics were collected
        var stats = ThreadLocalMemoryManager.GetThreadStats();
        stats.ThreadLocalHits.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Observability_Should_Work_In_Concurrent_Scenarios()
    {
        const int threadCount = 4;
        const int operationsPerThread = 25;
        var tasks = new Task[threadCount];

        for (int t = 0; t < threadCount; t++)
        {
            int threadId = t;
            tasks[t] = Task.Run(() =>
            {
                using var activity = NGqlActivity.StartQuery($"concurrent_test_thread_{threadId}");
                
                activity.WithObservability(() =>
                {
                    for (int i = 0; i < operationsPerThread; i++)
                    {
                        var query = QueryBuilder
                            .CreateDefaultBuilder($"ConcurrentTest_{threadId}_{i}")
                            .AddField("data.field1")
                            .AddField("data.field2");
                        
                        _ = query.ToString();
                    }
                }, $"thread_{threadId}_operations");
            });
        }

        await Task.WhenAll(tasks);
        
        // Test should complete without exceptions, indicating observability works in concurrent scenarios
        tasks.Should().AllSatisfy(task => task.IsCompletedSuccessfully.Should().BeTrue());
    }

    [Fact]
    public void Activity_Extensions_Should_Provide_Safe_Execution()
    {
        var result = "";
        var successAction = () =>
        {
            using var activity = NGqlActivity.StartQuery("safe_execution_test");
            result = activity.WithObservability(() => "success", "test_success");
        };
        
        successAction.Should().NotThrow();
        result.Should().Be("success");
        
        // Test exception handling
        var exceptionAction = () =>
        {
            using var activity = NGqlActivity.StartQuery("exception_execution_test");
            activity.WithObservability(() => 
            {
                throw new ArgumentException("Test error");
            }, "test_error");
        };
        
        exceptionAction.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void NGqlTelemetry_Should_Accept_Metric_Recording()
    {
        // Test that telemetry methods don't throw exceptions
        var recordingAction = () =>
        {
            NGqlTelemetry.RecordQueryBuilt("TestQuery", 5, 0.001);
            NGqlTelemetry.RecordFieldAdded("test.field", true, false);
            NGqlTelemetry.RecordSerialization(0.0005, 1024);
            NGqlTelemetry.RecordActiveQueryChange(1);
            NGqlTelemetry.RecordActiveQueryChange(-1);
        };

        recordingAction.Should().NotThrow();
    }

    #region NGqlTelemetry Comprehensive Tests

    [Fact]
    public void NGqlTelemetry_RecordQueryBuilt_NoFields_ShouldNotThrow()
    {
        // Arrange & Act & Assert
        var recordAction = () => NGqlTelemetry.RecordQueryBuilt("EmptyQuery", 0, 0.0001);
        recordAction.Should().NotThrow();
    }

    [Fact]
    public void NGqlTelemetry_RecordQueryBuilt_ManyFields_ShouldNotThrow()
    {
        // Arrange & Act & Assert
        var recordAction = () => NGqlTelemetry.RecordQueryBuilt("LargeQuery", 100, 0.5);
        recordAction.Should().NotThrow();
    }

    [Fact]
    public void NGqlTelemetry_RecordQueryBuilt_NullQueryName_ShouldNotThrow()
    {
        // Arrange & Act & Assert
        var recordAction = () => NGqlTelemetry.RecordQueryBuilt(null, 10, 0.01);
        recordAction.Should().NotThrow();
    }

    [Fact]
    public void NGqlTelemetry_RecordQueryBuilt_VariousDurations_ShouldNotThrow()
    {
        // Arrange & Act & Assert
        var recordAction = () =>
        {
            NGqlTelemetry.RecordQueryBuilt("FastQuery", 5, 0.0001);
            NGqlTelemetry.RecordQueryBuilt("SlowQuery", 50, 2.5);
            NGqlTelemetry.RecordQueryBuilt("MediumQuery", 25, 0.1);
        };
        recordAction.Should().NotThrow();
    }

    [Fact]
    public void NGqlTelemetry_RecordFieldAdded_SimpleField_ShouldNotThrow()
    {
        // Arrange & Act & Assert
        var recordAction = () => NGqlTelemetry.RecordFieldAdded("name", false, false);
        recordAction.Should().NotThrow();
    }

    [Fact]
    public void NGqlTelemetry_RecordFieldAdded_FieldWithArguments_ShouldNotThrow()
    {
        // Arrange & Act & Assert
        var recordAction = () => NGqlTelemetry.RecordFieldAdded("users", true, false);
        recordAction.Should().NotThrow();
    }

    [Fact]
    public void NGqlTelemetry_RecordFieldAdded_FieldWithMetadata_ShouldNotThrow()
    {
        // Arrange & Act & Assert
        var recordAction = () => NGqlTelemetry.RecordFieldAdded("profile", false, true);
        recordAction.Should().NotThrow();
    }

    [Fact]
    public void NGqlTelemetry_RecordFieldAdded_NestedField_ShouldNotThrow()
    {
        // Arrange & Act & Assert
        var recordAction = () => NGqlTelemetry.RecordFieldAdded("user.profile.address", true, true);
        recordAction.Should().NotThrow();
    }

    [Fact]
    public void NGqlTelemetry_RecordFieldAdded_DeepNestedField_ShouldNotThrow()
    {
        // Arrange & Act & Assert
        var recordAction = () => NGqlTelemetry.RecordFieldAdded("a.b.c.d.e.f.g", false, false);
        recordAction.Should().NotThrow();
    }

    [Fact]
    public void NGqlTelemetry_RecordSerialization_SmallOutput_ShouldNotThrow()
    {
        // Arrange & Act & Assert
        var recordAction = () => NGqlTelemetry.RecordSerialization(0.0001, 256); // Small < 1KB
        recordAction.Should().NotThrow();
    }

    [Fact]
    public void NGqlTelemetry_RecordSerialization_MediumOutput_ShouldNotThrow()
    {
        // Arrange & Act & Assert
        var recordAction = () => NGqlTelemetry.RecordSerialization(0.001, 5120); // Medium < 10KB
        recordAction.Should().NotThrow();
    }

    [Fact]
    public void NGqlTelemetry_RecordSerialization_LargeOutput_ShouldNotThrow()
    {
        // Arrange & Act & Assert
        var recordAction = () => NGqlTelemetry.RecordSerialization(0.01, 50000); // Large < 100KB
        recordAction.Should().NotThrow();
    }

    [Fact]
    public void NGqlTelemetry_RecordSerialization_VeryLargeOutput_ShouldNotThrow()
    {
        // Arrange & Act & Assert
        var recordAction = () => NGqlTelemetry.RecordSerialization(0.05, 500000); // Very large >= 100KB
        recordAction.Should().NotThrow();
    }

    [Fact]
    public void NGqlTelemetry_RecordSerialization_EdgeCaseSizes_ShouldNotThrow()
    {
        // Arrange & Act & Assert
        var recordAction = () =>
        {
            NGqlTelemetry.RecordSerialization(0.0001, 1023);     // Just under 1KB boundary
            NGqlTelemetry.RecordSerialization(0.0001, 1024);     // Exactly 1KB boundary
            NGqlTelemetry.RecordSerialization(0.001, 10240);     // Exactly 10KB boundary
            NGqlTelemetry.RecordSerialization(0.01, 102400);     // Exactly 100KB boundary
        };
        recordAction.Should().NotThrow();
    }

    [Fact]
    public void NGqlTelemetry_RecordActiveQueryChange_SingleIncrement_ShouldNotThrow()
    {
        // Arrange & Act & Assert
        var recordAction = () => NGqlTelemetry.RecordActiveQueryChange(1);
        recordAction.Should().NotThrow();
    }

    [Fact]
    public void NGqlTelemetry_RecordActiveQueryChange_SingleDecrement_ShouldNotThrow()
    {
        // Arrange & Act & Assert
        var recordAction = () => NGqlTelemetry.RecordActiveQueryChange(-1);
        recordAction.Should().NotThrow();
    }

    [Fact]
    public void NGqlTelemetry_RecordActiveQueryChange_MultipleChanges_ShouldNotThrow()
    {
        // Arrange & Act & Assert
        var recordAction = () =>
        {
            NGqlTelemetry.RecordActiveQueryChange(1);
            NGqlTelemetry.RecordActiveQueryChange(1);
            NGqlTelemetry.RecordActiveQueryChange(1);
            NGqlTelemetry.RecordActiveQueryChange(-2);
            NGqlTelemetry.RecordActiveQueryChange(-1);
        };
        recordAction.Should().NotThrow();
    }

    [Fact]
    public void NGqlTelemetry_RecordActiveQueryChange_LargeDeltas_ShouldNotThrow()
    {
        // Arrange & Act & Assert
        var recordAction = () =>
        {
            NGqlTelemetry.RecordActiveQueryChange(100);
            NGqlTelemetry.RecordActiveQueryChange(-50);
            NGqlTelemetry.RecordActiveQueryChange(1000);
        };
        recordAction.Should().NotThrow();
    }

    [Fact]
    public void NGqlTelemetry_RecordPoolOperation_StringBuilderPool_ShouldNotThrow()
    {
        // Arrange & Act & Assert
        var recordAction = () => NGqlTelemetry.RecordPoolOperation("stringbuilder", "get", "hit", 10);
        recordAction.Should().NotThrow();
    }

    [Fact]
    public void NGqlTelemetry_RecordPoolOperation_ArgumentsPool_ShouldNotThrow()
    {
        // Arrange & Act & Assert
        var recordAction = () => NGqlTelemetry.RecordPoolOperation("arguments", "return", "miss", 5);
        recordAction.Should().NotThrow();
    }

    [Fact]
    public void NGqlTelemetry_RecordPoolOperation_DifferentCacheLevels_ShouldNotThrow()
    {
        // Arrange & Act & Assert
        var recordAction = () =>
        {
            NGqlTelemetry.RecordPoolOperation("pool1", "get", "thread_local_hit", 20);
            NGqlTelemetry.RecordPoolOperation("pool2", "get", "global_hit", 15);
            NGqlTelemetry.RecordPoolOperation("pool3", "get", "miss", 0);
        };
        recordAction.Should().NotThrow();
    }

    [Fact]
    public void NGqlTelemetry_RecordPoolOperation_LargePoolSize_ShouldNotThrow()
    {
        // Arrange & Act & Assert
        var recordAction = () => NGqlTelemetry.RecordPoolOperation("largepool", "operation", "hit", 10000);
        recordAction.Should().NotThrow();
    }

    [Fact]
    public void NGqlTelemetry_CreateTimedScope_ShouldNotThrow()
    {
        // Arrange & Act & Assert
        var action = () =>
        {
            using var scope = NGqlTelemetry.CreateTimedScope("test_operation", default);
        };
        action.Should().NotThrow();
    }

    [Fact]
    public void NGqlTelemetry_CreateTimedScope_Serialize_ShouldNotThrow()
    {
        // Arrange & Act & Assert
        var action = () =>
        {
            using var scope = NGqlTelemetry.CreateTimedScope("ngql.serialize", default);
        };
        action.Should().NotThrow();
    }

    [Fact]
    public void NGqlTelemetry_CreateTimedScope_MultipleScopes_ShouldNotThrow()
    {
        // Arrange & Act & Assert
        var action = () =>
        {
            using (var scope1 = NGqlTelemetry.CreateTimedScope("op1", default))
            {
                using (var scope2 = NGqlTelemetry.CreateTimedScope("op2", default))
                {
                    // Nested scopes
                }
            }
        };
        action.Should().NotThrow();
    }

    [Fact]
    public void NGqlTelemetry_DisposeResources_ShouldNotThrow()
    {
        // Arrange & Act & Assert
        var action = () => NGqlTelemetry.DisposeResources();
        action.Should().NotThrow();
    }

    [Fact]
    public void NGqlTelemetry_RecordQueryBuilt_ZeroDuration_ShouldNotThrow()
    {
        // Arrange & Act & Assert
        var recordAction = () => NGqlTelemetry.RecordQueryBuilt("ZeroDurationQuery", 5, 0);
        recordAction.Should().NotThrow();
    }

    [Fact]
    public void NGqlTelemetry_RecordSerialization_ZeroOutput_ShouldNotThrow()
    {
        // Arrange & Act & Assert
        var recordAction = () => NGqlTelemetry.RecordSerialization(0, 0);
        recordAction.Should().NotThrow();
    }

    #endregion
}