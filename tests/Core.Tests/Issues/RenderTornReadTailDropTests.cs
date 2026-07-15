using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using NGql.Core.Abstractions;
using NGql.Core.Builders;
using Xunit;

namespace NGql.Core.Tests.Issues;

/// <summary>
/// Regression: <c>QueryTextBuilder.BuildFieldDefinitions(FieldChildren)</c> used to read
/// <c>children.Count</c> and <c>children.AsSpan()</c> as two INDEPENDENT volatile snapshots.
/// <see cref="FieldChildren"/> is a lock-free-read concurrent structure: a concurrent append
/// landing between the two reads yields a span longer than the rented count, which downstream
/// rendering, clearing and array return all measured against the stale count — silently dropping
/// the tail field(s) (and, on a bucket-boundary crossing, throwing an intermittent
/// <see cref="ArgumentException"/>). The fix takes the span once and derives the count from
/// <c>span.Length</c>, so every field the reader observes is rendered.
///
/// The invariant test is the primary guard (it deterministically catches a systematic tail-drop).
/// The concurrency test is a best-effort probe: it drives the exact torn-read window and asserts
/// invariants that hold under ANY interleaving (no exception; every rendered field is a real
/// appended field). It is deliberately non-flaky — it never asserts an exact observed count.
/// </summary>
public class RenderTornReadTailDropTests
{
    [Fact]
    public void Render_NestedNodeWithManyChildren_EmitsEveryChild()
    {
        // Arrange — a parent node whose children render through the FieldChildren path.
        // Use enough children to cross the FieldChildren index threshold (16) and a couple of
        // internal growth boundaries, so a stale-count tail-drop would be visible.
        var childNames = Enumerable.Range(0, 40).Select(i => $"field{i:D2}").ToArray();
        var builder = QueryBuilder.CreateDefaultBuilder("GetUser")
            .AddField("user", subFields: childNames);

        // Act
        var rendered = builder.ToString();

        // Assert — every declared child appears in the output.
        foreach (var name in childNames)
        {
            rendered.Should().Contain(name);
        }
    }

    [Fact]
    public void Render_ConcurrentAppendWhileRendering_NeverThrowsAndNeverCorrupts()
    {
        // Arrange — seed a parent node and grab its concurrent child collection directly so we can
        // append on one thread while another thread renders. Internals are visible to Core.Tests.
        var builder = QueryBuilder.CreateDefaultBuilder("Race")
            .AddField("user", subFields: new[] { "seed" });

        var userChildren = builder.Definition.Fields["user"]._children;
        userChildren.Should().NotBeNull("the seeded node must own a FieldChildren backing store");

        const int appendCount = 500;
        var validNames = new HashSet<string> { "seed" };
        for (int i = 0; i < appendCount; i++) validNames.Add($"gen{i:D3}");

        Exception? failure = null;
        var start = new ManualResetEventSlim(false);

        var appender = new Thread(() =>
        {
            start.Wait();
            for (int i = 0; i < appendCount; i++)
            {
                userChildren!.Append(new FieldDefinition($"gen{i:D3}"));
                // Tiny yield keeps the two threads interleaving instead of one finishing first.
                if ((i & 0x1F) == 0) Thread.Yield();
            }
        });

        var renderer = new Thread(() =>
        {
            start.Wait();
            try
            {
                for (int i = 0; i < 5_000; i++)
                {
                    var output = builder.ToString();

                    // Invariant that holds under ANY interleaving: every child token the renderer
                    // emitted must correspond to a field that was actually appended. A torn read that
                    // over-copied into a short-rented buffer would surface as an exception (caught
                    // below) or as stale/garbage slots — this guards the latter without asserting an
                    // exact count (which would be racy).
                    foreach (var token in ExtractUserChildTokens(output))
                    {
                        if (!validNames.Contains(token))
                        {
                            throw new Xunit.Sdk.XunitException(
                                $"Rendered an unexpected child token '{token}' — indicates a corrupt/torn snapshot.");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });

        // Act
        appender.Start();
        renderer.Start();
        start.Set();
        appender.Join();
        renderer.Join();

        // Assert
        failure.Should().BeNull("render must never throw or emit a corrupt field under concurrent append");

        // After both threads finish, a final render must contain every appended field (no permanent
        // tail-drop once mutation has quiesced).
        var finalRender = builder.ToString();
        foreach (var name in validNames)
        {
            finalRender.Should().Contain(name);
        }
    }

    /// <summary>
    /// Pulls the leaf child tokens (the seed plus <c>genNNN</c> names) out of the rendered query.
    /// Intentionally permissive: it only needs to recover the field identifiers so the test can
    /// check each one is a legitimately-appended name.
    /// </summary>
    private static IEnumerable<string> ExtractUserChildTokens(string rendered)
    {
        foreach (var rawLine in rendered.Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.Length == 0) continue;
            if (line is "{" or "}" or "user{") continue;
            if (line.StartsWith("query", StringComparison.Ordinal)) continue;
            if (line.StartsWith("user", StringComparison.Ordinal)) continue;
            // Leaf field lines are a bare identifier; anything else (braces, the operation header)
            // was filtered above.
            yield return line;
        }
    }
}
