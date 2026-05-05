using System;
using System.Threading.Tasks;
using FluentAssertions;
using NGql.Core.Builders;
using NGql.Core.Tests.Extensions;
using Xunit;

namespace NGql.Core.Tests.Builders;

public class NamedFragmentRenderingTests
{
    [Fact]
    public async Task Single_Fragment_With_Spread_Renders_Definition_After_Operation()
    {
        var qb = QueryBuilder.CreateDefaultBuilder("GetUser")
            .AddFragment("UserSummary", "User", f => f
                .AddField("id")
                .AddField("name")
                .AddField("avatarUrl"))
            .AddField("user", u => u.SpreadFragment("UserSummary"));

        await qb.Verify();
    }

    [Fact]
    public void Multiple_Fragments_Render_Sorted_Alphabetically_By_Name()
    {
        var qb = QueryBuilder.CreateDefaultBuilder("GetData")
            .AddFragment("ZebraFields", "Zebra", f => f.AddField("stripes"))
            .AddFragment("AlphaFields", "Alpha", f => f.AddField("first"))
            .AddFragment("MidFields", "Mid", f => f.AddField("middle"))
            .AddField("data", d => d
                .SpreadFragment("AlphaFields")
                .SpreadFragment("MidFields")
                .SpreadFragment("ZebraFields"));

        var rendered = qb.ToString();

        // Definitions sorted alphabetically — Alpha before Mid before Zebra.
        rendered.IndexOf("fragment AlphaFields", StringComparison.Ordinal)
            .Should().BeLessThan(rendered.IndexOf("fragment MidFields", StringComparison.Ordinal));
        rendered.IndexOf("fragment MidFields", StringComparison.Ordinal)
            .Should().BeLessThan(rendered.IndexOf("fragment ZebraFields", StringComparison.Ordinal));
    }

    [Fact]
    public void Spread_Order_Preserved_From_Declaration()
    {
        var qb = QueryBuilder.CreateDefaultBuilder("GetUser")
            .AddFragment("AuditFields", "User", f => f.AddField("createdAt"))
            .AddFragment("UserSummary", "User", f => f.AddField("id"))
            .AddField("user", u => u
                .SpreadFragment("UserSummary")
                .SpreadFragment("AuditFields"));

        var rendered = qb.ToString();

        // Spreads keep declaration order even though their definitions are sorted.
        rendered.IndexOf("...UserSummary", StringComparison.Ordinal)
            .Should().BeLessThan(rendered.IndexOf("...AuditFields", StringComparison.Ordinal));
    }

    [Fact]
    public void Spread_Coexists_With_Plain_Fields_In_Same_Selection_Set()
    {
        var qb = QueryBuilder.CreateDefaultBuilder("GetUser")
            .AddFragment("UserSummary", "User", f => f.AddField("id").AddField("name"))
            .AddField("user", u => u
                .AddField("__typename")
                .SpreadFragment("UserSummary"));

        var rendered = qb.ToString();

        rendered.Should().Contain("__typename");
        rendered.Should().Contain("...UserSummary");
        // Plain fields come before spreads in the output (spreads render after fields and inline fragments).
        rendered.IndexOf("__typename", StringComparison.Ordinal)
            .Should().BeLessThan(rendered.IndexOf("...UserSummary", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Spread_Inside_InlineFragment_Renders_With_Type_Narrowing()
    {
        var qb = QueryBuilder.CreateDefaultBuilder("Search")
            .AddFragment("RepoCard", "Repository", f => f
                .AddField("name")
                .AddField("stargazerCount"))
            .AddField("nodes", n => n
                .OnType("Repository", r => r.SpreadFragment("RepoCard"))
                .OnType("Issue", i => i.AddField("title")));

        await qb.Verify();
    }

    [Fact]
    public async Task Fragment_Spread_Inside_Another_Fragment_Renders_Correctly()
    {
        var qb = QueryBuilder.CreateDefaultBuilder("GetUser")
            .AddFragment("AuditFields", "User", f => f
                .AddField("createdAt")
                .AddField("updatedAt"))
            .AddFragment("UserCard", "User", f => f
                .AddField("id")
                .AddField("name")
                .SpreadFragment("AuditFields"))
            .AddField("user", u => u.SpreadFragment("UserCard"));

        await qb.Verify();
    }

    [Fact]
    public async Task Fragment_With_Nested_InlineFragments_Renders_Both()
    {
        var qb = QueryBuilder.CreateDefaultBuilder("Search")
            .AddFragment("SearchResult", "Node", f => f
                .AddField("__typename")
                .OnType("Repository", r => r.AddField("stargazerCount"))
                .OnType("Issue", i => i.AddField("title")))
            .AddField("nodes", n => n.SpreadFragment("SearchResult"));

        await qb.Verify();
    }

    [Fact]
    public void Fragment_Without_Any_Spread_Still_Renders_Definition()
    {
        // Edge case: declaring an unused fragment should still render its definition. NGql
        // doesn't validate spread coverage; the server (or a linter) catches dead fragments.
        var qb = QueryBuilder.CreateDefaultBuilder("GetUser")
            .AddFragment("Unused", "User", f => f.AddField("id"))
            .AddField("user", u => u.AddField("name"));

        var rendered = qb.ToString();

        rendered.Should().Contain("fragment Unused on User");
        rendered.Should().Contain("name");
        rendered.Should().NotContain("...Unused");
    }

    [Fact]
    public void Spread_Without_Fragment_Definition_Renders_Verbatim()
    {
        // NGql is schemaless — spreading an undeclared fragment renders the spread; the
        // server rejects it with a clear error. Documents this contract.
        var qb = QueryBuilder.CreateDefaultBuilder("GetUser")
            .AddField("user", u => u.SpreadFragment("DoesNotExist"));

        var rendered = qb.ToString();

        rendered.Should().Contain("...DoesNotExist");
        rendered.Should().NotContain("fragment DoesNotExist");
    }

    [Fact]
    public void Mutation_With_Named_Fragment_Renders_Mutation_Prefix()
    {
        var qb = QueryBuilder.CreateMutationBuilder("CreateUser")
            .AddFragment("UserResult", "User", f => f.AddField("id").AddField("name"))
            .AddField("createUser", c => c.SpreadFragment("UserResult"));

        var rendered = qb.ToString();

        rendered.Should().StartWith("mutation CreateUser{");
        rendered.Should().Contain("fragment UserResult on User");
    }

    [Fact]
    public void Same_Fragment_Spread_At_Multiple_Sites_Defines_Fragment_Once()
    {
        var qb = QueryBuilder.CreateDefaultBuilder("GetUsers")
            .AddFragment("UserSummary", "User", f => f.AddField("id").AddField("name"))
            .AddField("users", u => u.SpreadFragment("UserSummary"))
            .AddField("admins", a => a.SpreadFragment("UserSummary"));

        var rendered = qb.ToString();

        // Definition appears exactly once, spread appears at both call sites.
        var defCount = CountOccurrences(rendered, "fragment UserSummary");
        var spreadCount = CountOccurrences(rendered, "...UserSummary");
        defCount.Should().Be(1);
        spreadCount.Should().Be(2);
    }

    private static int CountOccurrences(string haystack, string needle)
    {
        int count = 0, idx = 0;
        while ((idx = haystack.IndexOf(needle, idx, StringComparison.Ordinal)) >= 0)
        {
            count++;
            idx += needle.Length;
        }
        return count;
    }
}
