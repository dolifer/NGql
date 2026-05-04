using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace NGql.Core.Abstractions;

/// <summary>
///     Represents a GraphQL named fragment — a reusable selection set declared at the operation
///     top level and referenced from one or more field selection sets via the <c>...Name</c>
///     spread syntax. Renders as <c>fragment Name on TypeName { … }</c> after the operation
///     block.
/// </summary>
/// <remarks>
///     Named fragments live on <see cref="QueryDefinition.NamedFragments"/> (operation-scoped),
///     not on <see cref="FieldDefinition.InlineFragments"/> (field-scoped). They are referenced
///     from a field's selection set by adding the fragment's <see cref="Name"/> to
///     <see cref="FieldDefinition.SpreadFragments"/>; the renderer emits <c>...Name</c> at
///     each spread site.
///
///     Naming follows the GraphQL spec: ASCII letters, digits, and underscores; first character
///     must be a letter or underscore. Validation is intentionally minimal — invalid names
///     render verbatim and the server rejects them with a clear error.
/// </remarks>
[SuppressMessage("Minor Code Smell", "S2292:Trivial properties should be auto-implemented")]
public sealed record NamedFragmentDefinition
{
    internal FieldChildren? _fields;
    internal Dictionary<string, InlineFragmentDefinition>? _fragments;
    internal List<string>? _spreadFragments;

    /// <summary>
    /// Creates a named fragment with the given <paramref name="name"/> on <paramref name="onType"/>
    /// and no children.
    /// </summary>
    /// <param name="name">The fragment's identifier, used at spread sites (<c>...Name</c>).
    /// Cannot be null or whitespace.</param>
    /// <param name="onType">The GraphQL type the fragment is declared on. Rendered verbatim
    /// after <c>fragment Name on </c>.</param>
    public NamedFragmentDefinition(string name, string onType)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Named fragment name cannot be null or whitespace.", nameof(name));
        }
        if (string.IsNullOrWhiteSpace(onType))
        {
            throw new ArgumentException("Named fragment type name cannot be null or whitespace.", nameof(onType));
        }

        Name = name;
        OnType = onType;
    }

    /// <summary>
    /// The fragment's identifier. Used by spreads (<see cref="FieldDefinition.SpreadFragments"/>)
    /// to reference this fragment.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; init; }

    /// <summary>
    /// The GraphQL type the fragment is declared on. Rendered verbatim after <c>fragment Name on </c>.
    /// </summary>
    [JsonPropertyName("onType")]
    public string OnType { get; init; }

    /// <summary>
    /// Fields selected by this fragment.
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
    /// Inline fragments inside this named fragment's selection set. Useful when the fragment's
    /// fields themselves return union/interface types that need further narrowing.
    /// </summary>
    [JsonPropertyName("inlineFragments")]
    public IReadOnlyDictionary<string, InlineFragmentDefinition> InlineFragments
        => (IReadOnlyDictionary<string, InlineFragmentDefinition>?)_fragments ?? EmptyFragments;

    /// <summary>
    /// Names of other <see cref="NamedFragmentDefinition"/>s spread inside this fragment's
    /// selection set. See <see cref="FieldDefinition.SpreadFragments"/> for the contract.
    /// </summary>
    [JsonPropertyName("spreadFragments")]
    public IReadOnlyList<string> SpreadFragments
        => (IReadOnlyList<string>?)_spreadFragments ?? EmptySpreadFragments;

    private static readonly IReadOnlyDictionary<string, FieldDefinition> EmptyFields = new Dictionary<string, FieldDefinition>();
    private static readonly IReadOnlyDictionary<string, InlineFragmentDefinition> EmptyFragments = new Dictionary<string, InlineFragmentDefinition>();
    private static readonly IReadOnlyList<string> EmptySpreadFragments = Array.Empty<string>();

    public bool Equals(NamedFragmentDefinition? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return string.Equals(Name, other.Name, StringComparison.Ordinal);
    }

    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Name);
}
