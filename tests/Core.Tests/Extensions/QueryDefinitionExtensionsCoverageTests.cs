using System.Collections.Generic;
using FluentAssertions;
using NGql.Core.Abstractions;
using NGql.Core.Extensions;
using Xunit;

namespace NGql.Core.Tests.Extensions;

/// <summary>
/// Covers the name-or-alias matching arms of <see cref="QueryDefinitionExtensions"/> that the
/// behavioural suite leaves partially reached.
/// </summary>
public class QueryDefinitionExtensionsCoverageTests
{
    [Fact]
    public void FindFieldRecursively_WhenAliasedFieldMatchesNeitherNameNorAlias_ReturnsNoPaths()
    {
        var fields = new Dictionary<string, FieldDefinition>
        {
            ["userName"] = new("name", "String", "userName"),
        };

        var result = QueryDefinitionExtensions.FindFieldRecursively(fields, "email", string.Empty);

        result.Should().BeEmpty();
    }

    [Fact]
    public void FindFieldRecursively_WhenAliasedFieldMatchesAlias_ReturnsItsPath()
    {
        var fields = new Dictionary<string, FieldDefinition>
        {
            ["userName"] = new("name", "String", "userName"),
        };

        var result = QueryDefinitionExtensions.FindFieldRecursively(fields, "userName", string.Empty);

        result.Should().ContainSingle().Which.Should().Be("userName");
    }
}
