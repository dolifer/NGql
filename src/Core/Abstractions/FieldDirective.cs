using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

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
    /// <param name="arguments">Optional directive arguments; <c>null</c> for a no-argument directive.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is null or whitespace.</exception>
    public FieldDirective(string name, IReadOnlyDictionary<string, object?>? arguments = null)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Directive name cannot be null or whitespace.", nameof(name));
        }

        Name = name;
        Arguments = arguments is { Count: > 0 } ? arguments : null;
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
}
