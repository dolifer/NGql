using System.Net.Http;
using System.Text;
using System.Text.Json;

using Spectre.Console;
using Spectre.Console.Json;

namespace NGql.Tool;

/// <summary>
/// Sends a rendered GraphQL operation to a remote endpoint and reports the response.
/// </summary>
internal static class Executor
{
    public static async Task<int> ExecuteAsync(
        string graphql,
        string endpoint,
        IReadOnlyList<KeyValuePair<string, string>> headers,
        IReadOnlyDictionary<string, object?> variables)
    {
        var body = new Dictionary<string, object?> { ["query"] = graphql };
        if (variables.Count > 0)
        {
            body["variables"] = variables;
        }

        var bodyJson = JsonSerializer.Serialize(body, JsonWriteOptions);

        using var http = new HttpClient();
        using var req = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(bodyJson, Encoding.UTF8, "application/json"),
        };

        foreach (var (name, value) in headers)
        {
            // Try request-scoped first; fall back to content-scoped for content headers.
            if (!req.Headers.TryAddWithoutValidation(name, value))
            {
                req.Content?.Headers.TryAddWithoutValidation(name, value);
            }
        }

        HttpResponseMessage response;
        try
        {
            response = await http.SendAsync(req);
        }
        catch (HttpRequestException ex)
        {
            await Console.Error.WriteLineAsync($"ngql: HTTP request failed: {ex.Message}");
            return ExitCodes.HttpFailure;
        }

        var responseBody = await response.Content.ReadAsStringAsync();

        WriteResponse(responseBody);

        if (!response.IsSuccessStatusCode)
        {
            await Console.Error.WriteLineAsync($"ngql: HTTP {(int)response.StatusCode} {response.ReasonPhrase}");
            return ExitCodes.HttpFailure;
        }

        if (HasGraphQlErrors(responseBody))
        {
            return ExitCodes.GraphQlErrors;
        }

        return ExitCodes.Ok;
    }

    private static void WriteResponse(string responseBody)
    {
        // If the body parses as JSON, hand it to AnsiConsole as a JsonText so we get syntax
        // highlighting in interactive terminals. Spectre auto-detects redirection and falls
        // back to plain text when stdout isn't a TTY. If parsing fails (HTML error page,
        // plain text, etc.) write the raw body.
        try
        {
            using var _ = JsonDocument.Parse(responseBody);
            AnsiConsole.Write(new JsonText(responseBody));
            AnsiConsole.WriteLine();
        }
        catch (JsonException)
        {
            Console.WriteLine(responseBody);
        }
    }

    private static bool HasGraphQlErrors(string responseBody)
    {
        try
        {
            using var doc = JsonDocument.Parse(responseBody);
            return doc.RootElement.ValueKind == JsonValueKind.Object
                && doc.RootElement.TryGetProperty("errors", out var errors)
                && errors.ValueKind == JsonValueKind.Array
                && errors.GetArrayLength() > 0;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static readonly JsonSerializerOptions JsonWriteOptions = new()
    {
        WriteIndented = true,
    };
}
