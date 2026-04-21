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
}
