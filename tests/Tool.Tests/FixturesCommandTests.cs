using FluentAssertions;
using Xunit;

namespace NGql.Tool.Tests;

[Collection(ConsoleCollection.Name)]
public sealed class FixturesCommandTests : IDisposable
{
    private readonly CliHarness _cli = new();
    private readonly TempDir _temp = new();

    public FixturesCommandTests() => CliHarness.SetBaseDirectory(RepoFixtures.ToolProjectDir);

    public void Dispose()
    {
        _cli.Dispose();
        _temp.Dispose();
    }

    [Fact]
    public async Task Fixtures_AgainstShippedFixtures_ReportsAllPassing()
    {
        var result = await _cli.RunAsync("fixtures");

        result.ExitCode.Should().Be(0);
        result.Out.Should().Contain($"{RepoFixtures.SnippetCount} passed, 0 failed");
    }

    [Fact]
    public async Task Fixtures_AgainstShippedFixtures_PrintsOnePassLinePerFixture()
    {
        var result = await _cli.RunAsync("fixtures");

        foreach (var name in RepoFixtures.Names)
            result.Out.Should().Contain($"PASS  {name}");
    }

    [Theory]
    [MemberData(nameof(FixtureNames))]
    public async Task Fixture_ForEachShippedFixture_RendersTheExpectedGraphQl(string name)
    {
        var result = await _cli.RunAsync("fixture", name);

        result.ExitCode.Should().Be(0);
        result.Out.Should().Contain($"PASS  {name}");
    }

    public static TheoryData<string> FixtureNames()
    {
        var data = new TheoryData<string>();
        foreach (var name in RepoFixtures.Names)
            data.Add(name);
        return data;
    }

    [Fact]
    public async Task Fixture_WithUnknownName_ExitsFileNotFound()
    {
        var result = await _cli.RunAsync("fixture", "no_such_fixture");

        result.ExitCode.Should().Be(66);
        result.Error.Should().Contain("fixture not found:").And.Contain("no_such_fixture.snippet");
    }

    [Fact]
    public async Task Fixtures_WithMissingFixturesDir_ExitsFileNotFound()
    {
        CliHarness.SetBaseDirectory(Path.Combine(_temp.Path, "nowhere"));

        var result = await _cli.RunAsync("fixtures");

        result.ExitCode.Should().Be(66);
        result.Error.Should().Contain("fixtures dir not found:");
    }

    [Fact]
    public async Task Fixtures_WithEmptyFixturesDir_ReportsNoFixtures()
    {
        _temp.Dir("Fixtures");
        CliHarness.SetBaseDirectory(_temp.Path);

        var result = await _cli.RunAsync("fixtures");

        result.ExitCode.Should().Be(0);
        result.Out.Should().Contain("(no fixtures found under");
    }

    [Fact]
    public async Task Fixtures_WithSnippetLackingExpectedFile_ReportsSmoke()
    {
        _temp.Write("Fixtures/smoke.snippet", "QueryBuilder.CreateDefaultBuilder(\"S\").AddField(\"a.b\")");
        CliHarness.SetBaseDirectory(_temp.Path);

        var result = await _cli.RunAsync("fixtures");

        result.ExitCode.Should().Be(0);
        result.Out.Should().Contain("SMOKE smoke  (no expected file; output below)")
            .And.Contain("      query S{")
            .And.Contain("1 passed, 0 failed");
    }

    [Fact]
    public async Task Fixtures_WithOutputMismatch_ReportsFailWithDiff()
    {
        _temp.Write("Fixtures/mismatch.snippet", "QueryBuilder.CreateDefaultBuilder(\"S\").AddField(\"a.b\")");
        _temp.Write("Fixtures/mismatch.expected", "query Other{\n}\n");
        CliHarness.SetBaseDirectory(_temp.Path);

        var result = await _cli.RunAsync("fixtures");

        result.ExitCode.Should().Be(1);
        result.Out.Should().Contain("FAIL  mismatch  (output mismatch)")
            .And.Contain("--- expected ---")
            .And.Contain("--- actual   ---")
            .And.Contain("0 passed, 1 failed");
    }

    [Fact]
    public async Task Fixture_WithCompileError_ReportsFailAndExitsRenderFailed()
    {
        _temp.Write("Fixtures/broken.snippet", "this is not C#");
        CliHarness.SetBaseDirectory(_temp.Path);

        var result = await _cli.RunAsync("fixture", "broken");

        result.ExitCode.Should().Be(1);
        result.Out.Should().Contain("FAIL  broken  (compile/run error)").And.Contain("compile error:");
    }

    [Fact]
    public async Task Fixture_WithSnippetLackingExpectedFile_ReportsSmokeAndExitsZero()
    {
        _temp.Write("Fixtures/solo.snippet", "QueryBuilder.CreateDefaultBuilder(\"S\").AddField(\"a.b\")");
        CliHarness.SetBaseDirectory(_temp.Path);

        var result = await _cli.RunAsync("fixture", "solo");

        result.ExitCode.Should().Be(0);
        result.Out.Should().Contain("SMOKE solo");
    }
}
