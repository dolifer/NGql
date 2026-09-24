using System;
using System.Collections.Generic;
using FluentAssertions;
using NGql.Core.Extensions;
using Xunit;

namespace NGql.Core.Tests.Issues;

/// <summary>
/// Variable extraction created its cycle-detection set for every dictionary or list argument.
/// A cycle needs a nested container, so the set is now created on the first descent into one.
/// </summary>
public class VariableExtractionAllocationTests
{
    [Fact]
    public void ExtractVariablesFromValue_FlatDictionary_DoesNotAllocateVisitedSet()
    {
        var arguments = new Dictionary<string, object?> { ["first"] = 10, ["after"] = "abc", ["id"] = new Variable("$id", "ID") };
        var variables = new SortedSet<Variable>();
        Helpers.ExtractVariablesFromValue(arguments, variables);

        var before = GC.GetAllocatedBytesForCurrentThread();
        Helpers.ExtractVariablesFromValue(arguments, variables);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        // Only the boxed values enumerator remains; a HashSet with its buckets costs over 200 B.
        allocated.Should().BeLessThan(100);
        variables.Should().ContainSingle();
    }

    [Fact]
    public void ExtractVariablesFromValue_IndirectCycle_TerminatesAndFindsVariables()
    {
        var outer = new Dictionary<string, object?> { ["id"] = new Variable("$id", "ID") };
        var inner = new List<object?> { outer, new Variable("$after", "String") };
        outer["items"] = inner;
        var variables = new SortedSet<Variable>();

        Helpers.ExtractVariablesFromValue(outer, variables);

        variables.Should().HaveCount(2);
    }
}
