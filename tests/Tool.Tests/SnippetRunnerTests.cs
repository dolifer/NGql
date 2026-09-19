using FluentAssertions;
using Xunit;

namespace NGql.Tool.Tests;

public sealed class SnippetRunnerTests
{
    [Theory]
    [InlineData("mutation AddUser{\n}", true)]
    [InlineData("\n  mutation AddUser{\n}", true)]
    [InlineData("query GetUsers{\n}", false)]
    [InlineData("mutationish GetUsers{\n}", false)]
    [InlineData("", false)]
    public void IsMutation_ForRenderedOperation_DetectsTheMutationKeyword(string rendered, bool expected)
    {
        SnippetRunner.IsMutation(rendered).Should().Be(expected);
    }

    [Fact]
    public async Task CompileAndRun_WithValidSnippet_ReturnsRenderedOutputAndNoError()
    {
        var result = await SnippetRunner.CompileAndRun("""QueryBuilder.CreateDefaultBuilder("S").AddField("a.b")""");

        result.Ok.Should().BeTrue();
        result.Output.Should().Contain("query S{");
        result.Error.Should().BeEmpty();
    }

    [Fact]
    public async Task CompileAndRun_WithInvalidSnippet_ReturnsErrorAndNoOutput()
    {
        var result = await SnippetRunner.CompileAndRun("this is not C#");

        result.Ok.Should().BeFalse();
        result.Output.Should().BeEmpty();
        result.Error.Should().StartWith("compile error:");
    }
}
