using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using NGql.Core.Builders;
using NGql.Core.Features;
using Xunit;

namespace NGql.Core.Tests.Issues;

public class PreservationPrefixLookupTests
{
    [Fact]
    public void Preserve_MatchesAncestorPruningAcrossInsertionOrders()
    {
        var source = QueryBuilder.CreateDefaultBuilder("Q")
            .AddField("user.profile.name")
            .AddField("user.profile.age")
            .AddField("user.id")
            .AddField("userProfile.name");
        var candidates = new[]
        {
            "user", "USER.PROFILE", "user.profile.name", "user.id",
            "userProfile", "userProfile.name", "user.profile.age", " ", ""
        };
        var random = new Random(42);

        for (var iteration = 0; iteration < 100; iteration++)
        {
            var expected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var builder = PreservationBuilder.Create(source);
            for (var i = 0; i < 20; i++)
            {
                var path = candidates[random.Next(candidates.Length)];
                if (!string.IsNullOrWhiteSpace(path))
                {
                    expected.RemoveWhere(existing => path.StartsWith(existing + ".", StringComparison.OrdinalIgnoreCase));
                    expected.Add(path);
                }
                builder.Preserve(path);
            }

            builder.Build().ToString().Should().Be(source.Preserve(expected.ToArray()).ToString());
        }
    }

    [Fact]
    public void Preserve_RepeatedChildStillPrunesParentAddedLater()
    {
        var source = QueryBuilder.CreateDefaultBuilder("Q")
            .AddField("user.profile.name")
            .AddField("user.profile.age")
            .AddField("user.id");

        var result = PreservationBuilder.Create(source)
            .Preserve("user.profile.name", "user.profile", "user", "USER.PROFILE.NAME")
            .Build();

        result.ToString().Should().Be(source.Preserve("user.profile.name").ToString());
    }
}
