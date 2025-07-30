using System;
using System.Globalization;
using FluentAssertions;
using Xunit;

namespace NGql.Core.Tests.Issues;

public class GitHubIssuesTests
{
    [Fact]
    public void Issue_12_Given_DateTime_Value_In_Where_Clause_Causes_StackOverflow()
    {
        // Arrange
        var id = "some-package-id-42";
        var date = DateTime.Parse("2025-07-29T00:00:00Z").ToString(ValueFormatter.DateFormat, CultureInfo.InvariantCulture);
        string queryText = new Query("BookingQuery")
            .Select(new Query("getBookingSessions")
                .Where("packageId", id)
                .Where("date", date)
                .Select(["id", "name", "startTime", "endTime"]));

        // Assert
        queryText.Should().Be($@"query BookingQuery{{
    getBookingSessions(date:""{date}"", packageId:""some-package-id-42""){{
        id
        name
        startTime
        endTime
    }}
}}");
    }
}
