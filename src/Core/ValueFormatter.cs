using System;
using System.Globalization;

namespace NGql.Core;

internal static class ValueFormatter
{
    // Pre-allocated strings for common boolean values to avoid repeated allocations
    private const string TrueString = "true";
    private const string FalseString = "false";

    /// <summary>
    /// Attempts to format a primitive type value as a string with optimized handling for common types.
    /// </summary>
    /// <param name="value">The value to format</param>
    /// <param name="stringValue">The formatted string value if successful</param>
    /// <returns>True if the value was successfully formatted, false otherwise</returns>
    internal static bool TryFormatPrimitiveType(object value, out string? stringValue)
    {
        stringValue = value switch
        {
            string s => $"\"{s}\"",
            bool boolValue => boolValue ? TrueString : FalseString,
            
            // Optimized numeric formatting using invariant culture
            byte byteValue => byteValue.ToString(CultureInfo.InvariantCulture),
            sbyte sbyteValue => sbyteValue.ToString(CultureInfo.InvariantCulture),
            short shortValue => shortValue.ToString(CultureInfo.InvariantCulture),
            ushort ushortValue => ushortValue.ToString(CultureInfo.InvariantCulture),
            int intValue => intValue.ToString(CultureInfo.InvariantCulture),
            uint uintValue => uintValue.ToString(CultureInfo.InvariantCulture),
            long longValue => longValue.ToString(CultureInfo.InvariantCulture),
            ulong ulongValue => ulongValue.ToString(CultureInfo.InvariantCulture),
            
            // Floating point numbers with invariant culture
            float floatValue => floatValue.ToString(CultureInfo.InvariantCulture),
            double doubleValue => doubleValue.ToString(CultureInfo.InvariantCulture),
            decimal decimalValue => decimalValue.ToString(CultureInfo.InvariantCulture),
            
            // Custom types
            EnumValue enumValue => enumValue.Value,
            Enum enumValue => enumValue.ToString(),
            Variable variable => variable.Name,
            
            _ => default
        };

        return !string.IsNullOrWhiteSpace(stringValue);
    }
}
