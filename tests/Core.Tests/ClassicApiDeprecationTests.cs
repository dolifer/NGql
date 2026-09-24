using System;
using System.Reflection;
using FluentAssertions;
using NGql.Core.Abstractions;
using Xunit;

namespace NGql.Core.Tests;

public class ClassicApiDeprecationTests
{
    [Theory]
    [InlineData(typeof(Query))]
    [InlineData(typeof(Mutation))]
    [InlineData(typeof(QueryBlock))]
    public void ClassicApiType_IsObsoleteWithOwnDiagnosticId(Type type)
    {
        var obsolete = type.GetCustomAttribute<ObsoleteAttribute>();

        obsolete.Should().NotBeNull();
        obsolete!.IsError.Should().BeFalse("2.x only warns; removal happens in 3.0");
        obsolete.DiagnosticId.Should().Be("NGQL0001");
        obsolete.UrlFormat.Should().EndWith("docs/reference/MIGRATION.md");
        obsolete.Message.Should().Contain("removed in NGql 3.0").And.Contain("QueryBuilder");
    }
}
