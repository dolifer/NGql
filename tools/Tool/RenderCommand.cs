using System.ComponentModel;
using System.Text.Json;

using Spectre.Console.Cli;

namespace NGql.Tool;

/// <summary>
/// Default command. Renders an NGql snippet as GraphQL; optionally posts to a live endpoint.
/// </summary>
internal sealed class RenderCommand : AsyncCommand<RenderCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "[INPUT]")]
        [Description("Path to a snippet file, or '-' for stdin (default if no value provided).")]
        public string? Input { get; init; }

        [CommandOption("--execute")]
        [Description("After rendering, POST the operation to --endpoint and print the response.")]
        public bool Execute { get; init; }

        [CommandOption("--endpoint <URL>")]
        [Description("GraphQL endpoint URL to POST the rendered operation to. Required with --execute.")]
        public string? Endpoint { get; init; }

        [CommandOption("-H|--header <HEADER>")]
        [Description("HTTP header in 'Name: value' form. Repeatable.")]
        public string[] Headers { get; init; } = [];

        [CommandOption("--var <KEY_VALUE>")]
        [Description("GraphQL variable in 'key=value' form. Values are JSON-parsed when possible (numbers, bools, arrays, objects); otherwise sent as a string. Repeatable.")]
        public string[] Variables { get; init; } = [];

        [CommandOption("--allow-mutations")]
        [Description("Opt in to executing mutations. Default refuses for safety; mutations have side effects.")]
        public bool AllowMutations { get; init; }
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings s)
    {
        // ── 1. Resolve snippet text ────────────────────────────────────────────────────────
        string snippet;
        string sourceLabel;
        if (s.Input is null || s.Input == "-")
        {
            if (s.Input is null && !Console.IsInputRedirected)
            {
                await Console.Error.WriteLineAsync("ngql: no input. Pipe a snippet on stdin or pass a file path. See `ngql --help`.");
                return ExitCodes.InvalidUsage;
            }

            snippet = await Console.In.ReadToEndAsync();
            sourceLabel = "<stdin>";
        }
        else
        {
            if (!File.Exists(s.Input))
            {
                await Console.Error.WriteLineAsync($"ngql: file not found: {s.Input}");
                return ExitCodes.FileNotFound;
            }

            snippet = await File.ReadAllTextAsync(s.Input);
            sourceLabel = s.Input;
        }

        // ── 2. Compile + render ────────────────────────────────────────────────────────────
        var (ok, rendered, error) = await SnippetRunner.CompileAndRun(snippet);
        if (!ok)
        {
            await Console.Error.WriteLineAsync($"ngql: failed to render {sourceLabel}");
            await Console.Error.WriteLineAsync(error);
            return ExitCodes.RenderFailed;
        }

        var graphql = rendered ?? string.Empty;

        // ── 3. Render-only path ────────────────────────────────────────────────────────────
        if (!s.Execute)
        {
            Console.WriteLine(graphql);
            return ExitCodes.Ok;
        }

        // ── 4. Execute path ────────────────────────────────────────────────────────────────
        if (string.IsNullOrWhiteSpace(s.Endpoint))
        {
            await Console.Error.WriteLineAsync("ngql: --execute requires --endpoint <url>.");
            return ExitCodes.InvalidUsage;
        }

        if (SnippetRunner.IsMutation(graphql) && !s.AllowMutations)
        {
            await Console.Error.WriteLineAsync("ngql: refusing to execute a mutation by default. Re-run with --allow-mutations to opt in.");
            await Console.Error.WriteLineAsync("(Rendered operation:)");
            await Console.Error.WriteLineAsync(graphql);
            return ExitCodes.MutationBlocked;
        }

        if (!TryParseHeaders(s.Headers, out var headers, out var headerError))
        {
            await Console.Error.WriteLineAsync($"ngql: {headerError}");
            return ExitCodes.InvalidUsage;
        }

        if (!TryParseVariables(s.Variables, out var variables, out var varError))
        {
            await Console.Error.WriteLineAsync($"ngql: {varError}");
            return ExitCodes.InvalidUsage;
        }

        return await Executor.ExecuteAsync(graphql, s.Endpoint, headers, variables);
    }

    private static bool TryParseHeaders(
        string[] raw,
        out List<KeyValuePair<string, string>> result,
        out string? error)
    {
        result = new List<KeyValuePair<string, string>>(raw.Length);
        foreach (var entry in raw)
        {
            var colon = entry.IndexOf(':');
            if (colon <= 0)
            {
                error = $"header must be in 'Name: value' form, got '{entry}'";
                return false;
            }
            result.Add(new KeyValuePair<string, string>(
                entry[..colon].Trim(),
                entry[(colon + 1)..].Trim()));
        }
        error = null;
        return true;
    }

    private static bool TryParseVariables(
        string[] raw,
        out Dictionary<string, object?> result,
        out string? error)
    {
        result = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var entry in raw)
        {
            var eq = entry.IndexOf('=');
            if (eq <= 0)
            {
                error = $"variable must be in 'key=value' form, got '{entry}'";
                return false;
            }
            var key = entry[..eq];
            var rawValue = entry[(eq + 1)..];
            result[key] = ParseVariableValue(rawValue);
        }
        error = null;
        return true;
    }

    private static object? ParseVariableValue(string raw)
    {
        // JSON-parse if possible (catches numbers, bools, arrays, objects, null, quoted strings).
        // Fall back to bare-string semantics so `--var name=alice` works without quoting.
        try
        {
            using var doc = JsonDocument.Parse(raw);
            return JsonElementToObject(doc.RootElement);
        }
        catch (JsonException)
        {
            return raw;
        }
    }

    private static object? JsonElementToObject(JsonElement el) => el.ValueKind switch
    {
        JsonValueKind.Object => el.EnumerateObject().ToDictionary(p => p.Name, p => JsonElementToObject(p.Value)),
        JsonValueKind.Array => el.EnumerateArray().Select(JsonElementToObject).ToList(),
        JsonValueKind.String => el.GetString(),
        JsonValueKind.Number => el.TryGetInt64(out var l) ? l : el.GetDouble(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Null => null,
        _ => null,
    };
}
