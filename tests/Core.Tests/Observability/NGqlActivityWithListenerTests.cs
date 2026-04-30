using System;
using System.Diagnostics;
using FluentAssertions;
using NGql.Core.Observability;
using Xunit;

namespace NGql.Core.Tests.Observability;

/// <summary>
/// Verifies the listener-active code paths of <see cref="NGqlActivity"/>: when an
/// <see cref="ActivityListener"/> is registered for the NGql.Core source, every
/// Start* helper returns an activity backed by a real <see cref="Activity"/>.
/// All <c>_activity?.X</c> calls then route to the underlying Activity.
/// Lives in the ObservabilityListener collection so other listener-registering
/// tests do not run in parallel and double-register a listener for this fixture.
/// </summary>
[Collection("ObservabilityListener")]
public class NGqlActivityWithListenerTests
{
    private static ActivityListener CreateAllDataListener() => new()
    {
        ShouldListenTo = _ => true,
        Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData
    };

    [Fact]
    public void StartQuery_WithListener_ProducesActiveActivityWithIdAndTraceId()
    {
        var listener = CreateAllDataListener();
        ActivitySource.AddActivityListener(listener);
        try
        {
            using var activity = NGqlActivity.StartQuery("with_listener_query");

            activity.IsRecording.Should().BeTrue();
            activity.Id.Should().NotBeNullOrEmpty();
            activity.TraceId.Should().NotBeNullOrEmpty();
        }
        finally
        {
            listener.Dispose();
        }
    }

    [Fact]
    public void Chaining_WithListener_RoutesAllMethodsToUnderlyingActivity()
    {
        // Single chained scenario covers WithStatus, WithException, AddEvent (all overloads)
        // — each method internally calls `_activity?.X`; with a registered listener, _activity
        // is non-null and every chained call routes to the real Activity.
        var listener = CreateAllDataListener();
        ActivitySource.AddActivityListener(listener);
        try
        {
            using var activity = NGqlActivity.StartQuery("with_listener_chain")
                .WithStatus(ActivityStatusCode.Ok)
                .WithStatus(ActivityStatusCode.Error, "failure")
                .WithException(new InvalidOperationException("boom"))
                .AddEvent("ev1")
                .AddEvent("ev2", DateTimeOffset.UtcNow)
                .AddEvent("ev3", new ActivityTagsCollection { { "k", "v" } })
                .AddEvent("ev4", new ActivityTagsCollection(), DateTimeOffset.UtcNow);

            activity.IsRecording.Should().BeTrue();
        }
        finally
        {
            listener.Dispose();
        }
    }
}
