using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using FluentAssertions;
using Xunit;

namespace NGql.Core.Tests.Issues;

public class ScalarFormattingTests
{
    public static IEnumerable<object[]> Values()
    {
        foreach (var value in new object[]
        {
            sbyte.MinValue, sbyte.MaxValue, short.MinValue, short.MaxValue,
            int.MinValue, int.MaxValue, long.MinValue, long.MaxValue,
            float.MinValue, float.MaxValue, float.Epsilon, -0.0f,
            double.MinValue, double.MaxValue, double.Epsilon, -0.0d,
            decimal.MinValue, decimal.MaxValue, 1.2300m,
            DateTime.MinValue, DateTime.MaxValue,
            new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc),
            DateTimeOffset.MinValue, DateTimeOffset.MaxValue,
            new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.FromHours(-7))
        })
        {
            yield return new[] { value };
        }
    }

    [Theory]
    [MemberData(nameof(Values))]
    public void ScalarFormatting_MatchesInvariantRepresentation(object value)
    {
        var originalCulture = CultureInfo.CurrentCulture;
        try
        {
            var culture = (CultureInfo)CultureInfo.GetCultureInfo("fr-FR").Clone();
            culture.NumberFormat.NegativeSign = "~";
            CultureInfo.CurrentCulture = culture;
            var isDate = value is DateTime or DateTimeOffset;
            var expected = ((IFormattable)value).ToString(
                isDate ? ValueFormatter.DateFormat : null, CultureInfo.InvariantCulture);
            if (isDate) expected = '"' + expected + '"';

            var builder = new StringBuilder();
            ValueFormatter.TryAppendPrimitive(value, builder).Should().BeTrue();
            builder.ToString().Should().Be(expected);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }
}
