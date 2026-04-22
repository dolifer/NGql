namespace NGql.Core;

public readonly struct EnumValue : IComparable, IComparable<EnumValue>, IEquatable<EnumValue>
{
    /// <summary>
    /// The value for the enum.
    /// </summary>
    public string Value { get; }

    public EnumValue(object value)
    {
        ArgumentNullException.ThrowIfNull(value);
        Value = value switch
        {
            string str when !string.IsNullOrWhiteSpace(str) => str,
            Enum enumValue => enumValue.ToString() ?? throw new ArgumentException("Enum value cannot be null.", nameof(value)),
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
