using System.ComponentModel;
using System.Diagnostics;

using Spectre.Console.Cli;

namespace NGql.Tool;

/// <summary>
/// Internal: runs every Fixtures/*.snippet, compares against its .expected, prints PASS/FAIL.
/// Used by `make skill-eval` for regression testing inside the NGql repo. Hidden from --help.
/// </summary>
internal sealed class FixturesCommand : AsyncCommand<FixturesCommand.Settings>
{
    public sealed class Settings : CommandSettings { }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        var fixturesDir = FixtureSupport.ResolveFixturesDir();
        if (!Directory.Exists(fixturesDir))
        {
            await Console.Error.WriteLineAsync($"fixtures dir not found: {fixturesDir}");
            return ExitCodes.FileNotFound;
        }

        var snippets = Directory.GetFiles(fixturesDir, "*.snippet").OrderBy(p => p).ToList();
        if (snippets.Count == 0)
        {
            Console.WriteLine($"(no fixtures found under {fixturesDir})");
            return ExitCodes.Ok;
        }

        var sw = Stopwatch.StartNew();
        var pass = 0;
        var fail = 0;
        foreach (var snippetPath in snippets)
        {
            var name = Path.GetFileNameWithoutExtension(snippetPath);
            var expectedPath = Path.Combine(fixturesDir, $"{name}.expected");
            if (await FixtureSupport.RunFixture(snippetPath, expectedPath))
                pass++;
            else
                fail++;
        }

        sw.Stop();
        Console.WriteLine();
        Console.WriteLine($"{pass} passed, {fail} failed  ({sw.ElapsedMilliseconds} ms)");
        return fail == 0 ? ExitCodes.Ok : ExitCodes.RenderFailed;
    }
}

/// <summary>
/// Internal: runs a single named fixture by basename (without extension).
/// Used by contributors to debug a specific fixture. Hidden from --help.
/// </summary>
internal sealed class FixtureCommand : AsyncCommand<FixtureCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<NAME>")]
        [Description("Fixture basename (without extension).")]
        public string Name { get; init; } = string.Empty;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings)
    {
        var fixturesDir = FixtureSupport.ResolveFixturesDir();
        var snippetPath  = Path.Combine(fixturesDir, $"{settings.Name}.snippet");
        var expectedPath = Path.Combine(fixturesDir, $"{settings.Name}.expected");
        if (!File.Exists(snippetPath))
        {
            await Console.Error.WriteLineAsync($"fixture not found: {snippetPath}");
            return ExitCodes.FileNotFound;
        }

        return await FixtureSupport.RunFixture(snippetPath, expectedPath)
            ? ExitCodes.Ok
            : ExitCodes.RenderFailed;
    }
}

internal static class FixtureSupport
{
    public static async Task<bool> RunFixture(string snippetPath, string expectedPath)
    {
        var name = Path.GetFileNameWithoutExtension(snippetPath);
        var snippet = await File.ReadAllTextAsync(snippetPath);
        var (ok, output, error) = await SnippetRunner.CompileAndRun(snippet);

        if (!ok)
        {
            Console.WriteLine($"FAIL  {name}  (compile/run error)");
            foreach (var line in (error ?? string.Empty).Split('\n'))
                Console.WriteLine($"      {line}");
            return false;
        }

        if (!File.Exists(expectedPath))
        {
            Console.WriteLine($"SMOKE {name}  (no expected file; output below)");
            foreach (var line in (output ?? string.Empty).Split('\n'))
                Console.WriteLine($"      {line}");
            return true;
        }

        var expected = (await File.ReadAllTextAsync(expectedPath)).Replace("\r\n", "\n").TrimEnd();
        var actual = (output ?? string.Empty).Replace("\r\n", "\n").TrimEnd();

        if (expected == actual)
        {
            Console.WriteLine($"PASS  {name}");
            return true;
        }

        Console.WriteLine($"FAIL  {name}  (output mismatch)");
        Console.WriteLine($"      --- expected ---");
        foreach (var line in expected.Split('\n'))
            Console.WriteLine($"      {line}");
        Console.WriteLine($"      --- actual   ---");
        foreach (var line in actual.Split('\n'))
            Console.WriteLine($"      {line}");
        return false;
    }

    public static string ResolveFixturesDir()
    {
        // When running via `dotnet run`, BaseDirectory is bin/Debug/...; fall back to a
        // project-relative Fixtures dir found by walking up to Tool.csproj.
        var local = Path.Combine(AppContext.BaseDirectory, "Fixtures");
        if (Directory.Exists(local))
            return local;

        var dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "Tool.csproj")))
            dir = Path.GetDirectoryName(dir);

        return dir is null ? local : Path.Combine(dir, "Fixtures");
    }
}
