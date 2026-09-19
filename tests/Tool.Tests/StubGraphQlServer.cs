using System.Net;
using System.Net.Sockets;
using System.Text;

namespace NGql.Tool.Tests;

internal sealed record StubResponse(int StatusCode, string ContentType, string Body)
{
    public static StubResponse Json(string body) => new(200, "application/json", body);

    public static StubResponse Text(string body) => new(200, "text/plain", body);

    public static StubResponse Status(int statusCode, string body) => new(statusCode, "text/plain", body);
}

/// <summary>
/// Minimal in-process HTTP/1.1 server on a dynamically assigned loopback port. A raw
/// <see cref="TcpListener"/> rather than <see cref="HttpListener"/> so no OS URL reservation is
/// needed and the exact response bytes are under test control.
/// </summary>
internal sealed class StubGraphQlServer : IAsyncDisposable
{
    private readonly TcpListener _listener;
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _loop;
    private readonly Func<string, StubResponse> _respond;

    public string? LastBody { get; private set; }

    public IReadOnlyDictionary<string, string> LastHeaders { get; private set; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    public string Url => $"http://127.0.0.1:{((IPEndPoint)_listener.LocalEndpoint).Port}/graphql";

    public StubGraphQlServer(Func<string, StubResponse> respond)
    {
        _respond = respond;
        _listener = new TcpListener(IPAddress.Loopback, 0);
        _listener.Start();
        _loop = AcceptLoopAsync(_cts.Token);
    }

    private async Task AcceptLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await _listener.AcceptTcpClientAsync(token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (SocketException)
            {
                return;
            }

            using (client)
            {
                await HandleAsync(client, token);
            }
        }
    }

    private async Task HandleAsync(TcpClient client, CancellationToken token)
    {
        var stream = client.GetStream();
        var headerBytes = await ReadUntilHeaderEndAsync(stream, token);
        if (headerBytes is null)
            return;

        var headerText = Encoding.ASCII.GetString(headerBytes);
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in headerText.Split("\r\n").Skip(1))
        {
            var colon = line.IndexOf(':');
            if (colon > 0)
                headers[line[..colon].Trim()] = line[(colon + 1)..].Trim();
        }

        var length = headers.TryGetValue("Content-Length", out var raw) && int.TryParse(raw, out var n) ? n : 0;
        var body = new byte[length];
        var read = 0;
        while (read < length)
        {
            var chunk = await stream.ReadAsync(body.AsMemory(read, length - read), token);
            if (chunk == 0)
                break;
            read += chunk;
        }

        LastHeaders = headers;
        LastBody = Encoding.UTF8.GetString(body, 0, read);

        var response = _respond(LastBody);
        var payload = Encoding.UTF8.GetBytes(response.Body);
        var head = Encoding.ASCII.GetBytes(
            $"HTTP/1.1 {response.StatusCode} {ReasonPhrase(response.StatusCode)}\r\n" +
            $"Content-Type: {response.ContentType}\r\n" +
            $"Content-Length: {payload.Length}\r\n" +
            "Connection: close\r\n\r\n");

        await stream.WriteAsync(head, token);
        await stream.WriteAsync(payload, token);
        await stream.FlushAsync(token);
    }

    private static async Task<byte[]?> ReadUntilHeaderEndAsync(NetworkStream stream, CancellationToken token)
    {
        var buffer = new List<byte>(1024);
        var one = new byte[1];
        while (true)
        {
            var read = await stream.ReadAsync(one.AsMemory(0, 1), token);
            if (read == 0)
                return null;

            buffer.Add(one[0]);
            if (buffer.Count >= 4
                && buffer[^4] == (byte)'\r' && buffer[^3] == (byte)'\n'
                && buffer[^2] == (byte)'\r' && buffer[^1] == (byte)'\n')
            {
                return buffer.ToArray();
            }
        }
    }

    private static string ReasonPhrase(int statusCode) => statusCode switch
    {
        200 => "OK",
        400 => "Bad Request",
        500 => "Internal Server Error",
        _ => "Unknown",
    };

    public async ValueTask DisposeAsync()
    {
        await _cts.CancelAsync();
        _listener.Stop();
        try
        {
            await _loop;
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown.
        }
        _cts.Dispose();
    }
}
