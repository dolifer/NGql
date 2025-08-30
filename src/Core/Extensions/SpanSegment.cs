namespace NGql.Core.Extensions;

/// <summary>
/// Ref struct for holding field segment information as spans to avoid string allocations
/// </summary>
internal readonly ref struct SpanSegment(
    ReadOnlySpan<char> name,
    ReadOnlySpan<char> alias,
    bool isLastFragment,
    ReadOnlySpan<char> parsedType)
{
    public ReadOnlySpan<char> Name { get; } = name;
    public ReadOnlySpan<char> Alias { get; } = alias;
    public ReadOnlySpan<char> ParsedType { get; } = parsedType;
    public bool IsLastFragment { get; } = isLastFragment;
    public bool HasAlias => !Alias.IsEmpty;
    public bool HasParsedType => !ParsedType.IsEmpty;
}
