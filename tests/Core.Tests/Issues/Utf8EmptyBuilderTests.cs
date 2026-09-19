using System.Buffers;
using System.Text;
using FluentAssertions;
using Xunit;

namespace NGql.Core.Tests.Issues;

public class Utf8EmptyBuilderTests
{
    [Fact]
    public void StringBuilderChunks_EmptyBuilder_ExposeOneEmptyChunk()
    {
        var chunks = new StringBuilder().GetChunks();

        chunks.MoveNext().Should().BeTrue();
        chunks.Current.Length.Should().Be(0);
        chunks.MoveNext().Should().BeFalse();
    }

    [Fact]
    public void WriteUtf8_MinimalQuery_MatchesToString()
    {
        var query = new Query("Q");
        var writer = new ArrayBufferWriter<byte>();

        query.WriteUtf8(writer);

        Encoding.UTF8.GetString(writer.WrittenSpan).Should().Be(query.ToString());
    }
}
