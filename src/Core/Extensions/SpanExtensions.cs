using System.Runtime.CompilerServices;

namespace NGql.Core.Extensions;

internal static class SpanExtensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool HasSpaces(this ReadOnlySpan<char> span) => span.IndexOf(' ') != -1;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool HasColons(this ReadOnlySpan<char> span) => span.IndexOf(':') != -1;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool HasDots(this ReadOnlySpan<char> span) => span.IndexOf('.') != -1;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsSimpleField(this ReadOnlySpan<char> span) => !span.HasDots() && !span.HasSpaces() && !span.HasColons();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsDottedField(this ReadOnlySpan<char> span) => span.HasDots() && !span.HasSpaces() && !span.HasColons();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsComplexField(this ReadOnlySpan<char> span) => span.HasSpaces() || span.HasColons();
}
