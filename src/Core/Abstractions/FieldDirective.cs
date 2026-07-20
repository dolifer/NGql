using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using NGql.Core.Extensions;

namespace NGql.Core.Abstractions;

/// <summary>
///     Represents a GraphQL directive attached to a field — rendered as
///     <c>@Name</c> optionally followed by <c>(argName:value, …)</c> after the field's name and
///     arguments and before its selection set. Examples: <c>@include(if:$show)</c>,
///     <c>@skip(if:$hide)</c>, <c>@deprecated</c>.
/// </summary>
/// <remarks>
///     Directives are immutable value objects. A no-argument directive (<see cref="Arguments"/>
///     is <c>null</c>) carries no dictionary, so the common <c>@deprecated</c>-style case allocates
///     nothing beyond the record itself. Argument values reuse the same formatting as field
///     arguments, so <c>if:$x</c> and complex values render identically.
/// </remarks>
[SuppressMessage("Minor Code Smell", "S2292:Trivial properties should be auto-implemented")]
public sealed record FieldDirective
{
    /// <summary>
    /// Creates a directive with the given name and optional arguments.
    /// </summary>
    /// <param name="name">The directive name WITHOUT the leading <c>@</c> (e.g. <c>"include"</c>).
    /// Rendered with the <c>@</c> prefix.</param>
    /// <param name="arguments">Optional directive arguments; <c>null</c> for a no-argument directive.
    /// Argument keys are normalized through the same case-insensitive pipeline as field arguments:
    /// keys that collide under <see cref="StringComparer.OrdinalIgnoreCase"/> throw (rather than
    /// silently last-win), and the resulting dictionary is sorted for deterministic rendering.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is null or whitespace,
    /// or when <paramref name="arguments"/> contains keys colliding under OrdinalIgnoreCase.</exception>
    public FieldDirective(string name, IReadOnlyDictionary<string, object?>? arguments = null)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Directive name cannot be null or whitespace.", nameof(name));
        }

        Name = name;
        Arguments = NormalizeArguments(arguments);
    }

    // Route directive arguments through the same normalization field arguments use
    // (Helpers.SortArgumentValue over the dictionary): keys colliding under OrdinalIgnoreCase
    // fail fast instead of silently last-winning, nested values are normalized identically, and
    // the result is a sorted dictionary so rendering is deterministic — matching field-argument
    // behavior exactly.
    private static IReadOnlyDictionary<string, object?>? NormalizeArguments(IReadOnlyDictionary<string, object?>? arguments)
    {
        if (arguments is not { Count: > 0 })
        {
            return null;
        }

        var source = arguments as IDictionary<string, object?>
            ?? new Dictionary<string, object?>(arguments);
        return (IReadOnlyDictionary<string, object?>)Helpers.SortArgumentValue(source)!;
    }

    /// <summary>
    /// The directive name WITHOUT the leading <c>@</c> (stored as <c>"include"</c>, rendered as
    /// <c>@include</c>).
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; init; }

    /// <summary>
    /// The directive's arguments, or <c>null</c> when the directive takes no arguments. Argument
    /// values render with the same formatter used for field arguments.
    /// </summary>
    [JsonPropertyName("arguments")]
    public IReadOnlyDictionary<string, object?>? Arguments { get; init; }

    /// <summary>
    /// Structural equality for dedup on the merge path. The compiler-generated record
    /// <see cref="object.Equals(object?)"/> compares <see cref="Arguments"/> by REFERENCE (an
    /// <see cref="IReadOnlyDictionary{TKey, TValue}"/> has no value equality), so two independently
    /// built <c>@include(if:$x)</c> instances are never record-equal. This compares by
    /// <see cref="Name"/> plus argument entries (case-insensitive keys, value-equal values —
    /// <see cref="Variable"/> equality is value-based), which is what "the same directive" means
    /// for merge deduplication.
    /// </summary>
    internal bool IsStructurallyEqualTo(FieldDirective other)
    {
        if (!string.Equals(Name, other.Name, StringComparison.Ordinal))
        {
            return false;
        }

        var a = Arguments;
        var b = other.Arguments;
        if (a is null || a.Count == 0)
        {
            return b is null || b.Count == 0;
        }
        if (b is null || a.Count != b.Count)
        {
            return false;
        }

        foreach (var (key, value) in a)
        {
            if (!b.TryGetValue(key, out var otherValue) || !Equals(value, otherValue))
            {
                return false;
            }
        }
        return true;
    }
}
