using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using FluentAssertions;
using Xunit;
// ReSharper disable RedundantExplicitParamsArrayCreation

namespace NGql.Core.Tests.Issues;

[SuppressMessage("Minor Code Smell", "S3878:Arrays should not be created for params parameters")]
[SuppressMessage("Minor Code Smell", "S3220:Method calls should not resolve ambiguously to overloads with \"params\"")]
public class GitHubIssuesTests
{
    [Fact]
    public void Issue_12_Given_DateTime_Value_In_Where_Clause_Causes_StackOverflow()
    {
        // Arrange
        var id = "some-package-id-42";
        var date = DateTime.Parse("2025-07-29T00:00:00Z", CultureInfo.InvariantCulture).ToString(ValueFormatter.DateFormat, CultureInfo.InvariantCulture);
        string queryText = new Query("BookingQuery")
            .Select(new Query("getBookingSessions")
                .Where("packageId", id)
                .Where("date", date)
                .Select(["id", "name", "startTime", "endTime"]));

        // Assert
        queryText.Should().Be($@"query BookingQuery{{
    getBookingSessions(date:""{date}"", packageId:""some-package-id-42""){{
        endTime
        id
        name
        startTime
    }}
}}");
    }
}
