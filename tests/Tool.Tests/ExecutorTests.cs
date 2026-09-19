using System.Net.Sockets;
using System.Text.Json;

using FluentAssertions;
using Xunit;

namespace NGql.Tool.Tests;

[Collection(ConsoleCollection.Name)]
public sealed class ExecutorTests : IDisposable
{
    private const string Snippet = """
        QueryBuilder.CreateDefaultBuilder("GetUsers")
            .AddField("users.name")
        """;

    private readonly CliHarness _cli = new();
    private readonly TempDir _temp = new();

    public void Dispose()
    {
        _cli.Dispose();
        _temp.Dispose();
    }

    private Task<CliResult> ExecuteAsync(StubGraphQlServer server, params string[] extra) =>
        _cli.RunAsync([_temp.Write("snippet.cs", Snippet), "--execute", "--endpoint", server.Url, .. extra]);

    [Fact]
    public async Task Execute_WithDataResponse_PrintsJsonAndExitsZero()
    {
        await using var server = new StubGraphQlServer(_ => StubResponse.Json("""{"data":{"users":[{"name":"ada"}]}}"""));

        var result = await ExecuteAsync(server);

        result.ExitCode.Should().Be(0);
        result.Out.Should().Contain("users").And.Contain("ada");
    }

    [Fact]
    public async Task Execute_WithDataResponse_PostsQueryInBody()
    {
        await using var server = new StubGraphQlServer(_ => StubResponse.Json("""{"data":{}}"""));

        await ExecuteAsync(server);

        using var body = JsonDocument.Parse(server.LastBody!);
        body.RootElement.GetProperty("query").GetString().Should().Contain("query GetUsers");
        body.RootElement.TryGetProperty("variables", out _).Should().BeFalse();
    }

    [Fact]
    public async Task Execute_WithVariables_PostsJsonTypedVariables()
    {
        await using var server = new StubGraphQlServer(_ => StubResponse.Json("""{"data":{}}"""));

        await ExecuteAsync(
            server,
            "--var", "count=42",
            "--var", "ratio=1.5",
            "--var", "active=true",
            "--var", "archived=false",
            "--var", "missing=null",
            "--var", "tags=[\"a\",\"b\"]",
            "--var", "filter={\"id\":7}",
            "--var", "quoted=\"alice\"",
            "--var", "bare=alice");

        using var body = JsonDocument.Parse(server.LastBody!);
        var vars = body.RootElement.GetProperty("variables");
        vars.GetProperty("count").GetInt64().Should().Be(42);
        vars.GetProperty("ratio").GetDouble().Should().Be(1.5);
        vars.GetProperty("active").GetBoolean().Should().BeTrue();
        vars.GetProperty("archived").GetBoolean().Should().BeFalse();
        vars.GetProperty("missing").ValueKind.Should().Be(JsonValueKind.Null);
        vars.GetProperty("tags").EnumerateArray().Select(e => e.GetString()).Should().Equal("a", "b");
        vars.GetProperty("filter").GetProperty("id").GetInt64().Should().Be(7);
        vars.GetProperty("quoted").GetString().Should().Be("alice");
        vars.GetProperty("bare").GetString().Should().Be("alice");
    }

    [Fact]
    public async Task Execute_WithHeaders_SendsRequestAndContentHeaders()
    {
        await using var server = new StubGraphQlServer(_ => StubResponse.Json("""{"data":{}}"""));

        var result = await ExecuteAsync(
            server,
            "-H", "Authorization: Bearer token-123",
            "-H", "Content-Language: en");

        result.ExitCode.Should().Be(0);
        server.LastHeaders["Authorization"].Should().Be("Bearer token-123");
        server.LastHeaders["Content-Language"].Should().Be("en");
    }

    [Fact]
    public async Task Execute_WithGraphQlErrors_ExitsGraphQlErrors()
    {
        await using var server = new StubGraphQlServer(_ =>
            StubResponse.Json("""{"errors":[{"message":"field unknown"}]}"""));

        var result = await ExecuteAsync(server);

        result.ExitCode.Should().Be(2);
        result.Out.Should().Contain("field unknown");
    }

    [Theory]
    [InlineData("""{"data":{},"errors":[]}""")]
    [InlineData("""{"data":{},"errors":"not-an-array"}""")]
    [InlineData("""[{"data":{}}]""")]
    public async Task Execute_WithoutUsableErrorsArray_ExitsZero(string body)
    {
        await using var server = new StubGraphQlServer(_ => StubResponse.Json(body));

        var result = await ExecuteAsync(server);

        result.ExitCode.Should().Be(0);
    }

    [Fact]
    public async Task Execute_WithNonJsonBody_PrintsRawBody()
    {
        await using var server = new StubGraphQlServer(_ => StubResponse.Text("<html>not json</html>"));

        var result = await ExecuteAsync(server);

        result.ExitCode.Should().Be(0);
        result.Out.Should().Contain("<html>not json</html>");
    }

    [Fact]
    public async Task Execute_WithServerError_ExitsHttpFailure()
    {
        await using var server = new StubGraphQlServer(_ => StubResponse.Status(500, "boom"));

        var result = await ExecuteAsync(server);

        result.ExitCode.Should().Be(3);
        result.Error.Should().Contain("ngql: HTTP 500");
        result.Out.Should().Contain("boom");
    }

    [Fact]
    public async Task Execute_WithUnreachableEndpoint_ExitsHttpFailure()
    {
        var path = _temp.Write("snippet.cs", Snippet);
        var port = FreePort();

        var result = await _cli.RunAsync(path, "--execute", "--endpoint", $"http://127.0.0.1:{port}/graphql");

        result.ExitCode.Should().Be(3);
        result.Error.Should().Contain("ngql: HTTP request failed:");
    }

    private static int FreePort()
    {
        var listener = new TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        var port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
