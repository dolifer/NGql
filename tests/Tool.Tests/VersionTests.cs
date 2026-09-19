using System.Reflection;
using System.Reflection.Emit;

using FluentAssertions;
using Xunit;

namespace NGql.Tool.Tests;

public sealed class VersionTests
{
    [Fact]
    public void GetVersion_WithInformationalVersion_StripsTheSourceLinkSuffix()
    {
        var asm = Build("2.3.4", informational: "2.3.4-preview.7+abcdef0");

        Program.GetVersion(asm).Should().Be("2.3.4-preview.7");
    }

    [Fact]
    public void GetVersion_WithInformationalVersionAndNoSuffix_ReturnsItVerbatim()
    {
        var asm = Build("2.3.4", informational: "2.3.4-preview.7");

        Program.GetVersion(asm).Should().Be("2.3.4-preview.7");
    }

    [Fact]
    public void GetVersion_WithoutInformationalVersion_FallsBackToTheAssemblyVersion()
    {
        var asm = Build("2.3.4.0", informational: null);

        Program.GetVersion(asm).Should().Be("2.3.4.0");
    }

    [Fact]
    public void GetVersion_ForTheRunningTool_ReturnsASingleSemverLine()
    {
        Program.GetVersion(typeof(Program).Assembly).Should().MatchRegex(@"^\d+\.\d+\.\d+");
    }

    private static Assembly Build(string version, string? informational)
    {
        var name = new AssemblyName("NGql.Tool.VersionProbe." + Guid.NewGuid().ToString("N"))
        {
            Version = Version.Parse(version),
        };
        var asm = AssemblyBuilder.DefineDynamicAssembly(name, AssemblyBuilderAccess.Run);

        if (informational is not null)
        {
            var ctor = typeof(AssemblyInformationalVersionAttribute).GetConstructor([typeof(string)])!;
            asm.SetCustomAttribute(new CustomAttributeBuilder(ctor, [informational]));
        }

        return asm;
    }
}
