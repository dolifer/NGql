using System.Globalization;
using System.Text;

namespace NGql.Core;

internal static class ValueFormatter
{
    internal const string DateFormat = "yyyy-MM-dd'T'HH:mm:ss.fffK";

    // Pre-allocated strings for common boolean values to avoid repeated allocations
    private const string TrueString = "true";
    private const string FalseString = "false";

    /// <summary>
    /// Returns true when <paramref name="value"/> is a primitive type this formatter handles.
    /// Does not allocate — used by callers that only need the boolean result.
    /// </summary>
    internal static bool IsPrimitiveType(object value) => value switch
    {
        string or bool
        or byte or sbyte or short or ushort or int or uint or long or ulong
        or float or double or decimal
        or DateTime or DateTimeOffset
        or EnumValue or Enum or Variable => true,
        _ => false
    };

    /// <summary>
    /// Writes the primitive representation of <paramref name="value"/> directly to
    /// <paramref name="builder"/> without allocating an intermediate string.
    /// Uses <see cref="ISpanFormattable"/> for all numeric / date types.
    /// </summary>
    /// <returns>True if the value was handled; false if it is not a known primitive.</returns>
    internal static bool TryAppendPrimitive(object value, StringBuilder builder)
    {
        switch (value)
        {
            case string s:
                builder.Append('"'); builder.Append(s); builder.Append('"');
                return true;

            case bool b:
                builder.Append(b ? TrueString : FalseString);
                return true;

            case int v:    builder.Append(v); return true;
            case long v:   builder.Append(v); return true;
            case uint v:   builder.Append(v); return true;
            case ulong v:  builder.Append(v); return true;
            case short v:  builder.Append(v); return true;
            case ushort v: builder.Append(v); return true;
            case byte v:   builder.Append(v); return true;
            case sbyte v:  builder.Append(v); return true;

            case float v:
            {
                Span<char> buf = stackalloc char[32];
                builder.Append(v.TryFormat(buf, out int n, provider: CultureInfo.InvariantCulture)
                    ? buf[..n]
                    : v.ToString(CultureInfo.InvariantCulture).AsSpan());
                return true;
            }
            case double v:
            {
                Span<char> buf = stackalloc char[32];
                builder.Append(v.TryFormat(buf, out int n, provider: CultureInfo.InvariantCulture)
                    ? buf[..n]
                    : v.ToString(CultureInfo.InvariantCulture).AsSpan());
                return true;
            }
            case decimal v:
            {
                Span<char> buf = stackalloc char[32];
                builder.Append(v.TryFormat(buf, out int n, provider: CultureInfo.InvariantCulture)
                    ? buf[..n]
                    : v.ToString(CultureInfo.InvariantCulture).AsSpan());
                return true;
            }

            case DateTime v:
            {
                Span<char> buf = stackalloc char[40];
                builder.Append('"');
                builder.Append(v.TryFormat(buf, out int n, DateFormat, CultureInfo.InvariantCulture)
                    ? buf[..n]
                    : v.ToString(DateFormat, CultureInfo.InvariantCulture).AsSpan());
                builder.Append('"');
                return true;
            }
            case DateTimeOffset v:
            {
                Span<char> buf = stackalloc char[40];
                builder.Append('"');
                builder.Append(v.TryFormat(buf, out int n, DateFormat, CultureInfo.InvariantCulture)
                    ? buf[..n]
                    : v.ToString(DateFormat, CultureInfo.InvariantCulture).AsSpan());
                builder.Append('"');
                return true;
            }

            case EnumValue ev:
                builder.Append(ev.Value);
                return true;

            case Enum e:
                builder.Append(e.ToString()); // Enum.ToString() is unavoidable without codegen
                return true;

            case Variable v:
                builder.Append(v.Name);
                return true;
        }

        return false;
    }

    /// <summary>
    /// Legacy overload — kept for callers that genuinely need the formatted string value.
    /// Prefer <see cref="TryAppendPrimitive"/> when a <see cref="StringBuilder"/> is in scope.
    /// </summary>
    internal static bool TryFormatPrimitiveType(object value, out string? stringValue)
    {
        stringValue = value switch
        {
            string s => $"\"{s}\"",
            bool boolValue => boolValue ? TrueString : FalseString,

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

            EnumValue enumValue => enumValue.Value,
            Enum enumValue => enumValue.ToString(),
            Variable variable => variable.Name,

            _ => null
        };

        return !string.IsNullOrWhiteSpace(stringValue);
    }
}
