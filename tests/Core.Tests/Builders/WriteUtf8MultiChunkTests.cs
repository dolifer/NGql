using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using FluentAssertions;
using NGql.Core.Builders;
using Xunit;

namespace NGql.Core.Tests.Builders;

/// <summary>
/// A render large enough that the pooled <see cref="StringBuilder"/> is guaranteed to span several
/// internal chunks, so the UTF-8 transcode takes the multi-chunk encoder loop rather than either
/// single-chunk fast path. The stateful encoder must reassemble surrogate pairs that land on a
/// chunk boundary, so parity with <c>Encoding.UTF8.GetBytes(query.ToString())</c> is the assertion.
/// </summary>
public class WriteUtf8MultiChunkTests
{
    private const int FieldCount = 6000;

    private static QueryBuilder VeryLargeQuery()
    {
        var builder = QueryBuilder.CreateDefaultBuilder("VeryLarge");
        for (var i = 0; i < FieldCount; i++)
        {
            builder.AddField($"group{i}.nested{i}.leafValue{i}");
        }
        return builder;
    }

    [Fact]
    public void WriteUtf8_VeryLargeQuery_MatchesToStringBytes()
    {
        var builder = VeryLargeQuery();
        var expected = Encoding.UTF8.GetBytes(builder.ToString());
        var writer = new ArrayBufferWriter<byte>();

        builder.WriteUtf8(writer);

        writer.WrittenSpan.ToArray().Should().Equal(expected);
    }

    [Fact]
    public void WriteUtf8_VeryLargeQueryWithAstralArgument_MatchesToStringBytes()
    {
        var builder = VeryLargeQuery();
        builder.AddField("marker", new Dictionary<string, object?> { ["label"] = string.Concat(Enumerable.Repeat("😀𝓊", 200)) });
        var expected = Encoding.UTF8.GetBytes(builder.ToString());
        var writer = new ArrayBufferWriter<byte>();

        builder.WriteUtf8(writer);

        writer.WrittenSpan.ToArray().Should().Equal(expected);
    }

    [Fact]
    public void WriteUtf8_VeryLargeQueryIntoBoundedWriter_MatchesToStringBytes()
    {
        var builder = VeryLargeQuery();
        var expected = Encoding.UTF8.GetBytes(builder.ToString());
        var writer = new BoundedBufferWriter(64);

        builder.WriteUtf8(writer);

        writer.Written.Should().Equal(expected);
    }

    // Hands back at most `maxSpan` bytes per GetSpan call, forcing the encoder loop to fill the
    // destination in several passes instead of a single whole-chunk transcode.
    private sealed class BoundedBufferWriter(int maxSpan) : IBufferWriter<byte>
    {
        private readonly List<byte> _written = [];
        private byte[] _buffer = new byte[maxSpan];

        public IReadOnlyList<byte> Written => _written;

        public void Advance(int count) => _written.AddRange(_buffer.AsSpan(0, count).ToArray());

        public Memory<byte> GetMemory(int sizeHint = 0) => GetBuffer(sizeHint);

        public Span<byte> GetSpan(int sizeHint = 0) => GetBuffer(sizeHint).Span;

        private Memory<byte> GetBuffer(int sizeHint)
        {
            var size = sizeHint <= 0 ? maxSpan : Math.Min(sizeHint, maxSpan);
            if (_buffer.Length < size) _buffer = new byte[size];
            return _buffer.AsMemory(0, size);
        }
    }
}
