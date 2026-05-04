using FluentAssertions;
using NGql.Core.Builders;
using Xunit;

namespace NGql.Core.Tests.Builders;

public class NamedFragmentRenderingTests
{
    [Fact]
    public void Single_Fragment_With_Spread_Renders_Definition_After_Operation()
    {
        var qb = QueryBuilder.CreateDefaultBuilder("GetUser")
            .AddFragment("UserSummary", "User", f => f
                .AddField("id")
                .AddField("name")
                .AddField("avatarUrl"))
            .AddField("user", u => u.SpreadFragment("UserSummary"));

        qb.ToString().Should().Be(
            "query GetUser{\n" +
            "    user{\n" +
            "        ...UserSummary\n" +
            "    }\n" +
            "}\n" +
            "fragment UserSummary on User{\n" +
            "    avatarUrl\n" +
            "    id\n" +
            "    name\n" +
            "}\n");
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

        // Definitions sorted alphabetically — Alpha before Mid before Zebra
        rendered.IndexOf("fragment AlphaFields", System.StringComparison.Ordinal)
            .Should().BeLessThan(rendered.IndexOf("fragment MidFields", System.StringComparison.Ordinal));
        rendered.IndexOf("fragment MidFields", System.StringComparison.Ordinal)
            .Should().BeLessThan(rendered.IndexOf("fragment ZebraFields", System.StringComparison.Ordinal));
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
        rendered.IndexOf("...UserSummary", System.StringComparison.Ordinal)
            .Should().BeLessThan(rendered.IndexOf("...AuditFields", System.StringComparison.Ordinal));
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
        rendered.IndexOf("__typename", System.StringComparison.Ordinal)
            .Should().BeLessThan(rendered.IndexOf("...UserSummary", System.StringComparison.Ordinal));
    }

    [Fact]
    public void Spread_Inside_InlineFragment_Renders_With_Type_Narrowing()
    {
        var qb = QueryBuilder.CreateDefaultBuilder("Search")
            .AddFragment("RepoCard", "Repository", f => f
                .AddField("name")
                .AddField("stargazerCount"))
            .AddField("nodes", n => n
                .OnType("Repository", r => r.SpreadFragment("RepoCard"))
                .OnType("Issue", i => i.AddField("title")));

        qb.ToString().Should().Be(
            "query Search{\n" +
            "    nodes{\n" +
            "        ... on Issue{\n" +
            "            title\n" +
            "        }\n" +
            "        ... on Repository{\n" +
            "            ...RepoCard\n" +
            "        }\n" +
            "    }\n" +
            "}\n" +
            "fragment RepoCard on Repository{\n" +
            "    name\n" +
            "    stargazerCount\n" +
            "}\n");
    }

    [Fact]
    public void Fragment_Spread_Inside_Another_Fragment_Renders_Correctly()
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

        qb.ToString().Should().Be(
            "query GetUser{\n" +
            "    user{\n" +
            "        ...UserCard\n" +
            "    }\n" +
            "}\n" +
            "fragment AuditFields on User{\n" +
            "    createdAt\n" +
            "    updatedAt\n" +
            "}\n" +
            "fragment UserCard on User{\n" +
            "    id\n" +
            "    name\n" +
            "    ...AuditFields\n" +
            "}\n");
    }

    [Fact]
    public void Fragment_With_Nested_InlineFragments_Renders_Both()
    {
        var qb = QueryBuilder.CreateDefaultBuilder("Search")
            .AddFragment("SearchResult", "Node", f => f
                .AddField("__typename")
                .OnType("Repository", r => r.AddField("stargazerCount"))
                .OnType("Issue", i => i.AddField("title")))
            .AddField("nodes", n => n.SpreadFragment("SearchResult"));

        qb.ToString().Should().Be(
            "query Search{\n" +
            "    nodes{\n" +
            "        ...SearchResult\n" +
            "    }\n" +
            "}\n" +
            "fragment SearchResult on Node{\n" +
            "    __typename\n" +
            "    ... on Issue{\n" +
            "        title\n" +
            "    }\n" +
            "    ... on Repository{\n" +
            "        stargazerCount\n" +
            "    }\n" +
            "}\n");
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
        while ((idx = haystack.IndexOf(needle, idx, System.StringComparison.Ordinal)) >= 0)
        {
            count++;
            idx += needle.Length;
        }
        return count;
    }
}
