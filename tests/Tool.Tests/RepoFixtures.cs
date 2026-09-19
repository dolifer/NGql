namespace NGql.Tool.Tests;

/// <summary>
/// Locates the repo's real <c>tools/Tool/Fixtures</c> so the shipped fixtures are exercised
/// through the same discovery contract the CLI uses (a directory containing Tool.csproj).
/// </summary>
internal static class RepoFixtures
{
    public static string ToolProjectDir { get; } = Locate();

    public static string Dir { get; } = Path.Combine(ToolProjectDir, "Fixtures");

    public static IReadOnlyList<string> Names { get; } = Directory
        .GetFiles(Dir, "*.snippet")
        .Select(Path.GetFileNameWithoutExtension)
        .OfType<string>()
        .OrderBy(n => n, StringComparer.Ordinal)
        .ToArray();

    public static int SnippetCount => Names.Count;

    private static string Locate()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "NGql.sln")))
            dir = Path.GetDirectoryName(dir);

        if (dir is null)
            throw new InvalidOperationException("could not locate the repository root from " + AppContext.BaseDirectory);

        return Path.Combine(dir, "tools", "Tool");
    }
}
