using FluentAssertions;
using Xunit;

namespace NGql.Tool.Tests;

[Collection(ConsoleCollection.Name)]
public sealed class ToolEnvironmentTests
{
    [Fact]
    public void BaseDirectory_ByDefault_ReturnsTheAppContextBaseDirectory()
    {
        ToolEnvironment.BaseDirectory().Should().Be(AppContext.BaseDirectory);
    }

    [Fact]
    public void IsInputRedirected_ByDefault_MatchesTheConsoleState()
    {
        ToolEnvironment.IsInputRedirected().Should().Be(Console.IsInputRedirected);
    }
}
