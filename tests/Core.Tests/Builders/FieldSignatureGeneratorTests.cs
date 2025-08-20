using System.Collections.Generic;
using FluentAssertions;
using NGql.Core.Abstractions;
using NGql.Core.Features;
using Xunit;

namespace NGql.Core.Tests.Builders;

public class FieldSignatureGeneratorTests
{
    [Fact]
    public void GenerateSignature_EmptyFields_ShouldReturnZero()
    {
        // Arrange
        var fields = new SortedDictionary<string, FieldDefinition>();

        // Act
        var signature = FieldSignatureGenerator.GenerateSignature(fields);

        // Assert
        signature.Should().Be(0);
    }

    [Fact]
    public void GenerateSignature_SameFieldsNoArguments_ShouldReturnSameSignature()
    {
        // Arrange
        var fields1 = new SortedDictionary<string, FieldDefinition>
        {
            ["profiles"] = new("profiles", "object", null, new SortedDictionary<string, object?>(), 
                new SortedDictionary<string, FieldDefinition>
                {
                    ["id"] = new("id")
                })
        };

        var fields2 = new SortedDictionary<string, FieldDefinition>
        {
            ["profiles"] = new("profiles", "object", null, new SortedDictionary<string, object?>(), 
                new SortedDictionary<string, FieldDefinition>
                {
                    ["id"] = new("id")
                })
        };

        // Act
        var signature1 = FieldSignatureGenerator.GenerateSignature(fields1);
        var signature2 = FieldSignatureGenerator.GenerateSignature(fields2);

        // Assert
        signature1.Should().Be(signature2);
    }

    [Fact]
    public void GenerateSignature_SameFieldsWithSameArguments_ShouldReturnSameSignature()
    {
        // Arrange
        var args = new SortedDictionary<string, object?> { ["filter"] = "value" };

        var fields1 = new SortedDictionary<string, FieldDefinition>
        {
            ["profiles"] = new("profiles", "object", null, args, new SortedDictionary<string, FieldDefinition>())
        };

        var fields2 = new SortedDictionary<string, FieldDefinition>
        {
            ["profiles"] = new("profiles", "object", null, args, new SortedDictionary<string, FieldDefinition>())
        };

        // Act
        var signature1 = FieldSignatureGenerator.GenerateSignature(fields1);
        var signature2 = FieldSignatureGenerator.GenerateSignature(fields2);

        // Assert
        signature1.Should().Be(signature2);
    }

    [Fact]
    public void GenerateSignature_SameFieldsWithDifferentArguments_ShouldReturnDifferentSignatures()
    {
        // Arrange
        var args1 = new SortedDictionary<string, object?> { ["filter"] = "value1" };
        var args2 = new SortedDictionary<string, object?> { ["filter"] = "value2" };

        var fields1 = new SortedDictionary<string, FieldDefinition>
        {
            ["profiles"] = new("profiles", "object", null, args1, new SortedDictionary<string, FieldDefinition>())
        };

        var fields2 = new SortedDictionary<string, FieldDefinition>
        {
            ["profiles"] = new("profiles", "object", null, args2, new SortedDictionary<string, FieldDefinition>())
        };

        // Act
        var signature1 = FieldSignatureGenerator.GenerateSignature(fields1);
        var signature2 = FieldSignatureGenerator.GenerateSignature(fields2);

        // Assert
        signature1.Should().NotBe(signature2);
    }

    [Fact]
    public void GenerateSignature_DifferentFields_ShouldReturnDifferentSignatures()
    {
        // Arrange
        var fields1 = new SortedDictionary<string, FieldDefinition>
        {
            ["profiles"] = new("profiles")
        };

        var fields2 = new SortedDictionary<string, FieldDefinition>
        {
            ["users"] = new("users")
        };

        // Act
        var signature1 = FieldSignatureGenerator.GenerateSignature(fields1);
        var signature2 = FieldSignatureGenerator.GenerateSignature(fields2);

        // Assert
        signature1.Should().NotBe(signature2);
    }

    [Fact]
    public void GenerateSignature_NestedFieldsWithDifferentArguments_ShouldReturnDifferentSignatures()
    {
        // Arrange
        var nestedArgs1 = new SortedDictionary<string, object?> { ["email"] = "test1@test.com" };
        var nestedArgs2 = new SortedDictionary<string, object?> { ["email"] = "test2@test.com" };

        var fields1 = new SortedDictionary<string, FieldDefinition>
        {
            ["profiles"] = new("profiles", "object", null, new SortedDictionary<string, object?>(), 
                new SortedDictionary<string, FieldDefinition>
                {
                    ["node"] = new("node", "object", null, nestedArgs1, new SortedDictionary<string, FieldDefinition>())
                })
        };

        var fields2 = new SortedDictionary<string, FieldDefinition>
        {
            ["profiles"] = new("profiles", "object", null, new SortedDictionary<string, object?>(), 
                new SortedDictionary<string, FieldDefinition>
                {
                    ["node"] = new("node", "object", null, nestedArgs2, new SortedDictionary<string, FieldDefinition>())
                })
        };

        // Act
        var signature1 = FieldSignatureGenerator.GenerateSignature(fields1);
        var signature2 = FieldSignatureGenerator.GenerateSignature(fields2);

        // Assert
        signature1.Should().NotBe(signature2);
    }
}
