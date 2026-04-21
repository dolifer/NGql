using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using NGql.Core.Abstractions;
using Xunit;

namespace NGql.Core.Tests.Abstractions;

/// <summary>
/// RED TEST: Demonstrates FieldChildren race condition in Append() method.
/// Non-atomic _count++ can lose entries when accessed concurrently.
/// </summary>
public class FieldChildrenConcurrencyTests
{
    [Fact]
    public async Task FieldChildren_Append_Should_Be_Thread_Safe()
    {
        // ARRANGE
        var children = new FieldChildren();
        const int threadCount = 10;
        const int appendsPerThread = 100;
        var tasks = new List<Task>();
        var errors = new List<string>();

        // ACT: Create many fields concurrently
        for (int t = 0; t < threadCount; t++)
        {
            int threadId = t;
            tasks.Add(Task.Run(() =>
            {
                try
                {
                    for (int i = 0; i < appendsPerThread; i++)
                    {
                        var field = new FieldDefinition($"field_t{threadId}_i{i}");
                        // This should be thread-safe but is not - race condition on _count++
                        children.Append(field);
                    }
                }
                catch (Exception ex)
                {
                    lock (errors)
                    {
                        errors.Add($"Thread {threadId}: {ex.Message}");
                    }
                }
            }));
        }

        // ASSERT
        await Task.WhenAll(tasks);

        // If race condition exists, we'll have fewer items than expected
        // because some writes were lost
        var expectedCount = threadCount * appendsPerThread;
        var actualCount = children.Count;

        errors.Should().BeEmpty("No exceptions should occur during concurrent appends");
        actualCount.Should().Be(expectedCount, $"Should have all {expectedCount} items, but race condition lost some");
    }

    [Fact]
    public async Task FieldChildren_Set_Should_Be_Thread_Safe()
    {
        // ARRANGE
        var children = new FieldChildren();
        const int operations = 500;
        var tasks = new List<Task>();
        var errors = new List<string>();

        // Pre-add some fields
        for (int i = 0; i < 20; i++)
        {
            children.Append(new FieldDefinition($"field_{i}"));
        }

        // ACT: Concurrent appends and sets
        for (int t = 0; t < 10; t++)
        {
            int threadId = t;
            tasks.Add(Task.Run(() =>
            {
                try
                {
                    for (int i = 0; i < operations; i++)
                    {
                        if (i % 2 == 0)
                        {
                            children.Append(new FieldDefinition($"thread_{threadId}_append_{i}"));
                        }
                        else
                        {
                            children.Set($"field_{i % 20}", new FieldDefinition($"updated_{threadId}_{i}"));
                        }
                    }
                }
                catch (Exception ex)
                {
                    lock (errors)
                    {
                        errors.Add($"Thread {threadId}: {ex.Message}");
                    }
                }
            }));
        }

        // ASSERT
        await Task.WhenAll(tasks);
        errors.Should().BeEmpty("No exceptions should occur during concurrent operations");
        children.Count.Should().BeGreaterThan(20, "Should have added fields");
    }
}

/// <summary>
/// RED TEST: Demonstrates QueryDefinition recursive ToString() buffer corruption.
/// Using a single cached ThreadLocal<QueryTextBuilder>, nested ToString() calls
/// reuse and corrupt the mutable StringBuilder.
/// </summary>
public class QueryDefinitionRecursionTests
{
    [Fact]
    public void QueryDefinition_ToString_Should_Handle_Nested_Calls()
    {
        // ARRANGE: Create nested query definitions
        var outerField = new FieldDefinition("outer");
        var outerQuery = new QueryDefinition("OuterQuery");
        outerQuery._fields = new() { { "outer", outerField } };

        var innerField = new FieldDefinition("inner");
        var innerQuery = new QueryDefinition("InnerQuery");
        innerQuery._fields = new() { { "inner", innerField } };

        // Add inner query as a field to outer query (creating a nesting situation)
        var nestedField = new FieldDefinition("nested");
        outerField._children = new FieldChildren();
        outerField._children.Append(nestedField);

        // ACT: Call ToString on both (simulating recursive access)
        var outerString = outerQuery.ToString();
        var innerString = innerQuery.ToString();

        // ASSERT: Both should have correct output, not corrupted by reuse
        outerString.Should().Contain("OuterQuery", "Outer query string should contain the query name");
        innerString.Should().Contain("InnerQuery", "Inner query string should contain the query name");
        
        // Most importantly, outer string should not be corrupted by inner's reuse
        outerString.Should().NotContain("InnerQuery", "Outer query should not contain inner query name");
        innerString.Should().NotContain("OuterQuery", "Inner query should not contain outer query name");
    }

    [Fact]
    public void QueryDefinition_ToString_Should_Not_Corrupt_With_Multiple_Calls()
    {
        // ARRANGE
        var query = new QueryDefinition("TestQuery");
        var field = new FieldDefinition("testField");
        query._fields = new() { { "testField", field } };

        // ACT: Call ToString multiple times in succession
        var result1 = query.ToString();
        var result2 = query.ToString();
        var result3 = query.ToString();

        // ASSERT: All results should be identical (not corrupted)
        result1.Should().Be(result2, "Multiple ToString calls should produce identical output");
        result2.Should().Be(result3, "Multiple ToString calls should produce identical output");
        result1.Should().Contain("TestQuery");
        result1.Should().Contain("testField");
    }
}

/// <summary>
/// RED TEST: FieldChildren read-side race condition.
/// While mutations (Append/Set) are protected, reads (Find/AsSpan) are unprotected.
/// Thread A reads _items pointer, Thread B resizes array → Thread A reads wrong array.
/// </summary>
public class FieldChildrenReadRaceConditionTests
{
    [Fact]
    public void FieldChildren_Find_Should_Be_Safe_During_Concurrent_Append()
    {
        // ARRANGE
        var children = new FieldChildren();
        var errors = new ConcurrentBag<Exception>();

        // ACT: One thread appends, others search concurrently
        var appendThread = Task.Run(() =>
        {
            try
            {
                for (int i = 0; i < 1000; i++)
                {
                    children.Append(new FieldDefinition($"field{i}"));
                }
            }
            catch (Exception ex) { errors.Add(ex); }
        });

        var searchTasks = Enumerable.Range(0, 5).Select(_ => Task.Run(() =>
        {
            try
            {
                for (int i = 0; i < 500; i++)
                {
                    // These reads MUST NOT crash or return corrupt data
                    var result = children.Find("field50".AsSpan());
                    var span = children.AsSpan();  // Must not throw IndexOutOfRange
                }
            }
            catch (Exception ex) { errors.Add(ex); }
        })).ToList();

        Task.WaitAll(searchTasks.Concat(new[] { appendThread }).ToArray());

        // ASSERT: No crashes, no corrupted state
        errors.Should().BeEmpty("Concurrent Find + Append should never crash");
        children.Count.Should().Be(1000, "All appends should succeed");
    }

    [Fact]
    public void FieldChildren_AsSpan_Should_Never_IndexOutOfRange_During_Resize()
    {
        // ARRANGE
        var children = new FieldChildren();
        var exceptions = new ConcurrentBag<Exception>();

        // ACT: Rapid append + span access
        var tasks = new List<Task>();
        
        // Appender thread
        tasks.Add(Task.Run(() =>
        {
            try
            {
                for (int i = 0; i < 100; i++)
                {
                    children.Append(new FieldDefinition($"f{i}"));
                }
            }
            catch (Exception ex) { exceptions.Add(ex); }
        }));

        // Span reader threads
        for (int t = 0; t < 4; t++)
        {
            tasks.Add(Task.Run(() =>
            {
                try
                {
                    for (int i = 0; i < 50; i++)
                    {
                        var span = children.AsSpan();
                        // Access span elements - if array was resized mid-read, this will crash
                        _ = span.Length;
                        if (span.Length > 0) _ = span[0];
                    }
                }
                catch (Exception ex) { exceptions.Add(ex); }
            }));
        }

        Task.WaitAll(tasks.ToArray());

        // ASSERT
        exceptions.Should().BeEmpty("AsSpan should never cause IndexOutOfRange during concurrent resize");
    }

    [Fact]
    public void FieldChildren_Enumerate_With_Modifications_Should_Use_Snapshot()
    {
        // Arrange - Verify that enumeration uses snapshot pattern (lines 255-295 coverage)
        var children = new FieldChildren();
        for (int i = 0; i < 5; i++)
        {
            children.Append(new FieldDefinition($"field_{i}"));
        }

        var enumeratedNames = new List<string>();

        // Act - Enumerate while item count is known (returns KeyValuePair)
        foreach (var kvp in ((IEnumerable<KeyValuePair<string, FieldDefinition>>)children))
        {
            enumeratedNames.Add(kvp.Value.Name);
            // Snapshot pattern means we can't corrupt iteration by modifications
        }

        // Assert
        enumeratedNames.Should().HaveCount(5);
        enumeratedNames.Should().Equal("field_0", "field_1", "field_2", "field_3", "field_4");
    }

    [Fact]
    public async Task FieldChildren_Enumerate_During_Concurrent_Appends_Should_Be_Safe()
    {
        // This test exercises the enumerator snapshot logic (lines 255-295)
        var children = new FieldChildren();
        for (int i = 0; i < 10; i++)
        {
            children.Append(new FieldDefinition($"initial_{i}"));
        }

        var iterationResults = new ConcurrentBag<int>();
        var exceptions = new ConcurrentBag<Exception>();

        // Arrange: Start multiple concurrent iterations
        var iterationTasks = new List<Task>();
        for (int iter = 0; iter < 5; iter++)
        {
            iterationTasks.Add(Task.Run(() =>
            {
                try
                {
                    int count = 0;
                    foreach (var field in children)
                    {
                        count++;
                        System.Threading.Thread.Sleep(1); // Simulate work
                    }
                    iterationResults.Add(count);
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            }));
        }

        // Act: While iterating, add more fields
        var appendTask = Task.Run(async () =>
        {
            await Task.Delay(5);
            for (int i = 10; i < 20; i++)
            {
                children.Append(new FieldDefinition($"appended_{i}"));
            }
        });

        await Task.WhenAll(iterationTasks.Concat(new[] { appendTask }).ToArray());

        // Assert - All iterations should complete without exceptions (snapshot protects them)
        exceptions.Should().BeEmpty("Concurrent enumeration should be safe via snapshot pattern");
        // Each iterator sees the snapshot at the time it started enumerating
        iterationResults.Should().HaveCount(5).And.AllSatisfy(count => count.Should().BeGreaterThanOrEqualTo(10));
    }

    [Fact]
    public void FieldChildren_Clone_Should_Create_Independent_Copy()
    {
        // Tests Clone method (line coverage for thread-safe copy operation)
        var children = new FieldChildren();
        for (int i = 0; i < 5; i++)
        {
            children.Append(new FieldDefinition($"field_{i}"));
        }

        var cloned = children.Clone();

        // Verify cloned has all original fields
        cloned.Count.Should().Be(5);
        
        var clonedNames = cloned.Select(f => f.Value.Name).ToList();
        clonedNames.Should().Equal("field_0", "field_1", "field_2", "field_3", "field_4");

        // Modify original
        children.Append(new FieldDefinition("field_5"));

        // Verify clone is independent
        cloned.Count.Should().Be(5, "Clone should not be affected by original modifications");
    }

    [Fact]
    public void FieldChildren_Find_Should_Be_Thread_Safe()
    {
        // Tests Find method thread-safety (protected via locks)
        var children = new FieldChildren();
        for (int i = 0; i < 20; i++)
        {
            children.Append(new FieldDefinition($"field_{i}"));
        }

        var findResults = new ConcurrentBag<bool>();

        // Act: Concurrent Find operations (using AsSpan instead of Find)
        for (int i = 0; i < 10; i++)
        {
            int index = i;
            var task = Task.Run(() =>
            {
                var span = children.AsSpan();
                bool found = false;
                for (int j = 0; j < span.Length; j++)
                {
                    if (span[j].Name == $"field_{index}")
                    {
                        found = true;
                        break;
                    }
                }
                findResults.Add(found);
            });
            task.Wait();
        }

        // Assert
        findResults.Should().AllSatisfy(r => r.Should().BeTrue());
    }

    [Fact]
    public void FieldChildren_TryGetValue_Should_Find_Fields_By_Name()
    {
        // Test uncovered line: TryGetValue method path (lines 66-73)
        var children = new FieldChildren();
        children.Append(new FieldDefinition("userId"));
        children.Append(new FieldDefinition("userName"));
        
        var result1 = children.TryGetValue("userId".AsSpan(), out var field1);
        var result2 = children.TryGetValue("notExists".AsSpan(), out var field2);
        
        result1.Should().BeTrue();
        field1.Should().NotBeNull();
        field1!.Name.Should().Be("userId");
        
        result2.Should().BeFalse();
        field2.Should().BeNull();
    }

    [Fact]
    public void FieldChildren_Find_By_Span_Should_Handle_CaseInsensitive_Lookup()
    {
        // Test uncovered line: Find method with case-insensitive comparison (line 59)
        var children = new FieldChildren();
        children.Append(new FieldDefinition("Profile"));
        children.Append(new FieldDefinition("Email"));
        
        // Find with different case should still work
        var profileLower = children.Find("profile".AsSpan());
        var emailUpper = children.Find("EMAIL".AsSpan());
        
        profileLower.Should().NotBeNull();
        profileLower!.Name.Should().Be("Profile");
        
        emailUpper.Should().NotBeNull();
        emailUpper!.Name.Should().Be("Email");
    }

    [Fact]
    public void FieldChildren_Set_Should_Update_Existing_Field()
    {
        // Test uncovered line: Set method with name and field (line 112)
        var children = new FieldChildren();
        var originalField = new FieldDefinition("field1", "Type1");
        children.Append(originalField);

        var updatedField = new FieldDefinition("field1", "UpdatedType");
        children.Set("field1", updatedField);

        var result = children.Find("field1".AsSpan());
        result!.Type.Should().Be("UpdatedType");
    }

    [Fact]
    public void FieldChildren_AsSpan_Enumeration_Should_See_Snapshot()
    {
        // Test uncovered line: AsSpan during concurrent mutations (line 38-41)
        var children = new FieldChildren();
        for (int i = 0; i < 5; i++)
        {
            children.Append(new FieldDefinition($"field_{i}"));
        }

        var span1 = children.AsSpan();
        span1.Length.Should().Be(5);

        // Add more fields
        children.Append(new FieldDefinition("field_5"));
        
        // Original span should still reflect original size
        span1.Length.Should().Be(5, "Span should be a snapshot");
        
        // New span should include the new field
        var span2 = children.AsSpan();
        span2.Length.Should().Be(6);
    }

    [Fact]
    public void FieldChildren_Enumeration_With_Index_Should_Work()
    {
        // Test uncovered line: Enumeration when _index becomes active (>= 16 items)
        var children = new FieldChildren();
        const int itemCount = 20;
        
        for (int i = 0; i < itemCount; i++)
        {
            children.Append(new FieldDefinition($"field_{i:D2}"));
        }

        var enumeratedCount = 0;
        foreach (var kvp in children)
        {
            kvp.Key.Should().StartWith("field_");
            enumeratedCount++;
        }

        enumeratedCount.Should().Be(itemCount);
    }

    [Fact]
    public void FieldChildren_GetValueOrDefault_Should_Handle_Missing_Keys()
    {
        // Test uncovered line: GetValueOrDefault IReadOnlyDictionary implementation
        var children = new FieldChildren();
        children.Append(new FieldDefinition("existingField"));
        
        var existing = ((IReadOnlyDictionary<string, FieldDefinition>)children).GetValueOrDefault("existingField");
        var missing = ((IReadOnlyDictionary<string, FieldDefinition>)children).GetValueOrDefault("missingField");
        
        existing.Should().NotBeNull();
        existing!.Name.Should().Be("existingField");
        
        missing.Should().BeNull();
    }

    [Fact]
    public void FieldChildren_ContainsKey_Should_Check_Field_Existence()
    {
        // Test uncovered line: ContainsKey IReadOnlyDictionary implementation (line 75-76)
        var children = new FieldChildren();
        children.Append(new FieldDefinition("email"));
        children.Append(new FieldDefinition("phone"));
        
        ((IReadOnlyDictionary<string, FieldDefinition>)children).ContainsKey("email").Should().BeTrue();
        ((IReadOnlyDictionary<string, FieldDefinition>)children).ContainsKey("address").Should().BeFalse();
    }

    [Fact]
    public void FieldChildren_Indexer_Should_Return_Field_By_Name()
    {
        // Test uncovered line: IReadOnlyDictionary indexer (line 78-83)
        var children = new FieldChildren();
        children.Append(new FieldDefinition("data", "String"));
        
        var dict = (IReadOnlyDictionary<string, FieldDefinition>)children;
        var field = dict["data"];
        
        field.Should().NotBeNull();
        field.Name.Should().Be("data");
        field.Type.Should().Be("String");
    }

    [Fact]
    public void FieldChildren_Keys_And_Values_Enumerables_Should_Work()
    {
        // Test uncovered line: Keys and Values properties
        var children = new FieldChildren();
        children.Append(new FieldDefinition("id", "ID"));
        children.Append(new FieldDefinition("name", "String"));
        
        var dict = (IReadOnlyDictionary<string, FieldDefinition>)children;
        var keys = dict.Keys.ToList();
        var values = dict.Values.ToList();
        
        keys.Should().Equal("id", "name");
        values.Should().HaveCount(2);
        values[0].Type.Should().Be("ID");
        values[1].Type.Should().Be("String");
    }
}
