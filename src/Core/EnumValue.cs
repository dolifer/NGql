namespace NGql.Core;

/// <summary>
/// Wraps a GraphQL enum value so it renders unquoted (e.g. <c>role:ADMIN</c>) instead of as
/// a string literal. Pass an <see cref="EnumValue"/> as an argument value to opt out of
/// string quoting in the rendered query.
/// </summary>
public readonly struct EnumValue : IComparable, IComparable<EnumValue>, IEquatable<EnumValue>
{
    /// <summary>The enum identifier as it will appear in the rendered GraphQL.</summary>
    public string Value { get; }

    /// <summary>
    /// Creates an <see cref="EnumValue"/> from a non-empty string identifier or a CLR
    /// <see cref="System.Enum"/> instance (its <c>ToString()</c> becomes the rendered value).
    /// </summary>
    /// <param name="value">A non-empty string or an <see cref="Enum"/> instance.</param>
    /// <exception cref="ArgumentNullException"><paramref name="value"/> is null.</exception>
    /// <exception cref="ArgumentException"><paramref name="value"/> is an empty/whitespace string or an unsupported type.</exception>
    public EnumValue(object value)
    {
        ArgumentNullException.ThrowIfNull(value);
        Value = value switch
        {
            string str when !string.IsNullOrWhiteSpace(str) => str,
            Enum enumValue => enumValue.ToString(),
            string => throw new ArgumentException("Enum value cannot be null or whitespace.", nameof(value)),
            _ => throw new ArgumentException($"Invalid enum value type: {value.GetType().Name}. Expected a non-empty string or Enum.", nameof(value))
        };
    }

    public override string ToString() => Value;

    public static bool operator ==(EnumValue left, EnumValue right) => left.Equals(right);

    public static bool operator !=(EnumValue left, EnumValue right) => !left.Equals(right);

    public static bool operator <(EnumValue left, EnumValue right) => left.CompareTo(right) < 0;

    public static bool operator >(EnumValue left, EnumValue right) => left.CompareTo(right) > 0;

    public static bool operator <=(EnumValue left, EnumValue right) => left.CompareTo(right) <= 0;

    public static bool operator >=(EnumValue left, EnumValue right) => left.CompareTo(right) >= 0;

    public int CompareTo(object? obj)
    {
        return obj switch
        {
            null => 1,
            EnumValue enumValue => CompareTo(enumValue),
            _ => throw new ArgumentException($"Object must be of type {nameof(EnumValue)}")
        };
    }

    public int CompareTo(EnumValue other) => string.Compare(Value, other.Value, StringComparison.OrdinalIgnoreCase);

    public override bool Equals(object? obj) => CompareTo(obj) == 0;

    public bool Equals(EnumValue other) => CompareTo(other) == 0;

    public override int GetHashCode() => Value.GetHashCode(StringComparison.OrdinalIgnoreCase);
}
