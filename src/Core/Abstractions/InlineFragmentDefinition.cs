using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace NGql.Core.Abstractions;

/// <summary>
///     Represents a GraphQL inline fragment — a type-conditional selection set rendered as
///     <c>... on TypeName { … }</c>. Used to narrow a parent field whose schema type is a
///     union or interface to a specific concrete type.
/// </summary>
/// <remarks>
///     Inline fragments are siblings of regular fields under their parent <see cref="FieldDefinition"/>
///     (see <see cref="FieldDefinition.InlineFragments"/>). They are kept in a separate collection
///     because their access pattern (lookup by type name) and rendering shape (the <c>... on</c>
///     prefix) differ from regular fields. Nested inline fragments — fragments inside fragments —
///     are supported recursively.
/// </remarks>
[SuppressMessage("Minor Code Smell", "S2292:Trivial properties should be auto-implemented")]
public sealed record InlineFragmentDefinition
{
    internal FieldChildren? _fields;
    internal Dictionary<string, InlineFragmentDefinition>? _fragments;
    internal List<string>? _spreadFragments;
    internal List<FieldDirective>? _directives;

    /// <summary>
    /// Creates an inline fragment for <paramref name="typeName"/> with no children.
    /// </summary>
    /// <param name="typeName">The GraphQL type the fragment narrows to (e.g. <c>"Repository"</c>).
    /// Rendered verbatim — case matters, GraphQL type names are case-sensitive.</param>
    public InlineFragmentDefinition(string typeName)
    {
        if (string.IsNullOrWhiteSpace(typeName))
        {
            throw new ArgumentException("Inline fragment type name cannot be null or whitespace.", nameof(typeName));
        }

        TypeName = typeName;
    }

    /// <summary>
    /// The GraphQL type the fragment narrows to. Rendered verbatim after <c>... on </c>.
    /// </summary>
    [JsonPropertyName("typeName")]
    public string TypeName { get; init; }

    /// <summary>
    /// Fields selected when the parent value's runtime type is <see cref="TypeName"/>.
    /// </summary>
    [JsonPropertyName("fields")]
    public IReadOnlyDictionary<string, FieldDefinition> Fields
        => (IReadOnlyDictionary<string, FieldDefinition>?)_fields ?? EmptyFields;

    /// <summary>
    /// Get-or-add the underlying <see cref="FieldChildren"/> store. Internal so the builder
    /// can pass a stable reference when the user's lambda adds fields to the fragment.
    /// </summary>
    internal FieldChildren GetOrCreateFieldsStore()
        => _fields ??= new FieldChildren();

    /// <summary>
    /// Nested inline fragments inside this fragment's selection set. Useful when the fragment's
    /// fields themselves return union/interface types that need further narrowing.
    /// </summary>
    [JsonPropertyName("inlineFragments")]
    public IReadOnlyDictionary<string, InlineFragmentDefinition> InlineFragments
        => (IReadOnlyDictionary<string, InlineFragmentDefinition>?)_fragments ?? EmptyFragments;

    /// <summary>
    /// Names of <see cref="NamedFragmentDefinition"/>s spread into this inline fragment's
    /// selection set. See <see cref="FieldDefinition.SpreadFragments"/> for the contract; this
    /// property mirrors it.
    /// </summary>
    [JsonPropertyName("spreadFragments")]
    public IReadOnlyList<string> SpreadFragments
        => (IReadOnlyList<string>?)_spreadFragments ?? EmptySpreadFragments;

    /// <summary>
    /// Directives attached to this inline fragment, in the order they were added. Rendered after
    /// <c>... on TypeName</c> and before the selection set, e.g. <c>... on Admin @include(if:$a){ … }</c>.
    /// Returns an empty list (never null) when the fragment has no directives.
    /// </summary>
    /// <remarks>
    /// See <see cref="Builders.FieldBuilder.IncludeIf(Variable)"/>/<see cref="Builders.FieldBuilder.SkipIf(Variable)"/>/
    /// <see cref="Builders.FieldBuilder.Directive(string, System.Collections.Generic.Dictionary{string, object?})"/>
    /// called on the <see cref="Builders.FieldBuilder"/> passed to
    /// <see cref="Builders.FieldBuilder.OnType(string, System.Action{Builders.FieldBuilder})"/>'s
    /// lambda — the same directive API fields expose, attaching to the fragment itself rather than
    /// its parent field.
    /// </remarks>
    [JsonPropertyName("directives")]
    public IReadOnlyList<FieldDirective> Directives
        => (IReadOnlyList<FieldDirective>?)_directives ?? EmptyDirectives;

    /// <summary>
    /// Gets a value indicating whether this inline fragment carries any directives. Unlike reading
    /// <see cref="Directives"/>, this check allocates nothing on directive-less fragments.
    /// </summary>
    [JsonIgnore]
    public bool HasDirectives => _directives is { Count: > 0 };

    /// <summary>
    /// Append a directive to this fragment's directive list. Shares <see cref="DirectiveListOps"/>
    /// with <see cref="FieldDefinition.AddDirective"/>, so <c>include</c>/<c>skip</c> collapse
    /// (last-call-wins) and every other directive dedups structurally, identically to fields.
    /// </summary>
    internal void AddDirective(FieldDirective directive) => DirectiveListOps.Add(ref _directives, directive);

    private static readonly IReadOnlyDictionary<string, FieldDefinition> EmptyFields = new Dictionary<string, FieldDefinition>();
    private static readonly IReadOnlyDictionary<string, InlineFragmentDefinition> EmptyFragments = new Dictionary<string, InlineFragmentDefinition>();
    private static readonly IReadOnlyList<string> EmptySpreadFragments = Array.Empty<string>();
    private static readonly IReadOnlyList<FieldDirective> EmptyDirectives = Array.Empty<FieldDirective>();

    public bool Equals(InlineFragmentDefinition? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return string.Equals(TypeName, other.TypeName, StringComparison.Ordinal);
    }

    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(TypeName);
}
