namespace NGql.Tool;

/// <summary>
/// Process-level seams the CLI reads instead of touching <see cref="Console"/> and
/// <see cref="AppContext"/> directly. Defaults reproduce the real environment; tests swap them
/// so the stdin-redirection and fixture-discovery branches stay reachable.
/// </summary>
internal static class ToolEnvironment
{
    public static Func<bool> IsInputRedirected { get; set; } = () => Console.IsInputRedirected;

    public static Func<string> BaseDirectory { get; set; } = () => AppContext.BaseDirectory;
}
