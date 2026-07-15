using System;
using System.Text;
using FluentAssertions;
using NGql.Core.Builders;
using Xunit;

namespace NGql.Core.Tests.Issues;

/// <summary>
/// Regression tests for the field-sort tiebreaker in QueryTextBuilder.
///
/// The render-time field sort uses an unstable introsort (Array.Sort with a comparer). Before the
/// fix, the comparer keyed only on the effective name (Alias ?? Name, case-insensitive), so a run
/// of fields all sharing one effective key was ordered arbitrarily by introsort internals — the
/// order differed across runtimes and .NET versions once the tied run grew large enough (~17+),
/// which broke the deterministic-render guarantee and caused snapshot flakiness. The comparer now
/// falls back to ordinal Name then ordinal Alias, giving a total order from each field's own data.
/// </summary>
public class FieldSortTiebreakerTests
{
    private const int TiedFieldCount = 20;

    // Distinct child names that all collide on the same effective key: every child is aliased "a".
    private static QueryBuilder BuildAllTiedOnAliasA(int[] insertionOrder)
    {
        var builder = QueryBuilder.CreateDefaultBuilder("Tie");
        foreach (var i in insertionOrder)
        {
            builder.AddField($"parent.a:child{i:D2}");
        }
        return builder;
    }

    private static string ExpectedAllTiedOnAliasA()
    {
        var sb = new StringBuilder();
        sb.AppendLine("query Tie{");
        sb.AppendLine("    parent{");
        for (int i = 0; i < TiedFieldCount; i++)
        {
            sb.Append("        a:child").Append(i.ToString("D2")).AppendLine();
        }
        sb.AppendLine("    }");
        sb.Append("}");
        return sb.ToString();
    }

    [Fact]
    public void Render_FieldsCollidingOnEffectiveKey_ProducesDeterministicOrdinalOrder()
    {
        // Arrange — insert in reverse so a naive "keep insertion order" would surface as a failure.
        var reversed = new int[TiedFieldCount];
        for (int i = 0; i < TiedFieldCount; i++) reversed[i] = TiedFieldCount - 1 - i;
        var builder = BuildAllTiedOnAliasA(reversed);

        // Act
        var rendered = builder.ToString();

        // Assert — the all-tied run resolves to ordinal-by-name ascending, independent of insertion.
        rendered.Should().Be(ExpectedAllTiedOnAliasA());
    }

    [Fact]
    public void Render_FieldsCollidingOnEffectiveKey_IsByteIdenticalAcrossRepeatedRenders()
    {
        // Arrange
        var builder = BuildAllTiedOnAliasA(SequentialOrder());

        // Act
        var first = builder.ToString();
        var second = builder.ToString();
        var third = builder.ToString();

        // Assert — repeated renders of the same logical field set never diverge.
        second.Should().Be(first);
        third.Should().Be(first);
    }

    [Fact]
    public void Render_SameLogicalFieldSet_DifferentInsertionOrders_RenderIdentically()
    {
        // Arrange — same fields, three different insertion orders (sequential, reversed, shuffled).
        var sequential = BuildAllTiedOnAliasA(SequentialOrder()).ToString();

        var reversed = new int[TiedFieldCount];
        for (int i = 0; i < TiedFieldCount; i++) reversed[i] = TiedFieldCount - 1 - i;
        var reversedRender = BuildAllTiedOnAliasA(reversed).ToString();

        var shuffled = SequentialOrder();
        var rng = new Random(20260715);
        for (int i = shuffled.Length - 1; i > 0; i--)
        {
            var j = rng.Next(i + 1);
            (shuffled[i], shuffled[j]) = (shuffled[j], shuffled[i]);
        }
        var shuffledRender = BuildAllTiedOnAliasA(shuffled).ToString();

        // Assert — sort order is a total order derived from field data, not from insertion order.
        reversedRender.Should().Be(sequential);
        shuffledRender.Should().Be(sequential);
    }

    private static int[] SequentialOrder()
    {
        var order = new int[TiedFieldCount];
        for (int i = 0; i < TiedFieldCount; i++) order[i] = i;
        return order;
    }
}
