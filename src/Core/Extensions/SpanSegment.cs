namespace NGql.Core.Extensions;

/// <summary>
/// Ref struct for holding field segment information as spans to avoid string allocations
/// </summary>
internal readonly ref struct SpanSegment
{
    public readonly ReadOnlySpan<char> Name;
    public readonly ReadOnlySpan<char> Alias;
    public readonly bool IsLastFragment;
    public readonly ReadOnlySpan<char> ParsedType;

    public SpanSegment(ReadOnlySpan<char> name, ReadOnlySpan<char> alias, bool isLastFragment, ReadOnlySpan<char> parsedType)
    {
        Name = name;
        Alias = alias;
        IsLastFragment = isLastFragment;
        ParsedType = parsedType;
    }

    public readonly bool HasAlias => !Alias.IsEmpty;
    public readonly bool HasParsedType => !ParsedType.IsEmpty;
}
