using System;
using System.Buffers;
using System.Text.Json;

namespace NGql.Client.Tests.Extensions;

internal static class JsonExtensions
{
    private static readonly JsonSerializerOptions _options = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    internal static T? ToObject<T>(this JsonElement element, string? propertyPath = null)
    {
        var value = string.IsNullOrWhiteSpace(propertyPath) ? element : element.GetProperty(propertyPath);
        var bufferWriter = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(bufferWriter))
            value.WriteTo(writer);

        return JsonSerializer.Deserialize<T>(bufferWriter.WrittenSpan, _options);
    }

    internal static T? ToObject<T>(this JsonDocument document)
    {
        if (document == null)
            throw new ArgumentNullException(nameof(document));
        return document.RootElement.ToObject<T>();
    }
}