using System.Reflection;
using Spectre.Console.Cli;

namespace NGql.Tool;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        // Handle --version and -v before delegating to Spectre, since Spectre 0.49 doesn't
        // automatically fire the built-in version path when the default command claims unknown
        // args. Doing it here keeps the contract: `ngql --version` prints a single semver line
        // on stdout and exits 0.
        if (args.Length == 1 && (args[0] == "--version" || args[0] == "-v"))
        {
            Console.WriteLine(GetVersion());
            return 0;
        }

        var app = new CommandApp<RenderCommand>();
        app.Configure(config =>
        {
            config.SetApplicationName("ngql");
            config.SetApplicationVersion(GetVersion());

            // Internal subcommands used by the project's regression harness (`make skill-eval`).
            // Hidden from --help but discoverable via direct invocation.
            config.AddCommand<FixturesCommand>("fixtures")
                .IsHidden()
                .WithDescription("Run all built-in regression fixtures (contributor use only).");
            config.AddCommand<FixtureCommand>("fixture")
                .IsHidden()
                .WithDescription("Run a single named regression fixture (contributor use only).");

            config.PropagateExceptions();
        });

        try
        {
            return await app.RunAsync(args);
        }
        catch (CommandRuntimeException ex)
        {
            // Spectre's parse / validation / runtime failures (CommandParseException derives from this).
            await Console.Error.WriteLineAsync($"ngql: {ex.Message}");
            await Console.Error.WriteLineAsync("See `ngql --help`.");
            return ExitCodes.InvalidUsage;
        }
    }

    private static string GetVersion()
    {
        var asm = typeof(Program).Assembly;
        var info = asm.GetCustomAttributes(typeof(AssemblyInformationalVersionAttribute), false);
        var v = info.Length > 0
            ? ((AssemblyInformationalVersionAttribute)info[0]).InformationalVersion
            : asm.GetName().Version?.ToString() ?? "unknown";

        // Strip the "+<commit-sha>" SourceLink suffix.
        var plus = v.IndexOf('+');
        return plus > 0 ? v[..plus] : v;
    }
}

internal static class ExitCodes
{
    public const int Ok                = 0;
    public const int RenderFailed      = 1;
    public const int GraphQlErrors     = 2;
    public const int HttpFailure       = 3;
    public const int MutationBlocked   = 4;
    public const int InvalidUsage      = 64;
    public const int FileNotFound      = 66;
}
