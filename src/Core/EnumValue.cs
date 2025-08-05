using System;

namespace NGql.Core;

public readonly struct EnumValue : IComparable, IComparable<EnumValue>, IEquatable<EnumValue>
{
    /// <summary>
    /// The value for the enum.
    /// </summary>
    public string Value { get; }

    public EnumValue(object value)
    {
        Value = value switch
        {
            string str when !string.IsNullOrWhiteSpace(str) => str,
            Enum enumValue => enumValue.ToString() ?? throw new ArgumentException("Enum value cannot be null.", nameof(value)),
            _ => throw new ArgumentException($"Invalid enum value type: {value.GetType().Name}", nameof(value))
        };
        
        Value = value.ToString() ?? throw new ArgumentException("Enum value cannot be null.", nameof(value));
        
        if (value is null)
            throw new ArgumentException("Enum value cannot be null or whitespace.", nameof(value));
    }

    public override string ToString() => Value;

    public int CompareTo(object? obj)
    {
        return obj switch
        {
            null => 1,
            EnumValue enumValue => CompareTo(enumValue),
            _ => throw new ArgumentException($"Object must be of type {nameof(EnumValue)}")
        };
    }

    public static bool operator ==(EnumValue left, EnumValue right) => left.Equals(right);
    public static bool operator !=(EnumValue left, EnumValue right) => !left.Equals(right);
        
    public static bool operator <(EnumValue left, EnumValue right) => left.CompareTo(right) < 0;
    public static bool operator >(EnumValue left, EnumValue right) => left.CompareTo(right) > 0;
        
    public static bool operator <=(EnumValue left, EnumValue right) => left.CompareTo(right) <= 0;
    public static bool operator >=(EnumValue left, EnumValue right) => left.CompareTo(right) >= 0;
    
    public int CompareTo(EnumValue other) => string.Compare(Value, other.Value, StringComparison.OrdinalIgnoreCase);

    public override bool Equals(object? obj) => CompareTo(obj) == 0;

    public bool Equals(EnumValue other) => CompareTo(other) == 0;

    public override int GetHashCode() => Value.GetHashCode(StringComparison.OrdinalIgnoreCase);
}
