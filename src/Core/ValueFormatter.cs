using System.Buffers;
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
    /// Single is-test per category keeps cyclomatic complexity flat.
    /// </summary>
    internal static bool IsPrimitiveType(object value)
        => IsString(value) || IsBoolean(value) || IsInteger(value) || IsFloating(value)
        || IsDate(value) || IsEnumLike(value) || value is Variable;

    private static bool IsString(object v) => v is string;
    private static bool IsBoolean(object v) => v is bool;
    private static bool IsInteger(object v) => IsSignedInteger(v) || IsUnsignedInteger(v);
    private static bool IsSignedInteger(object v) => v is sbyte or short or int or long;
    private static bool IsUnsignedInteger(object v) => v is byte or ushort or uint or ulong;
    private static bool IsFloating(object v) => v is float or double or decimal;
    private static bool IsDate(object v) => v is DateTime or DateTimeOffset;
    private static bool IsEnumLike(object v) => v is EnumValue or Enum;

    /// <summary>
    /// Writes the primitive representation of <paramref name="value"/> directly to
    /// <paramref name="builder"/> without allocating an intermediate string.
    /// Each value category dispatches to a small dedicated helper to keep this top-level
    /// switch's cyclomatic complexity low.
    /// </summary>
    /// <returns>True if the value was handled; false if it is not a known primitive.</returns>
    internal static bool TryAppendPrimitive(object value, StringBuilder builder)
        => TryAppendScalar(value, builder)
        || TryAppendNamed(value, builder)
        || TryAppendInteger(value, builder)
        || TryAppendFloating(value, builder);

    private static bool TryAppendScalar(object value, StringBuilder builder)
    {
        switch (value)
        {
            case string s: AppendString(builder, s); return true;
            case bool b: AppendBoolean(builder, b); return true;
            case DateTime dt: AppendQuotedFormattable(builder, dt); return true;
            case DateTimeOffset dto: AppendQuotedFormattable(builder, dto); return true;
            default: return false;
        }
    }

    private static bool TryAppendNamed(object value, StringBuilder builder)
    {
        switch (value)
        {
            case Enum e: builder.Append(FormatEnumName(e)); return true;
            case EnumValue ev: builder.Append(ev.Value); return true;
            case Variable variable: builder.Append(variable.Name); return true;
            default: return false;
        }
    }

    /// <summary>
    /// Renders an enum as its single GraphQL <c>Name</c>. Per spec § 3.9 an EnumValue must be a
    /// Name, so undefined numeric values and unnamed <c>[Flags]</c> combinations (whose
    /// <c>ToString()</c> yields <c>"999"</c> or <c>"Read, Write"</c>) are rejected rather than
    /// emitted as invalid output.
    /// </summary>
    internal static string FormatEnumName(Enum value)
    {
        if (!Enum.IsDefined(value.GetType(), value))
        {
            throw new ArgumentException(
                $"GraphQL enum arguments must be a single defined enum member. " +
                $"The value '{value}' of type '{value.GetType().Name}' is not a defined member " +
                $"(undefined numeric values and unnamed [Flags] combinations are not valid GraphQL enum names).",
                nameof(value));
        }
        return value.ToString();
    }

    private static void AppendString(StringBuilder builder, string s)
    {
        builder.Append('"');
        if (NeedsEscape(s))
        {
            AppendEscapedBody(builder, s);
        }
        else
        {
            builder.Append(s);
        }
        builder.Append('"');
    }

    /// <summary>
    /// Per GraphQL spec § 2.9.4 a regular string literal must escape <c>\</c>, <c>"</c>, and
    /// the C0 control characters. Non-control Unicode characters pass through verbatim — the
    /// transport carries UTF-8.
    /// </summary>
    private static bool NeedsEscape(string s)
        => s.AsSpan().IndexOfAny(CharactersToEscape) >= 0;

    private static readonly SearchValues<char> CharactersToEscape = CreateCharactersToEscape();

    // '"', '\\' and the C0 control characters U+0000–U+001F.
    private static SearchValues<char> CreateCharactersToEscape()
    {
        Span<char> characters = stackalloc char[0x20 + 2];
        for (var c = 0; c < 0x20; c++) characters[c] = (char)c;
        characters[0x20] = '"';
        characters[0x21] = '\\';
        return SearchValues.Create(characters);
    }

    private static void AppendEscapedBody(StringBuilder builder, string s)
    {
        for (var i = 0; i < s.Length; i++)
        {
            var c = s[i];
            switch (c)
            {
                case '"':  builder.Append("\\\""); break;
                case '\\': builder.Append("\\\\"); break;
                case '\b': builder.Append("\\b");  break;
                case '\f': builder.Append("\\f");  break;
                case '\n': builder.Append("\\n");  break;
                case '\r': builder.Append("\\r");  break;
                case '\t': builder.Append("\\t");  break;
                default:
                    if (c < 0x20)
                    {
                        // Other C0 controls — emit as \u00XX (GraphQL accepts EscapedUnicode).
                        builder.Append("\\u");
                        builder.Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        builder.Append(c);
                    }
                    break;
            }
        }
    }

    private static void AppendBoolean(StringBuilder builder, bool b)
        => builder.Append(b ? TrueString : FalseString);

    private static bool TryAppendInteger(object value, StringBuilder builder)
        => TryAppendSignedInteger(value, builder) || TryAppendUnsignedInteger(value, builder);

    private static bool TryAppendSignedInteger(object value, StringBuilder builder)
    {
        switch (value)
        {
            case int v: AppendFormattable(builder, v); return true;
            case long v: AppendFormattable(builder, v); return true;
            case short v: AppendFormattable(builder, (int)v); return true;
            case sbyte v: AppendFormattable(builder, (int)v); return true;
            default: return false;
        }
    }

    private static bool TryAppendUnsignedInteger(object value, StringBuilder builder)
    {
        switch (value)
        {
            case uint v: builder.Append(v); return true;
            case ulong v: builder.Append(v); return true;
            case ushort v: builder.Append(v); return true;
            case byte v: builder.Append(v); return true;
            default: return false;
        }
    }

    private static bool TryAppendFloating(object value, StringBuilder builder)
    {
        switch (value)
        {
            case float v: GuardFiniteFloat(v); AppendFormattable(builder, v); return true;
            case double v: GuardFiniteDouble(v); AppendFormattable(builder, v); return true;
            case decimal v: AppendFormattable(builder, v); return true;
            default: return false;
        }
    }

    /// <summary>
    /// GraphQL FloatValue (spec § 3.5.2) has no representation for NaN or Infinity. The BCL would
    /// otherwise emit the bare tokens <c>NaN</c> / <c>Infinity</c> / <c>-Infinity</c>, which a
    /// conforming server rejects, so such values are refused at build time instead.
    /// </summary>
    private static void GuardFiniteDouble(double v)
    {
        if (double.IsNaN(v) || double.IsInfinity(v))
            throw new FormatException($"GraphQL does not support NaN/Infinity float values (got '{v.ToString(CultureInfo.InvariantCulture)}').");
    }

    private static void GuardFiniteFloat(float v)
    {
        if (float.IsNaN(v) || float.IsInfinity(v))
            throw new FormatException($"GraphQL does not support NaN/Infinity float values (got '{v.ToString(CultureInfo.InvariantCulture)}').");
    }

    /// <summary>Formats <paramref name="value"/> with invariant culture. Used for
    /// int/long/float/double/decimal — none of those produce strings longer than the BCL guarantees.</summary>
    private static void AppendFormattable<T>(StringBuilder builder, T value, ReadOnlySpan<char> format = default)
        where T : struct, ISpanFormattable
    {
        // Enough for the supported numeric types and DateFormat. The fallback inside
        // AppendSpanFormatted keeps this helper safe if another format or value type is
        // introduced later.
        Span<char> buffer = stackalloc char[StackBufferLength];
        AppendSpanFormatted(builder, value, format, buffer);
    }

    /// <summary>Stack buffer size used by <see cref="AppendFormattable{T}"/>; comfortably above the
    /// longest output of any currently-supported value type and format.</summary>
    internal const int StackBufferLength = 64;

    /// <summary>
    /// Formats <paramref name="value"/> into <paramref name="buffer"/> and appends the result,
    /// falling back to an allocating <c>ToString</c> when the buffer is too small. Split out from
    /// <see cref="AppendFormattable{T}"/> so the undersized-buffer fallback is exercisable without
    /// changing the production buffer size.
    /// </summary>
    internal static void AppendSpanFormatted<T>(StringBuilder builder, T value, ReadOnlySpan<char> format, Span<char> buffer)
        where T : struct, ISpanFormattable
    {
        if (value.TryFormat(buffer, out var written, format, CultureInfo.InvariantCulture))
        {
            builder.Append(buffer[..written]);
            return;
        }

        AppendFormattedFallback(builder, value, format);
    }

    /// <summary>Allocating path taken only when the destination span cannot hold the formatted
    /// value. Non-generic so it is compiled once rather than per value type.</summary>
    private static void AppendFormattedFallback(StringBuilder builder, IFormattable value, ReadOnlySpan<char> format)
        => builder.Append(value.ToString(format.IsEmpty ? null : format.ToString(), CultureInfo.InvariantCulture));

    /// <summary>Formats <paramref name="value"/> with the NGql DateFormat and quotes it.
    /// Used for DateTime/DateTimeOffset.</summary>
    private static void AppendQuotedFormattable<T>(StringBuilder builder, T value)
        where T : struct, ISpanFormattable
    {
        builder.Append('"');
        AppendFormattable(builder, value, DateFormat);
        builder.Append('"');
    }
}
