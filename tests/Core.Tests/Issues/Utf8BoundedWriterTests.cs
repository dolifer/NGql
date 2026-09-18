using System;
using System.Buffers;
using System.Collections.Generic;
using System.Text;
using FluentAssertions;
using NGql.Core.Builders;
using Xunit;

namespace NGql.Core.Tests.Issues;

public class Utf8BoundedWriterTests
{
    [Theory]
    [InlineData("name")]
    [InlineData("名前")]
    [InlineData("emoji😀field")]
    public void WriteUtf8_WriterReturningShortSpans_MatchesToString(string argument)
    {
        var query = QueryBuilder.CreateDefaultBuilder("Bounded")
            .AddField("user", new Dictionary<string, object?> { ["label"] = argument })
            .AddField("user.id");
        var writer = new ShortSpanWriter(spanLength: 16);

        query.WriteUtf8(writer);

        Encoding.UTF8.GetString(writer.WrittenSpan).Should().Be(query.ToString());
        writer.LargestSpan.Should().Be(16);
    }

    private sealed class ShortSpanWriter(int spanLength) : IBufferWriter<byte>
    {
        private readonly ArrayBufferWriter<byte> _inner = new();

        public int LargestSpan { get; private set; }

        public ReadOnlySpan<byte> WrittenSpan => _inner.WrittenSpan;

        public void Advance(int count) => _inner.Advance(count);

        public Memory<byte> GetMemory(int sizeHint = 0) => _inner.GetMemory(spanLength)[..spanLength];

        public Span<byte> GetSpan(int sizeHint = 0)
        {
            LargestSpan = Math.Max(LargestSpan, spanLength);
            return _inner.GetSpan(spanLength)[..spanLength];
        }
    }
}
