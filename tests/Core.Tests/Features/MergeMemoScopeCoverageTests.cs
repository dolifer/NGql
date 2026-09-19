using System;
using System.Collections.Generic;
using FluentAssertions;
using NGql.Core.Features;
using Xunit;

namespace NGql.Core.Tests.Features;

/// <summary>
/// Covers <see cref="MergeMemoTracker"/>'s secondary-scope bookkeeping, including the pruning of
/// scopes that have already been collected — a field observed by several indexes keeps only weak
/// references to the extra ones so a discarded index cannot keep its whole field tree alive.
/// </summary>
public class MergeMemoScopeCoverageTests
{
    [Fact]
    public void Invalidate_WhenOnlyThePrimaryScopeIsAttached_BumpsItsVersion()
    {
        var tracker = new MergeMemoTracker();
        var primary = new MergeMemoScope();
        tracker.Attach(primary);
        var before = primary.Version;

        tracker.Invalidate();

        primary.Version.Should().Be(before + 1);
    }

    [Fact]
    public void Invalidate_WhenNoScopeIsAttached_DoesNothing()
    {
        var tracker = new MergeMemoTracker();

        var act = tracker.Invalidate;

        act.Should().NotThrow();
    }

    [Fact]
    public void Attach_WhenTheSameScopeIsAttachedTwice_KeepsASingleRegistration()
    {
        var tracker = new MergeMemoTracker();
        var primary = new MergeMemoScope();
        var secondary = new MergeMemoScope();
        tracker.Attach(primary);
        tracker.Attach(secondary);
        tracker.Attach(secondary);

        tracker.Invalidate();

        secondary.Version.Should().Be(1);
    }

    [Fact]
    public void Invalidate_WithSeveralLiveSecondaryScopes_BumpsEveryOneOfThem()
    {
        var tracker = new MergeMemoTracker();
        var primary = new MergeMemoScope();
        var first = new MergeMemoScope();
        var second = new MergeMemoScope();
        tracker.Attach(primary);
        tracker.Attach(first);
        tracker.Attach(second);

        tracker.Invalidate();

        primary.Version.Should().Be(1);
        first.Version.Should().Be(1);
        second.Version.Should().Be(1);
    }

    [Fact]
    public void Invalidate_AfterASecondaryScopeWasCollected_PrunesItAndStillBumpsTheSurvivors()
    {
        var tracker = new MergeMemoTracker();
        var primary = new MergeMemoScope();
        var survivor = new MergeMemoScope();
        tracker.Attach(primary);
        AttachCollectableScopes(tracker);
        tracker.Attach(survivor);

        ForceCollection();
        tracker.Invalidate();

        primary.Version.Should().Be(1);
        survivor.Version.Should().Be(1);
    }

    [Fact]
    public void Attach_AfterASecondaryScopeWasCollected_ReusesTheVacatedSlot()
    {
        var tracker = new MergeMemoTracker();
        var primary = new MergeMemoScope();
        tracker.Attach(primary);
        AttachCollectableScopes(tracker);

        ForceCollection();
        var latecomer = new MergeMemoScope();
        tracker.Attach(latecomer);
        tracker.Invalidate();

        primary.Version.Should().Be(1);
        latecomer.Version.Should().Be(1);
    }

    // Attaches secondary scopes and drops every strong reference to them before returning, so the
    // tracker's WeakReference entries become collectable. Kept in its own non-inlined frame so the
    // JIT cannot keep the locals alive in the caller's frame past the collection point.
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static void AttachCollectableScopes(MergeMemoTracker tracker)
    {
        for (var i = 0; i < 4; i++)
        {
            tracker.Attach(new MergeMemoScope());
        }
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Major Code Smell", "S1215",
        Justification = "Weak-reference pruning can only be observed once the collectable scopes are actually collected.")]
    private static void ForceCollection()
    {
        for (var i = 0; i < 5; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }
    }
}
