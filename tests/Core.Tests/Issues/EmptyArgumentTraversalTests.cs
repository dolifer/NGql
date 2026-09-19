using System.Collections;
using System.Collections.Generic;
using FluentAssertions;
using NGql.Core.Extensions;
using Xunit;

namespace NGql.Core.Tests.Issues;

public class EmptyArgumentTraversalTests
{
    [Fact]
    public void EmptyList_IsRevisitedAfterMutationAndStillHandlesCycles()
    {
        var list = new ArrayList();
        var variables = new SortedSet<Variable>();
        Helpers.ExtractVariablesFromValue(list, variables);
        variables.Should().BeEmpty();

        list.Add(list);
        list.Add(new Variable("$id", "Int"));
        Helpers.ExtractVariablesFromValue(list, variables);
        variables.Should().ContainSingle().Which.Should().Be(new Variable("$id", "Int"));
    }

    [Fact]
    public void EmptyDictionary_IsRevisitedAfterMutationAndStillHandlesCycles()
    {
        var dictionary = new Dictionary<string, object>();
        var variables = new SortedSet<Variable>();
        Helpers.ExtractVariablesFromValue(dictionary, variables);
        variables.Should().BeEmpty();

        dictionary.Add("self", dictionary);
        dictionary.Add("id", new Variable("$id", "Int"));
        Helpers.ExtractVariablesFromValue(dictionary, variables);
        variables.Should().ContainSingle().Which.Should().Be(new Variable("$id", "Int"));
    }
}
