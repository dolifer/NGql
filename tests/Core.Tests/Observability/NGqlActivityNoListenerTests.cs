using System;
using System.Diagnostics;
using FluentAssertions;
using NGql.Core.Observability;
using Xunit;

namespace NGql.Core.Tests.Observability;

/// <summary>
/// Verifies the null-activity code paths of <see cref="NGqlActivity"/>: when no
/// <see cref="ActivityListener"/> is registered for the NGql.Core source, every
/// Start* helper returns an inactive activity whose underlying Activity is null.
/// All <c>_activity?.X</c> calls then take the null-conditional short-circuit.
/// Lives in the ObservabilityListener collection so other listener-registering
/// tests do not run in parallel and leak a listener into this fixture.
/// </summary>
[Collection("ObservabilityListener")]
public class NGqlActivityNoListenerTests
{
    [Theory]
    [InlineData("query")]
    [InlineData("field")]
    [InlineData("pooling")]
    public void Start_NoListener_ProducesInactiveActivity(string kind)
    {
        using var activity = kind switch
        {
            "query" => NGqlActivity.StartQuery("no_listener"),
            "field" => NGqlActivity.StartField("no_listener"),
            _ => NGqlActivity.StartPooling("pool", "operation"),
        };

        activity.IsRecording.Should().BeFalse();
        activity.Id.Should().BeNull();
        activity.TraceId.Should().BeNull();
    }

    [Fact]
    public void Chaining_NoListener_AllMethodsAreNoOps()
    {
        // Every chained method invokes `_activity?.X`. With no listener, _activity is null
        // and every null-conditional short-circuit branch is taken. This single test covers
        // all the With*/AddEvent overloads at once via the fluent chain.
        var act = () =>
        {
            using var activity = NGqlActivity.StartQuery("chain_no_listener")
                .WithQueryTags("name", 3)
                .WithFieldTags("path", true, false)
                .WithPoolingTags("pool", "hit", 1)
                .WithTag("custom", "value")
                .WithTag("custom-null", null)
                .WithStatus(ActivityStatusCode.Ok, "fine")
                .WithStatus(ActivityStatusCode.Error)
                .WithException(new InvalidOperationException("ex-msg"))
                .AddEvent("event1")
                .AddEvent("event2", DateTimeOffset.UtcNow)
                .AddEvent("event3", new ActivityTagsCollection { { "k", "v" } })
                .AddEvent("event4", new ActivityTagsCollection(), DateTimeOffset.UtcNow);
        };

        act.Should().NotThrow();
    }

    [Fact]
    public void WithObservability_NoListener_Action_ExecutesAndPropagatesException()
    {
        // Cover both overloads (Action / Func<T>) and both outcomes (success / throw).
        // A ref-struct activity can't be captured in a lambda, so each scenario invokes
        // the chained method directly and inspects the side effect.
        var executed = false;
        using (var ok = NGqlActivity.StartQuery("obs_no_listener_action_ok"))
        {
            ok.WithObservability(() => executed = true, "op");
        }
        executed.Should().BeTrue();

        bool caught = false;
        try
        {
            using var bad = NGqlActivity.StartQuery("obs_no_listener_action_throw");
            bad.WithObservability(() => throw new InvalidOperationException("boom"), "op");
        }
        catch (InvalidOperationException ex)
        {
            caught = true;
            ex.Message.Should().Be("boom");
        }
        caught.Should().BeTrue();
    }

    [Fact]
    public void WithObservability_NoListener_Func_ReturnsResultAndPropagatesException()
    {
        using (var ok = NGqlActivity.StartQuery("obs_no_listener_func_ok"))
        {
            ok.WithObservability(() => 42, "op").Should().Be(42);
        }

        bool caught = false;
        try
        {
            using var bad = NGqlActivity.StartQuery("obs_no_listener_func_throw");
            _ = bad.WithObservability<int>(() => throw new ArgumentException("nope"), "op");
        }
        catch (ArgumentException ex)
        {
            caught = true;
            ex.Message.Should().Be("nope");
        }
        caught.Should().BeTrue();
    }
}
