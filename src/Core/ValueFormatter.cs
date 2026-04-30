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
            case Enum e: builder.Append(e.ToString()); return true;
            case EnumValue ev: builder.Append(ev.Value); return true;
            case Variable variable: builder.Append(variable.Name); return true;
            default: return false;
        }
    }

    private static void AppendString(StringBuilder builder, string s)
    {
        builder.Append('"');
        builder.Append(s);
        builder.Append('"');
    }

    private static void AppendBoolean(StringBuilder builder, bool b)
        => builder.Append(b ? TrueString : FalseString);

    private static bool TryAppendInteger(object value, StringBuilder builder)
        => TryAppendSignedInteger(value, builder) || TryAppendUnsignedInteger(value, builder);

    private static bool TryAppendSignedInteger(object value, StringBuilder builder)
    {
        switch (value)
        {
            case int v: builder.Append(v); return true;
            case long v: builder.Append(v); return true;
            case short v: builder.Append(v); return true;
            case sbyte v: builder.Append(v); return true;
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
            case float v: AppendFormattable(builder, v); return true;
            case double v: AppendFormattable(builder, v); return true;
            case decimal v: AppendFormattable(builder, v); return true;
            default: return false;
        }
    }

    /// <summary>Formats <paramref name="value"/> with invariant culture. Used for
    /// float/double/decimal — none of those produce strings longer than the BCL guarantees.</summary>
    private static void AppendFormattable(StringBuilder builder, IFormattable value)
        => builder.Append(value.ToString(null, CultureInfo.InvariantCulture));

    /// <summary>Formats <paramref name="value"/> with the NGql DateFormat and quotes it.
    /// Used for DateTime/DateTimeOffset.</summary>
    private static void AppendQuotedFormattable(StringBuilder builder, IFormattable value)
    {
        builder.Append('"');
        builder.Append(value.ToString(DateFormat, CultureInfo.InvariantCulture));
        builder.Append('"');
    }
}
