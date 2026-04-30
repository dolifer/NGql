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
    [Fact]
    public void StartQuery_NoListener_ProducesInactiveActivity()
    {
        using var activity = NGqlActivity.StartQuery("no_listener_query");

        activity.IsRecording.Should().BeFalse();
        activity.Id.Should().BeNull();
        activity.TraceId.Should().BeNull();
    }

    [Fact]
    public void StartField_NoListener_ProducesInactiveActivity()
    {
        using var activity = NGqlActivity.StartField("no_listener_field");

        activity.IsRecording.Should().BeFalse();
    }

    [Fact]
    public void StartPooling_NoListener_ProducesInactiveActivity()
    {
        using var activity = NGqlActivity.StartPooling("pool", "operation");

        activity.IsRecording.Should().BeFalse();
    }

    [Fact]
    public void Chaining_NoListener_AllMethodsAreNoOps()
    {
        // Every method below internally invokes `_activity?.X`. With no listener,
        // _activity is null and the null-conditional short-circuit branch is taken.
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
    public void WithObservability_NoListener_StillExecutesAction()
    {
        var executed = false;

        using var activity = NGqlActivity.StartQuery("obs_no_listener_action");
        activity.WithObservability(() => executed = true, "op");

        executed.Should().BeTrue();
    }

    [Fact]
    public void WithObservability_NoListener_ReturnsFuncResult()
    {
        using var activity = NGqlActivity.StartQuery("obs_no_listener_func");

        var result = activity.WithObservability(() => 42, "op");

        result.Should().Be(42);
    }

    [Fact]
    public void WithObservability_NoListener_PropagatesActionException()
    {
        bool caught = false;
        try
        {
            using var activity = NGqlActivity.StartQuery("obs_no_listener_throw_action");
            activity.WithObservability(
                () => throw new InvalidOperationException("boom"),
                "op");
        }
        catch (InvalidOperationException ex)
        {
            caught = true;
            ex.Message.Should().Be("boom");
        }
        caught.Should().BeTrue();
    }

    [Fact]
    public void WithObservability_NoListener_PropagatesFuncException()
    {
        bool caught = false;
        try
        {
            using var activity = NGqlActivity.StartQuery("obs_no_listener_throw_func");
            _ = activity.WithObservability<int>(
                () => throw new ArgumentException("nope"),
                "op");
        }
        catch (ArgumentException ex)
        {
            caught = true;
            ex.Message.Should().Be("nope");
        }
        caught.Should().BeTrue();
    }
}
