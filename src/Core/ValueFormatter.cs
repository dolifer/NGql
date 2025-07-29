using System;
using System.Globalization;
using System.Numerics;

namespace NGql.Core;

internal static class ValueFormatter
{
    internal const string DateFormat = "yyyy-MM-dd'T'HH:mm:ss.fffK";
    
    internal static bool TryFormatPrimitiveType(object value, out string? stringValue)
    {
        stringValue = value switch
        {
            string s => $"\"{s}\"",
            bool boolValue => boolValue.ToString(CultureInfo.InvariantCulture).ToLowerInvariant(),
            byte byteValue => byteValue.ToString(CultureInfo.InvariantCulture),
            sbyte sbyteValue => sbyteValue.ToString(CultureInfo.InvariantCulture),
            short shortValue => shortValue.ToString(CultureInfo.InvariantCulture),
            ushort ushortValue => ushortValue.ToString(CultureInfo.InvariantCulture),
            int intValue => intValue.ToString(CultureInfo.InvariantCulture),
            uint uintValue => uintValue.ToString(CultureInfo.InvariantCulture),
            long longValue => longValue.ToString(CultureInfo.InvariantCulture),
            ulong ulongValue => ulongValue.ToString(CultureInfo.InvariantCulture),
            float floatValue => floatValue.ToString(CultureInfo.InvariantCulture),
            double doubleValue => doubleValue.ToString(CultureInfo.InvariantCulture),
            decimal decimalValue => decimalValue.ToString(CultureInfo.InvariantCulture),
            DateTime dateTimeValue => $"\"{dateTimeValue.ToString(DateFormat, CultureInfo.InvariantCulture)}\"",
            DateTimeOffset dateTimeOffsetValue => $"\"{dateTimeOffsetValue.ToString(DateFormat, CultureInfo.InvariantCulture)}\"",
            Enum enumValue => enumValue.ToString(),
            Variable variable => variable.Name,
            _ => default
        };

        return !string.IsNullOrWhiteSpace(stringValue);
    }
}
