using System.Collections.Generic;
using FluentAssertions;
using NGql.Core.Abstractions;
using NGql.Core.Features;
using Xunit;

namespace NGql.Core.Tests.Issues;

/// <summary>
/// Regression: signature generation copies the parent path into a fixed 256-char stack buffer.
/// The field-name copy was length-guarded, but the parent-path copy was not. Once an
/// intermediate (non-leaf) node overflows the buffer and builds its path on the heap, that
/// heap path — already longer than the buffer — becomes the parent path for its children.
/// The unguarded parent-path copy then threw <c>ArgumentException: Destination too short</c>
/// on the child before the field-name guard could run. A leaf-only overflow never surfaced
/// this because the poison only propagates through a node that itself has children.
/// </summary>
public class FieldSignatureDeepParentPathTests
{
    [Fact]
    public void GenerateSignature_IntermediateNodePathExceedsStackBuffer_DoesNotThrow()
    {
        // Arrange — three ~201-char segments nested three levels deep. The first segment
        // fits the 256 buffer; the second overflows it and produces a heap path of 256 chars
        // or more; that heap path becomes the parent path when recursing into the third.
        var segment1 = new string('a', 201);
        var segment2 = new string('b', 201);
        var segment3 = new string('c', 201);

        var fields = new Dictionary<string, FieldDefinition>
        {
            [segment1] = new(segment1, "object", null, new Dictionary<string, object?>(),
                new Dictionary<string, FieldDefinition>
                {
                    [segment2] = new(segment2, "object", null, new Dictionary<string, object?>(),
                        new Dictionary<string, FieldDefinition>
                        {
                            [segment3] = new(segment3, "String")
                        })
                })
        };

        // Act
        var act = () => FieldSignatureGenerator.GenerateSignature(fields);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void GenerateSignature_IntermediateNodePathExceedsStackBuffer_StaysDeterministicAndNonZero()
    {
        // Arrange
        var segment1 = new string('a', 201);
        var segment2 = new string('b', 201);
        var segment3 = new string('c', 201);

        var fields = new Dictionary<string, FieldDefinition>
        {
            [segment1] = new(segment1, "object", null, new Dictionary<string, object?>(),
                new Dictionary<string, FieldDefinition>
                {
                    [segment2] = new(segment2, "object", null, new Dictionary<string, object?>(),
                        new Dictionary<string, FieldDefinition>
                        {
                            [segment3] = new(segment3, "String")
                        })
                })
        };

        // Act
        var first = FieldSignatureGenerator.GenerateSignature(fields);
        var second = FieldSignatureGenerator.GenerateSignature(fields);

        // Assert
        first.Should().NotBe(0);
        second.Should().Be(first);
    }
}
