using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using NGql.Core.Abstractions;
using NGql.Core.Builders;
using Xunit;

namespace NGql.Core.Tests.Abstractions;

public class FragmentDefinitionEqualityBranchTests
{
    [Fact]
    public void InlineFragment_SpreadFragments_WithoutSpreads_ReturnsEmptyList()
    {
        var fragment = new InlineFragmentDefinition("Admin");

        fragment.SpreadFragments.Should().BeEmpty();
    }

    [Fact]
    public void InlineFragment_Equals_Null_ReturnsFalse()
    {
        var fragment = new InlineFragmentDefinition("Admin");

        fragment.Equals(null).Should().BeFalse();
    }

    [Fact]
    public void InlineFragment_Equals_SameReference_ReturnsTrue()
    {
        var fragment = new InlineFragmentDefinition("Admin");

        fragment.Equals(fragment).Should().BeTrue();
    }

    [Fact]
    public void InlineFragment_Equals_SameTypeNameDifferentInstance_ReturnsTrue()
    {
        new InlineFragmentDefinition("Admin")
            .Equals(new InlineFragmentDefinition("Admin"))
            .Should().BeTrue();
    }

    [Fact]
    public void InlineFragment_Equals_DifferentTypeName_ReturnsFalse()
    {
        new InlineFragmentDefinition("Admin")
            .Equals(new InlineFragmentDefinition("Guest"))
            .Should().BeFalse();
    }

    [Fact]
    public void NamedFragment_SpreadFragments_WithoutSpreads_ReturnsEmptyList()
    {
        var fragment = new NamedFragmentDefinition("UserParts", "User");

        fragment.SpreadFragments.Should().BeEmpty();
    }

    [Fact]
    public void NamedFragment_Equals_Null_ReturnsFalse()
    {
        var fragment = new NamedFragmentDefinition("UserParts", "User");

        fragment.Equals(null).Should().BeFalse();
    }

    [Fact]
    public void NamedFragment_Equals_SameReference_ReturnsTrue()
    {
        var fragment = new NamedFragmentDefinition("UserParts", "User");

        fragment.Equals(fragment).Should().BeTrue();
    }

    [Fact]
    public void NamedFragment_Equals_SameNameDifferentInstance_ReturnsTrue()
    {
        new NamedFragmentDefinition("UserParts", "User")
            .Equals(new NamedFragmentDefinition("UserParts", "Account"))
            .Should().BeTrue();
    }

    [Fact]
    public void NamedFragment_Equals_DifferentName_ReturnsFalse()
    {
        new NamedFragmentDefinition("UserParts", "User")
            .Equals(new NamedFragmentDefinition("OtherParts", "User"))
            .Should().BeFalse();
    }

    [Fact]
    public void QueryDefinition_Metadata_WithoutMetadata_ReturnsEmptyDictionary()
    {
        var definition = QueryBuilder.CreateDefaultBuilder("Test").AddField("user").Definition;

        definition.Metadata.Should().BeEmpty();
    }
}
