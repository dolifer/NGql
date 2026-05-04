using System;
using FluentAssertions;
using NGql.Core.Abstractions;
using Xunit;

namespace NGql.Core.Tests.Abstractions;

public class NamedFragmentDefinitionTests
{
    [Fact]
    public void Constructor_With_Valid_Name_And_Type_Sets_Properties()
    {
        var fragment = new NamedFragmentDefinition("UserSummary", "User");

        fragment.Name.Should().Be("UserSummary");
        fragment.OnType.Should().Be("User");
        fragment.Fields.Should().BeEmpty();
        fragment.InlineFragments.Should().BeEmpty();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_With_Invalid_Name_Throws(string? name)
    {
        var act = () => new NamedFragmentDefinition(name!, "User");

        act.Should().Throw<ArgumentException>().WithParameterName("name");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_With_Invalid_OnType_Throws(string? onType)
    {
        var act = () => new NamedFragmentDefinition("UserSummary", onType!);

        act.Should().Throw<ArgumentException>().WithParameterName("onType");
    }

    [Fact]
    public void Equality_Compares_By_Name_Only()
    {
        var a = new NamedFragmentDefinition("UserSummary", "User");
        var b = new NamedFragmentDefinition("UserSummary", "Admin");
        var c = new NamedFragmentDefinition("RepoCard", "User");

        a.Should().Be(b);
        a.GetHashCode().Should().Be(b.GetHashCode());
        a.Should().NotBe(c);
    }

    [Fact]
    public void Equality_Is_Case_Sensitive()
    {
        var a = new NamedFragmentDefinition("UserSummary", "User");
        var b = new NamedFragmentDefinition("usersummary", "User");

        a.Should().NotBe(b);
    }
}

public class QueryDefinition_NamedFragments_Tests
{
    [Fact]
    public void NamedFragments_Default_Is_Empty()
    {
        var query = new QueryDefinition("GetUser");

        query.NamedFragments.Should().BeEmpty();
    }

    [Fact]
    public void GetOrAddNamedFragment_New_Name_Creates_And_Returns()
    {
        var query = new QueryDefinition("GetUser");

        var fragment = query.GetOrAddNamedFragment("UserSummary", "User");

        fragment.Name.Should().Be("UserSummary");
        fragment.OnType.Should().Be("User");
        query.NamedFragments.Should().ContainKey("UserSummary");
        query.NamedFragments["UserSummary"].Should().BeSameAs(fragment);
    }

    [Fact]
    public void GetOrAddNamedFragment_Existing_Name_Same_Type_Returns_Existing()
    {
        var query = new QueryDefinition("GetUser");
        var first = query.GetOrAddNamedFragment("UserSummary", "User");

        var second = query.GetOrAddNamedFragment("UserSummary", "User");

        second.Should().BeSameAs(first);
        query.NamedFragments.Should().HaveCount(1);
    }

    [Fact]
    public void GetOrAddNamedFragment_Existing_Name_Different_Type_Throws()
    {
        var query = new QueryDefinition("GetUser");
        query.GetOrAddNamedFragment("UserSummary", "User");

        var act = () => query.GetOrAddNamedFragment("UserSummary", "Admin");

        act.Should().Throw<InvalidOperationException>()
           .WithMessage("*UserSummary*User*Admin*");
    }
}

public class FieldDefinition_SpreadFragments_Tests
{
    [Fact]
    public void SpreadFragments_Default_Is_Empty()
    {
        var field = new FieldDefinition("user");

        field.SpreadFragments.Should().BeEmpty();
    }

    [Fact]
    public void AddSpreadFragment_Adds_To_List_In_Declaration_Order()
    {
        var field = new FieldDefinition("user");

        field.AddSpreadFragment("UserSummary");
        field.AddSpreadFragment("AuditFields");
        field.AddSpreadFragment("Permissions");

        field.SpreadFragments.Should().ContainInOrder("UserSummary", "AuditFields", "Permissions");
    }

    [Fact]
    public void AddSpreadFragment_Same_Name_Twice_Is_Deduped()
    {
        var field = new FieldDefinition("user");

        field.AddSpreadFragment("UserSummary");
        field.AddSpreadFragment("UserSummary");

        field.SpreadFragments.Should().ContainSingle().Which.Should().Be("UserSummary");
    }

    [Fact]
    public void AddSpreadFragment_Is_Case_Sensitive_Per_GraphQL_Spec()
    {
        var field = new FieldDefinition("user");

        field.AddSpreadFragment("UserSummary");
        field.AddSpreadFragment("usersummary");

        field.SpreadFragments.Should().HaveCount(2);
    }
}
