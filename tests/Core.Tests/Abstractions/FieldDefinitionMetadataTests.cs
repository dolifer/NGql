using System.Collections.Generic;
using FluentAssertions;
using NGql.Core.Abstractions;
using Xunit;

namespace NGql.Core.Tests.Abstractions;

public class FieldDefinitionMetadataTests
{
    [Fact]
    public void HasMetadata_FieldWithoutMetadata_ReturnsFalseWithoutMaterializing()
    {
        // Arrange
        var field = new FieldDefinition("user");

        // Act & Assert — the check must not attach an empty dictionary to the field
        field.HasMetadata.Should().BeFalse();
        field._metadata.Should().BeNull("HasMetadata must not materialize the metadata dictionary");
    }

    [Fact]
    public void HasMetadata_FieldWithMetadata_ReturnsTrue()
    {
        // Arrange
        var field = new FieldDefinition("user")
        {
            Metadata = new Dictionary<string, object?> { ["flatten"] = "x" }
        };

        // Act & Assert
        field.HasMetadata.Should().BeTrue();
    }

    [Fact]
    public void HasMetadata_EmptyMetadataDictionary_ReturnsFalse()
    {
        // Arrange — reading Metadata materializes an empty dictionary (documented behavior)
        var field = new FieldDefinition("user");
        _ = field.Metadata;

        // Act & Assert
        field.HasMetadata.Should().BeFalse("an attached but empty dictionary is not metadata");
    }

    [Fact]
    public void Metadata_Getter_MaterializesMutableDictionary()
    {
        // Arrange
        var field = new FieldDefinition("user");

        // Act — mutation through the getter must persist (existing contract)
        field.Metadata["tag"] = "a";

        // Assert
        field.HasMetadata.Should().BeTrue();
        field.Metadata["tag"].Should().Be("a");
    }
}
