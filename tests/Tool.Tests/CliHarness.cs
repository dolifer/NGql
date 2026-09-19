using System.Text;

namespace NGql.Tool.Tests;

internal sealed record CliResult(int ExitCode, string Out, string Error);

/// <summary>
/// Redirects the process console and the <see cref="ToolEnvironment"/> seams for the lifetime of
/// a test, then restores them. Every consumer lives in <see cref="ConsoleCollection"/>, so only
/// one harness is ever active at a time.
/// </summary>
internal sealed class CliHarness : IDisposable
{
    private readonly TextWriter _originalOut = Console.Out;
    private readonly TextWriter _originalError = Console.Error;
    private readonly TextReader _originalIn = Console.In;
    private readonly Func<bool> _originalIsInputRedirected = ToolEnvironment.IsInputRedirected;
    private readonly Func<string> _originalBaseDirectory = ToolEnvironment.BaseDirectory;

    private readonly StringWriter _out = new() { NewLine = "\n" };
    private readonly StringWriter _error = new() { NewLine = "\n" };

    public CliHarness()
    {
        Console.SetOut(_out);
        Console.SetError(_error);
        Console.SetIn(TextReader.Null);
        ToolEnvironment.IsInputRedirected = () => false;
    }

    public static void SetStdIn(string text)
    {
        Console.SetIn(new StringReader(text));
        ToolEnvironment.IsInputRedirected = () => true;
    }

    public static void SetBaseDirectory(string path) => ToolEnvironment.BaseDirectory = () => path;

    public async Task<CliResult> RunAsync(params string[] args)
    {
        var exitCode = await Program.Main(args);
        return new CliResult(exitCode, Normalize(_out), Normalize(_error));
    }

    private static string Normalize(StringWriter writer) =>
        writer.GetStringBuilder().ToString().Replace("\r\n", "\n");

    public void Dispose()
    {
        Console.SetOut(_originalOut);
        Console.SetError(_originalError);
        Console.SetIn(_originalIn);
        ToolEnvironment.IsInputRedirected = _originalIsInputRedirected;
        ToolEnvironment.BaseDirectory = _originalBaseDirectory;
        _out.Dispose();
        _error.Dispose();
    }
}

/// <summary>Creates and deletes a scratch directory for fixture-discovery tests.</summary>
internal sealed class TempDir : IDisposable
{
    public string Path { get; } = System.IO.Path.Combine(
        System.IO.Path.GetTempPath(),
        "ngql-tool-tests-" + Guid.NewGuid().ToString("N"));

    public TempDir() => Directory.CreateDirectory(Path);

    public string Write(string relativePath, string content)
    {
        var full = System.IO.Path.Combine(Path, relativePath);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(full)!);
        File.WriteAllText(full, content, new UTF8Encoding(false));
        return full;
    }

    public string Dir(string relativePath)
    {
        var full = System.IO.Path.Combine(Path, relativePath);
        Directory.CreateDirectory(full);
        return full;
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(Path, recursive: true);
        }
        catch (IOException)
        {
            // Scratch dir cleanup is best-effort; a locked file must not fail the test.
        }
    }
}
