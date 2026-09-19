using FluentAssertions;
using Xunit;

namespace NGql.Tool.Tests;

[Collection(ConsoleCollection.Name)]
public sealed class FixtureSupportTests : IDisposable
{
    private readonly TempDir _temp = new();

    public void Dispose() => _temp.Dispose();

    [Fact]
    public void ResolveFixturesDir_WithLocalFixturesDir_ReturnsIt()
    {
        var expected = _temp.Dir("Fixtures");

        FixtureSupport.ResolveFixturesDir(_temp.Path).Should().Be(expected);
    }

    [Fact]
    public void ResolveFixturesDir_WithoutLocalDir_WalksUpToToolProject()
    {
        _temp.Write("Tool.csproj", "<Project />");
        var nested = _temp.Dir(Path.Combine("bin", "Release", "net10.0"));

        FixtureSupport.ResolveFixturesDir(nested).Should().Be(Path.Combine(_temp.Path, "Fixtures"));
    }

    [Fact]
    public void ResolveFixturesDir_WithNoToolProjectAnywhere_FallsBackToLocalPath()
    {
        var orphan = _temp.Dir("orphan");

        FixtureSupport.ResolveFixturesDir(orphan).Should().Be(Path.Combine(orphan, "Fixtures"));
    }

    [Fact]
    public void ResolveFixturesDir_WithoutArgument_UsesTheEnvironmentBaseDirectory()
    {
        var original = ToolEnvironment.BaseDirectory;
        try
        {
            var expected = _temp.Dir("Fixtures");
            ToolEnvironment.BaseDirectory = () => _temp.Path;

            FixtureSupport.ResolveFixturesDir().Should().Be(expected);
        }
        finally
        {
            ToolEnvironment.BaseDirectory = original;
        }
    }
}
