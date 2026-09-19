using System.Text.RegularExpressions;

using FluentAssertions;
using Xunit;

namespace NGql.Tool.Tests;

[Collection(ConsoleCollection.Name)]
public sealed class RenderCommandTests : IDisposable
{
    private const string Snippet = """
        QueryBuilder.CreateDefaultBuilder("GetUsers")
            .AddField("users.name")
        """;

    private const string Expected = """
        query GetUsers{
            users{
                name
            }
        }
        """;

    private readonly CliHarness _cli = new();
    private readonly TempDir _temp = new();

    public void Dispose()
    {
        _cli.Dispose();
        _temp.Dispose();
    }

    [Fact]
    public async Task Main_WithSnippetFile_RendersGraphQlAndExitsZero()
    {
        var path = _temp.Write("snippet.cs", Snippet);

        var result = await _cli.RunAsync(path);

        result.ExitCode.Should().Be(0);
        result.Out.TrimEnd().Should().Be(Expected);
    }

    [Fact]
    public async Task Main_WithDashInput_ReadsSnippetFromStdIn()
    {
        CliHarness.SetStdIn(Snippet);

        var result = await _cli.RunAsync("-");

        result.ExitCode.Should().Be(0);
        result.Out.TrimEnd().Should().Be(Expected);
    }

    [Fact]
    public async Task Main_WithNoArgumentsAndRedirectedStdIn_ReadsSnippetFromStdIn()
    {
        CliHarness.SetStdIn(Snippet);

        var result = await _cli.RunAsync();

        result.ExitCode.Should().Be(0);
        result.Out.TrimEnd().Should().Be(Expected);
    }

    [Fact]
    public async Task Main_WithDashAlongsideOptions_StillReadsSnippetFromStdIn()
    {
        await using var server = new StubGraphQlServer(_ => StubResponse.Json("""{"data":{}}"""));
        CliHarness.SetStdIn(Snippet);

        var result = await _cli.RunAsync("-", "--execute", "--endpoint", server.Url);

        result.ExitCode.Should().Be(0);
        server.LastBody.Should().Contain("query GetUsers");
    }

    [Fact]
    public async Task Main_WithNoArgumentsAndInteractiveStdIn_ReportsNoInput()
    {
        var result = await _cli.RunAsync();

        result.ExitCode.Should().Be(64);
        result.Error.Should().Contain("ngql: no input.");
    }

    [Fact]
    public async Task Main_WithMissingFile_ExitsFileNotFound()
    {
        var missing = Path.Combine(_temp.Path, "nope.cs");

        var result = await _cli.RunAsync(missing);

        result.ExitCode.Should().Be(66);
        result.Error.Should().Contain($"ngql: file not found: {missing}");
    }

    [Fact]
    public async Task Main_WithSnippetThatDoesNotCompile_ExitsRenderFailed()
    {
        var path = _temp.Write("bad.cs", "this is not C#");

        var result = await _cli.RunAsync(path);

        result.ExitCode.Should().Be(1);
        result.Error.Should().Contain($"ngql: failed to render {path}").And.Contain("compile error:");
    }

    [Fact]
    public async Task Main_WithSnippetThatThrows_ExitsRenderFailed()
    {
        var path = _temp.Write("throws.cs", "throw new InvalidOperationException(\"boom\");");

        var result = await _cli.RunAsync(path);

        result.ExitCode.Should().Be(1);
        result.Error.Should().Contain("runtime error: InvalidOperationException: boom");
    }

    [Fact]
    public async Task Main_WithSnippetYieldingNull_ExitsRenderFailed()
    {
        var path = _temp.Write("null.cs", "(object?)null");

        var result = await _cli.RunAsync(path);

        result.ExitCode.Should().Be(1);
        result.Error.Should().Contain("snippet produced null");
    }

    [Fact]
    public async Task Main_WithSnippetWhoseToStringReturnsNull_RendersEmptyOutput()
    {
        var path = _temp.Write("nullish.cs", """
            public class Nullish { public override string ToString() => null!; }
            (object)new Nullish()
            """);

        var result = await _cli.RunAsync(path);

        result.ExitCode.Should().Be(0);
        result.Out.Should().Be("\n");
    }

    [Theory]
    [InlineData("--version")]
    [InlineData("-v")]
    public async Task Main_WithVersionFlag_PrintsSingleSemverLine(string flag)
    {
        var result = await _cli.RunAsync(flag);

        result.ExitCode.Should().Be(0);
        result.Out.TrimEnd().Should().MatchRegex(@"^\d+\.\d+\.\d+(-[0-9A-Za-z.-]+)?$");
    }

    [Theory]
    [InlineData(new[] { "--endpoint" }, "Option 'endpoint' is defined but no value has been provided.")]
    [InlineData(new[] { "--var" }, "Option 'var' is defined but no value has been provided.")]
    [InlineData(new[] { "fixture" }, "Command 'fixture' is missing required argument 'NAME'.")]
    public async Task Main_WithUnparseableArguments_ExitsInvalidUsageWithHelpHint(string[] args, string expected)
    {
        var result = await _cli.RunAsync(args);

        result.ExitCode.Should().Be(64);
        result.Error.Should().Contain($"ngql: {expected}").And.Contain("See `ngql --help`.");
    }

    [Fact]
    public async Task Main_WithExecuteAndNoEndpoint_ExitsInvalidUsage()
    {
        var path = _temp.Write("snippet.cs", Snippet);

        var result = await _cli.RunAsync(path, "--execute");

        result.ExitCode.Should().Be(64);
        result.Error.Should().Contain("ngql: --execute requires --endpoint <url>.");
    }

    [Fact]
    public async Task Main_WithExecuteAndBlankEndpoint_ExitsInvalidUsage()
    {
        var path = _temp.Write("snippet.cs", Snippet);

        var result = await _cli.RunAsync(path, "--execute", "--endpoint", "   ");

        result.ExitCode.Should().Be(64);
        result.Error.Should().Contain("ngql: --execute requires --endpoint <url>.");
    }

    [Fact]
    public async Task Main_WithMutationAndNoOptIn_RefusesAndEchoesOperation()
    {
        var path = _temp.Write("mutation.cs", MutationSnippet);

        var result = await _cli.RunAsync(path, "--execute", "--endpoint", "http://localhost:1/graphql");

        result.ExitCode.Should().Be(4);
        result.Error.Should().Contain("refusing to execute a mutation by default")
            .And.Contain("(Rendered operation:)")
            .And.Contain("mutation AddUser");
    }

    [Fact]
    public async Task Main_WithMutationAndAllowMutations_ProceedsToExecution()
    {
        await using var server = new StubGraphQlServer(_ => StubResponse.Json("""{"data":{"addUser":{"id":"1"}}}"""));
        var path = _temp.Write("mutation.cs", MutationSnippet);

        var result = await _cli.RunAsync(path, "--execute", "--endpoint", server.Url, "--allow-mutations");

        result.ExitCode.Should().Be(0);
        server.LastBody.Should().Contain("mutation AddUser");
    }

    [Theory]
    [InlineData("-H", "no-colon-here", "header must be in 'Name: value' form, got 'no-colon-here'")]
    [InlineData("-H", ": leading-colon", "header must be in 'Name: value' form, got ': leading-colon'")]
    [InlineData("--var", "no-equals-here", "variable must be in 'key=value' form, got 'no-equals-here'")]
    [InlineData("--var", "=leading-equals", "variable must be in 'key=value' form, got '=leading-equals'")]
    public async Task Main_WithMalformedOption_ExitsInvalidUsage(string option, string value, string expectedError)
    {
        var path = _temp.Write("snippet.cs", Snippet);

        var result = await _cli.RunAsync(path, "--execute", "--endpoint", "http://localhost:1/graphql", option, value);

        result.ExitCode.Should().Be(64);
        result.Error.Should().Contain($"ngql: {expectedError}");
    }

    [Fact]
    public async Task Main_WithVersionFlag_OmitsSourceLinkCommitSuffix()
    {
        var result = await _cli.RunAsync("--version");

        result.Out.Should().NotContain("+");
        Regex.Matches(result.Out.TrimEnd(), "\n").Should().BeEmpty();
    }

    private const string MutationSnippet = """
        QueryBuilder.CreateMutationBuilder("AddUser")
            .AddField("addUser", new[] { "id" })
        """;
}
