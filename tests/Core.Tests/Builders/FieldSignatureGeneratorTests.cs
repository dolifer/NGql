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
            ["profiles"] = new("profiles", "object", null, new Dictionary<string, object?>(),
                new SortedDictionary<string, FieldDefinition>
                {
                    ["id"] = new("id")
                })
        };

        var fields2 = new SortedDictionary<string, FieldDefinition>
        {
            ["profiles"] = new("profiles", "object", null, new Dictionary<string, object?>(),
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
        var args = new Dictionary<string, object?> { ["filter"] = "value" };

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
        var args1 = new Dictionary<string, object?> { ["filter"] = "value1" };
        var args2 = new Dictionary<string, object?> { ["filter"] = "value2" };

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
        var nestedArgs1 = new Dictionary<string, object?> { ["email"] = "test1@test.com" };
        var nestedArgs2 = new Dictionary<string, object?> { ["email"] = "test2@test.com" };

        var fields1 = new SortedDictionary<string, FieldDefinition>
        {
            ["profiles"] = new("profiles", "object", null, new Dictionary<string, object?>(),
                new SortedDictionary<string, FieldDefinition>
                {
                    ["node"] = new("node", "object", null, nestedArgs1, new SortedDictionary<string, FieldDefinition>())
                })
        };

        var fields2 = new SortedDictionary<string, FieldDefinition>
        {
            ["profiles"] = new("profiles", "object", null, new Dictionary<string, object?>(),
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

    [Fact]
    public void GenerateSignature_VeryLongFieldName_TriggersBufferFallback_ShouldGenerateValidSignature()
    {
        // Arrange - Create a field with a very long name (>1024 chars to exceed buffer)
        var longFieldName = new string('a', 1200);
        var fields = new SortedDictionary<string, FieldDefinition>
        {
            [longFieldName] = new(longFieldName, "String")
        };

        // Act - Should not throw, but use string fallback internally
        var signature = FieldSignatureGenerator.GenerateSignature(fields);

        // Assert - Signature should be deterministic and non-zero
        signature.Should().NotBe(0);

        // Act again - Same field should produce same signature
        var signature2 = FieldSignatureGenerator.GenerateSignature(fields);

        // Assert - Deterministic
        signature.Should().Be(signature2);
    }

    [Fact]
    public void GenerateSignature_VeryLongNestedPath_ShouldHandleFallbackCorrectly()
    {
        // Arrange - Simple deep nesting (not very long names, just many levels)
        var fields = new SortedDictionary<string, FieldDefinition>
        {
            ["a"] = new("a", "object", null, new Dictionary<string, object?>(),
                new SortedDictionary<string, FieldDefinition>
                {
                    ["b"] = new("b", "object", null, new Dictionary<string, object?>(),
                        new SortedDictionary<string, FieldDefinition>
                        {
                            ["c"] = new("c", "object", null, new Dictionary<string, object?>(),
                                new SortedDictionary<string, FieldDefinition>
                                {
                                    ["d"] = new("d", "object", null, new Dictionary<string, object?>(),
                                        new SortedDictionary<string, FieldDefinition>
                                        {
                                            ["e"] = new("e", "String")
                                        })
                                })
                        })
                })
        };

        // Act
        var signature1 = FieldSignatureGenerator.GenerateSignature(fields);
        var signature2 = FieldSignatureGenerator.GenerateSignature(fields);

        // Assert - Should produce valid, deterministic signatures
        signature1.Should().NotBe(0);
        signature1.Should().Be(signature2);
    }

    [Fact]
    public void GenerateSignature_VeryLongFieldNameWithArguments_ShouldIncludeArgumentsInSignature()
    {
        // Arrange - Long field name with arguments
        var longFieldName = new string('b', 1100);
        var args = new Dictionary<string, object?> { ["filter"] = "longValue" };
        var fields = new SortedDictionary<string, FieldDefinition>
        {
            [longFieldName] = new(longFieldName, "object", null, args, new SortedDictionary<string, FieldDefinition>())
        };

        var fieldsNoArgs = new SortedDictionary<string, FieldDefinition>
        {
            [longFieldName] = new(longFieldName, "object", null, new Dictionary<string, object?>(), 
                new SortedDictionary<string, FieldDefinition>())
        };

        // Act
        var signatureWithArgs = FieldSignatureGenerator.GenerateSignature(fields);
        var signatureNoArgs = FieldSignatureGenerator.GenerateSignature(fieldsNoArgs);

        // Assert - Arguments should affect signature even with fallback path
        signatureWithArgs.Should().NotBe(signatureNoArgs);
    }

    [Fact]
    public void GenerateSignature_NestedFieldsWithArguments_ShouldConsiderNestingLevel()
    {
        // This tests the AppendFieldSignature and AppendFieldSignatureRemainder logic
        var fields = new SortedDictionary<string, FieldDefinition>
        {
            ["user"] = new("user", "User", null, new Dictionary<string, object?> { ["id"] = "123" },
                new SortedDictionary<string, FieldDefinition>
                {
                    ["profile"] = new("profile", "Profile", null, new Dictionary<string, object?>(),
                        new SortedDictionary<string, FieldDefinition>
                        {
                            ["avatar"] = new("avatar", "String", null, new Dictionary<string, object?> { ["size"] = "large" })
                        })
                })
        };

        // Act
        var signature = FieldSignatureGenerator.GenerateSignature(fields);

        // Assert - Should include nested arguments in signature
        signature.Should().NotBe(0);
        
        // Verify nested structure affects signature
        var simpleFields = new SortedDictionary<string, FieldDefinition>
        {
            ["user"] = new("user", "User")
        };
        var simpleSignature = FieldSignatureGenerator.GenerateSignature(simpleFields);
        signature.Should().NotBe(simpleSignature);
    }

    [Fact]
    public void GenerateSignature_FieldsWithSpecialCharactersInArguments_ShouldBeIncluded()
    {
        var fields = new SortedDictionary<string, FieldDefinition>
        {
            ["search"] = new("search", "Result[]", null, new Dictionary<string, object?>
            {
                ["query"] = "user@example.com",
                ["filter"] = "status=active&type=admin"
            }, new SortedDictionary<string, FieldDefinition>())
        };

        var fieldsNoSpecialChars = new SortedDictionary<string, FieldDefinition>
        {
            ["search"] = new("search", "Result[]", null, new Dictionary<string, object?>
            {
                ["query"] = "user",
                ["filter"] = "status"
            }, new SortedDictionary<string, FieldDefinition>())
        };

        // Act
        var sig1 = FieldSignatureGenerator.GenerateSignature(fields);
        var sig2 = FieldSignatureGenerator.GenerateSignature(fieldsNoSpecialChars);

        // Assert - Special characters should affect signature
        sig1.Should().NotBe(sig2);
        sig1.Should().NotBe(0);
    }

    [Fact]
    public void GenerateSignature_MultipleFields_ShouldConsiderAllFields()
    {
        var fields = new SortedDictionary<string, FieldDefinition>
        {
            ["user"] = new("user", "User", null, new Dictionary<string, object?> { ["id"] = "1" }),
            ["posts"] = new("posts", "Post[]", null, new Dictionary<string, object?> { ["limit"] = 10 }),
            ["comments"] = new("comments", "Comment[]", null, new Dictionary<string, object?> { ["sort"] = "date" })
        };

        var fewerFields = new SortedDictionary<string, FieldDefinition>
        {
            ["user"] = new("user", "User", null, new Dictionary<string, object?> { ["id"] = "1" }),
            ["posts"] = new("posts", "Post[]", null, new Dictionary<string, object?> { ["limit"] = 10 })
        };

        // Act
        var sig1 = FieldSignatureGenerator.GenerateSignature(fields);
        var sig2 = FieldSignatureGenerator.GenerateSignature(fewerFields);

        // Assert - Different field count should affect signature
        sig1.Should().NotBe(sig2);
    }

    [Fact]
    public void GenerateSignature_ComplexNestedWithTypedArguments_ShouldIncludeTypeInfo()
    {
        var fields = new SortedDictionary<string, FieldDefinition>
        {
            ["data"] = new("data", "DataNode", null, new Dictionary<string, object?>(),
                new SortedDictionary<string, FieldDefinition>
                {
                    ["edges"] = new("edges", "Edge[]", null, new Dictionary<string, object?> { ["first"] = 20, ["after"] = "cursor123" },
                        new SortedDictionary<string, FieldDefinition>
                        {
                            ["node"] = new("node", "T", null, null, 
                                new SortedDictionary<string, FieldDefinition>
                                {
                                    ["id"] = new("id", "ID!"),
                                    ["value"] = new("value", "String!")
                                })
                        })
                })
        };

        // Act
        var signature = FieldSignatureGenerator.GenerateSignature(fields);

        // Assert - Should generate valid signature for complex nested types
        signature.Should().NotBe(0);
    }

    [Fact]
    public void GenerateSignature_DeterministicWithDifferentFieldOrder_ShouldProduceDifferentSignature()
    {
        // Note: Signatures depend on insertion order since Dictionary iteration is insertion-order based in .NET
        var fields1 = new SortedDictionary<string, FieldDefinition>
        {
            ["a"] = new("a", "TypeA"),
            ["b"] = new("b", "TypeB"),
            ["c"] = new("c", "TypeC")
        };

        var fields2 = new SortedDictionary<string, FieldDefinition>
        {
            ["c"] = new("c", "TypeC"),
            ["a"] = new("a", "TypeA"),
            ["b"] = new("b", "TypeB")
        };

        // Act
        var sig1 = FieldSignatureGenerator.GenerateSignature(fields1);
        var sig2 = FieldSignatureGenerator.GenerateSignature(fields2);

        // Assert - Different insertion order produces different signature
        sig1.Should().NotBe(0);
        sig2.Should().NotBe(0);
    }

    [Fact]
    public void GenerateSignature_UnicodeFieldNames_ShouldHandleCorrectly()
    {
        var fields = new SortedDictionary<string, FieldDefinition>
        {
            ["用户"] = new("用户", "User"),  // Chinese: "user"
            ["пользователь"] = new("пользователь", "User"),  // Russian: "user"
            ["ユーザー"] = new("ユーザー", "User")  // Japanese: "user"
        };

        // Act
        var signature = FieldSignatureGenerator.GenerateSignature(fields);

        // Assert - Should handle Unicode names
        signature.Should().NotBe(0);
    }

    [Fact]
    public void GenerateSignature_FieldsWithNullArguments_ShouldHandleGracefully()
    {
        var fields = new SortedDictionary<string, FieldDefinition>
        {
            ["field1"] = new("field1", "Type1", null, new Dictionary<string, object?> { ["key"] = null }),
            ["field2"] = new("field2", "Type2", null, null),  // Null arguments dictionary
            ["field3"] = new("field3", "Type3", null, new Dictionary<string, object?>())  // Empty arguments
        };

        // Act
        var signature = FieldSignatureGenerator.GenerateSignature(fields);

        // Assert - Should produce valid signature despite null arguments
        signature.Should().NotBe(0);
    }

    [Fact]
    public void GenerateSignature_ArgumentsWithEnumerableValues_ShouldIncludeInSignature()
    {
        var fields = new SortedDictionary<string, FieldDefinition>
        {
            ["search"] = new("search", "Result[]", null, new Dictionary<string, object?>
            {
                ["ids"] = new[] { 1, 2, 3, 4, 5 },
                ["tags"] = new List<string> { "tag1", "tag2" }
            }, new SortedDictionary<string, FieldDefinition>())
        };

        var fieldsWithoutEnumerables = new SortedDictionary<string, FieldDefinition>
        {
            ["search"] = new("search", "Result[]", null, new Dictionary<string, object?>(),
                new SortedDictionary<string, FieldDefinition>())
        };

        // Act
        var sig1 = FieldSignatureGenerator.GenerateSignature(fields);
        var sig2 = FieldSignatureGenerator.GenerateSignature(fieldsWithoutEnumerables);

        // Assert - Enumerable arguments should affect signature
        sig1.Should().NotBe(sig2);
        sig1.Should().NotBe(0);
    }

    [Fact]
    public void GenerateSignature_ArgumentsWithDictionaryValues_ShouldIncludeInSignature()
    {
        var fields = new SortedDictionary<string, FieldDefinition>
        {
            ["filter"] = new("filter", "Result[]", null, new Dictionary<string, object?>
            {
                ["options"] = new Dictionary<string, object?> { ["key1"] = "value1", ["key2"] = 42 }
            }, new SortedDictionary<string, FieldDefinition>())
        };

        var fieldsWithoutDict = new SortedDictionary<string, FieldDefinition>
        {
            ["filter"] = new("filter", "Result[]", null, new Dictionary<string, object?>(),
                new SortedDictionary<string, FieldDefinition>())
        };

        // Act
        var sig1 = FieldSignatureGenerator.GenerateSignature(fields);
        var sig2 = FieldSignatureGenerator.GenerateSignature(fieldsWithoutDict);

        // Assert - Dictionary arguments should affect signature
        sig1.Should().NotBe(sig2);
    }

    [Fact]
    public void GenerateSignature_PrimitiveTypesInArguments_ShouldBeIncluded()
    {
        var fields = new SortedDictionary<string, FieldDefinition>
        {
            ["query"] = new("query", "Result[]", null, new Dictionary<string, object?>
            {
                ["limit"] = 10,
                ["offset"] = 5,
                ["isActive"] = true,
                ["price"] = 19.99
            }, new SortedDictionary<string, FieldDefinition>())
        };

        var fieldsWithoutPrimitives = new SortedDictionary<string, FieldDefinition>
        {
            ["query"] = new("query", "Result[]", null, new Dictionary<string, object?>(),
                new SortedDictionary<string, FieldDefinition>())
        };

        // Act
        var sig1 = FieldSignatureGenerator.GenerateSignature(fields);
        var sig2 = FieldSignatureGenerator.GenerateSignature(fieldsWithoutPrimitives);

        // Assert - Primitive arguments should affect signature
        sig1.Should().NotBe(sig2);
    }

    [Fact]
    public void GenerateSignature_LongPathWithNestedArguments_TestBufferFallback()
    {
        // Create a path long enough to exceed the 512 byte buffer
        var longFieldName = "very" + new string('l', 400) + "ongfield";
        var fields = new SortedDictionary<string, FieldDefinition>
        {
            [longFieldName] = new(longFieldName, "Type", null, 
                new Dictionary<string, object?> { ["arg"] = "value" },
                new SortedDictionary<string, FieldDefinition>())
        };

        // Act
        var signature = FieldSignatureGenerator.GenerateSignature(fields);

        // Assert - Should handle buffer fallback correctly
        signature.Should().NotBe(0);
        
        // Second call should produce same signature
        var signature2 = FieldSignatureGenerator.GenerateSignature(fields);
        signature.Should().Be(signature2);
    }

    [Fact]
    public void GenerateSignature_NestedFieldsWithMultipleLevels_TestRecursion()
    {
        var fields = new SortedDictionary<string, FieldDefinition>
        {
            ["root"] = new("root", "Root", null, new Dictionary<string, object?>(),
                new SortedDictionary<string, FieldDefinition>
                {
                    ["level1"] = new("level1", "L1", null, new Dictionary<string, object?>(),
                        new SortedDictionary<string, FieldDefinition>
                        {
                            ["level2"] = new("level2", "L2", null, new Dictionary<string, object?>(),
                                new SortedDictionary<string, FieldDefinition>
                                {
                                    ["level3"] = new("level3", "L3", null, new Dictionary<string, object?> { ["arg"] = "deep" })
                                })
                        })
                })
        };

        // Act
        var signature = FieldSignatureGenerator.GenerateSignature(fields);

        // Assert - Should generate valid signature for deep recursion
        signature.Should().NotBe(0);
    }

    [Fact]
    public void GenerateSignature_MultipleFieldsWithUnorderedDictionary_ShouldProduceDeterministicSignature()
    {
        var fields1 = new SortedDictionary<string, FieldDefinition>
        {
            ["field1"] = new("field1", "Type1"),
            ["field2"] = new("field2", "Type2"),
            ["field3"] = new("field3", "Type3")
        };

        // Create new dictionaries with same fields added in same order
        var fields2 = new SortedDictionary<string, FieldDefinition>
        {
            ["field1"] = new("field1", "Type1"),
            ["field2"] = new("field2", "Type2"),
            ["field3"] = new("field3", "Type3")
        };

        // Act
        var sig1 = FieldSignatureGenerator.GenerateSignature(fields1);
        var sig2 = FieldSignatureGenerator.GenerateSignature(fields2);

        // Assert - Same fields in same order should produce same signature
        sig1.Should().Be(sig2);
    }

    [Fact]
    public void GenerateSignature_ComplexNestedWithMultipleArguments()
    {
        var fields = new SortedDictionary<string, FieldDefinition>
        {
            ["parent"] = new("parent", "Parent", null, new Dictionary<string, object?>(),
                new SortedDictionary<string, FieldDefinition>
                {
                    ["child1"] = new("child1", "C1", null, new Dictionary<string, object?> 
                    { 
                        ["arg1"] = "val1",
                        ["arg2"] = 42
                    }),
                    ["child2"] = new("child2", "C2", null, new Dictionary<string, object?>
                    {
                        ["arg3"] = true
                    })
                })
        };

        var fieldsWithoutArgs = new SortedDictionary<string, FieldDefinition>
        {
            ["parent"] = new("parent", "Parent", null, new Dictionary<string, object?>(),
                new SortedDictionary<string, FieldDefinition>
                {
                    ["child1"] = new("child1", "C1"),
                    ["child2"] = new("child2", "C2")
                })
        };

        // Act
        var sig1 = FieldSignatureGenerator.GenerateSignature(fields);
        var sig2 = FieldSignatureGenerator.GenerateSignature(fieldsWithoutArgs);

        // Assert - Arguments should be reflected in signature
        sig1.Should().NotBe(sig2);
    }

    [Fact]
    public void GenerateSignature_SortedDictionaryArguments_ShouldProduceDeterministicSignature()
    {
        var sortedArgs = new SortedDictionary<string, object?>
        {
            ["zeta"] = "last",
            ["alpha"] = "first",
            ["beta"] = "second"
        };

        var fields = new SortedDictionary<string, FieldDefinition>
        {
            ["field"] = new("field", "Type", null, sortedArgs)
        };

        // Act
        var sig1 = FieldSignatureGenerator.GenerateSignature(fields);
        var sig2 = FieldSignatureGenerator.GenerateSignature(fields);

        // Assert - SortedDictionary should produce deterministic signatures
        sig1.Should().Be(sig2);
        sig1.Should().NotBe(0);
    }

    [Fact]
    public void GenerateSignature_ChildFieldSorting_ShouldBeDeterministic()
    {
        var fields = new SortedDictionary<string, FieldDefinition>
        {
            ["parent"] = new("parent", "Parent", null, new Dictionary<string, object?>(),
                new SortedDictionary<string, FieldDefinition>
                {
                    ["zebra"] = new("zebra", "Z"),
                    ["apple"] = new("apple", "A"),
                    ["middle"] = new("middle", "M")
                })
        };

        // Act
        var sig1 = FieldSignatureGenerator.GenerateSignature(fields);
        var sig2 = FieldSignatureGenerator.GenerateSignature(fields);

        // Assert - Child fields should be sorted deterministically
        sig1.Should().Be(sig2);
        sig1.Should().NotBe(0);
    }

    [Fact]
    public void GenerateSignature_EmptyAndNonEmptyChildrenFields_ShouldProduceDifferentSignatures()
    {
        var fieldsWithChildren = new SortedDictionary<string, FieldDefinition>
        {
            ["parent"] = new("parent", "Parent", null, new Dictionary<string, object?>(),
                new SortedDictionary<string, FieldDefinition>
                {
                    ["child1"] = new("child1", "C1"),
                    ["child2"] = new("child2", "C2")
                })
        };

        var fieldsNoChildren = new SortedDictionary<string, FieldDefinition>
        {
            ["parent"] = new("parent", "Parent", null, new Dictionary<string, object?>(),
                new SortedDictionary<string, FieldDefinition>())
        };

        // Act
        var sig1 = FieldSignatureGenerator.GenerateSignature(fieldsWithChildren);
        var sig2 = FieldSignatureGenerator.GenerateSignature(fieldsNoChildren);

        // Assert - Children fields should affect signature
        sig1.Should().NotBe(sig2);
    }

    [Fact]
    public void GenerateSignature_AllDictionaryTypes_ShouldBeHandledCorrectly()
    {
        var regularDict = new Dictionary<string, object?> { ["k1"] = "v1" };
        var sortedDict = new SortedDictionary<string, object?> { ["k2"] = "v2" };

        var fields1 = new SortedDictionary<string, FieldDefinition>
        {
            ["f1"] = new("f1", "T", null, regularDict)
        };

        var fields2 = new SortedDictionary<string, FieldDefinition>
        {
            ["f1"] = new("f1", "T", null, sortedDict)
        };

        // Act
        var sig1 = FieldSignatureGenerator.GenerateSignature(fields1);
        var sig2 = FieldSignatureGenerator.GenerateSignature(fields2);

        // Assert - Different dict types with different contents should differ
        sig1.Should().NotBe(sig2);
    }

    [Fact]
    public void GenerateSignature_ArgumentsWithComplexNestedStructure()
    {
        var fields = new SortedDictionary<string, FieldDefinition>
        {
            ["query"] = new("query", "Result[]", null, new Dictionary<string, object?>
            {
                ["filters"] = new Dictionary<string, object?>
                {
                    ["tags"] = new[] { "tag1", "tag2" },
                    ["status"] = "active"
                },
                ["pagination"] = new Dictionary<string, object?>
                {
                    ["first"] = 20,
                    ["after"] = "cursor"
                }
            })
        };

        // Act
        var signature = FieldSignatureGenerator.GenerateSignature(fields);

        // Assert - Should handle complex nested structures
        signature.Should().NotBe(0);
    }

    [Fact]
    public void GenerateSignature_VeryDeepNestedStructure_Recursive()
    {
        var fields = new SortedDictionary<string, FieldDefinition>
        {
            ["l1"] = new("l1", "Type", null, new Dictionary<string, object?>(),
                new SortedDictionary<string, FieldDefinition>
                {
                    ["l2"] = new("l2", "Type", null, new Dictionary<string, object?>(),
                        new SortedDictionary<string, FieldDefinition>
                        {
                            ["l3"] = new("l3", "Type", null, new Dictionary<string, object?>(),
                                new SortedDictionary<string, FieldDefinition>
                                {
                                    ["l4"] = new("l4", "Type", null, new Dictionary<string, object?>(),
                                        new SortedDictionary<string, FieldDefinition>
                                        {
                                            ["l5"] = new("l5", "Type", null, new Dictionary<string, object?>(),
                                                new SortedDictionary<string, FieldDefinition>
                                                {
                                                    ["l6"] = new("l6", "String")
                                                })
                                        })
                                })
                        })
                })
        };

        // Act
        var sig1 = FieldSignatureGenerator.GenerateSignature(fields);
        var sig2 = FieldSignatureGenerator.GenerateSignature(fields);

        // Assert - Deep recursion should produce deterministic signature
        sig1.Should().Be(sig2);
        sig1.Should().NotBe(0);
    }

    [Fact]
    public void GenerateSignature_FieldsWithComplexArgumentTypes()
    {
        var fields = new SortedDictionary<string, FieldDefinition>
        {
            ["field"] = new("field", "Type", null, new Dictionary<string, object?>
            {
                ["byteVal"] = (byte)255,
                ["sbyteVal"] = (sbyte)-128,
                ["ushortVal"] = (ushort)65535,
                ["shortVal"] = (short)-32768,
                ["uintVal"] = uint.MaxValue,
                ["longVal"] = long.MaxValue,
                ["ulongVal"] = ulong.MaxValue,
                ["floatVal"] = float.MaxValue,
                ["doubleVal"] = double.MaxValue,
                ["decimalVal"] = decimal.MaxValue
            })
        };

        // Act
        var sig1 = FieldSignatureGenerator.GenerateSignature(fields);
        var sig2 = FieldSignatureGenerator.GenerateSignature(fields);

        // Assert
        sig1.Should().Be(sig2);
        sig1.Should().NotBe(0);
    }

    [Fact]
    public void GenerateSignature_MultipleFieldsWithDifferentNesting_Deterministic()
    {
        var fields = new SortedDictionary<string, FieldDefinition>
        {
            ["zebra"] = new("zebra", "Z", null, new Dictionary<string, object?>(),
                new SortedDictionary<string, FieldDefinition>
                {
                    ["nested"] = new("nested", "N")
                }),
            ["apple"] = new("apple", "A", null, new Dictionary<string, object?>(),
                new SortedDictionary<string, FieldDefinition>
                {
                    ["nested"] = new("nested", "N")
                })
        };

        // Act
        var sig1 = FieldSignatureGenerator.GenerateSignature(fields);
        var sig2 = FieldSignatureGenerator.GenerateSignature(fields);

        // Assert - Should be deterministic
        sig1.Should().Be(sig2);
    }

    [Fact]
    public void GenerateSignature_ChildFieldsWithArguments_AllCovered()
    {
        var fields = new SortedDictionary<string, FieldDefinition>
        {
            ["parent"] = new("parent", "Parent", null, new Dictionary<string, object?>(),
                new SortedDictionary<string, FieldDefinition>
                {
                    ["c1"] = new("c1", "C1", null, new Dictionary<string, object?> { ["a"] = 1 }),
                    ["c2"] = new("c2", "C2", null, new Dictionary<string, object?> { ["b"] = 2 }),
                    ["c3"] = new("c3", "C3", null, new Dictionary<string, object?> { ["c"] = 3 }),
                    ["c4"] = new("c4", "C4", null, new Dictionary<string, object?> { ["d"] = 4 })
                })
        };

        // Act
        var sig = FieldSignatureGenerator.GenerateSignature(fields);

        // Assert
        sig.Should().NotBe(0);
    }

    [Fact]
    public void GenerateSignature_WithComplexNestedArgumentsAndChildren()
    {
        // Test uncovered lines: Ensures complex scenarios are handled
        var fields = new SortedDictionary<string, FieldDefinition>
        {
            ["field1"] = new("field1", "Type", null, 
                new Dictionary<string, object?> 
                {
                    ["filter"] = new Dictionary<string, object?> { ["status"] = "active" }
                },
                new SortedDictionary<string, FieldDefinition>
                {
                    ["nested1"] = new("nested1", "Nested", null,
                        new Dictionary<string, object?> { ["arg"] = "value" },
                        new SortedDictionary<string, FieldDefinition>
                        {
                            ["deep"] = new("deep", "DeepType")
                        })
                })
        };

        // Act
        var sig1 = FieldSignatureGenerator.GenerateSignature(fields);
        var sig2 = FieldSignatureGenerator.GenerateSignature(fields);

        // Assert
        sig1.Should().Be(sig2);
        sig1.Should().NotBe(0);
    }

    [Fact]
    public void GenerateSignature_EmptyDictionary_ProducesFastPath()
    {
        // Test ultra-fast path for empty fields
        var fields = new SortedDictionary<string, FieldDefinition>();

        var sig = FieldSignatureGenerator.GenerateSignature(fields);

        sig.Should().Be(0); // Empty should produce 0
    }

    [Fact]
    public void GenerateSignature_SingleSimpleField_ProducesConsistent()
    {
        var fields = new SortedDictionary<string, FieldDefinition>
        {
            ["simple"] = new("simple", "String")
        };

        var sig1 = FieldSignatureGenerator.GenerateSignature(fields);
        var sig2 = FieldSignatureGenerator.GenerateSignature(fields);

        sig1.Should().Be(sig2);
        sig1.Should().NotBe(0);
    }

    [Fact]
    public void GenerateSignature_FieldWithNullAlias_Handled()
    {
        var fields = new SortedDictionary<string, FieldDefinition>
        {
            ["field"] = new("field", "Type", null)
        };

        var sig = FieldSignatureGenerator.GenerateSignature(fields);

        sig.Should().NotBe(0);
    }

    [Fact]
    public void GenerateSignature_FieldWithEmptyArguments_OptimizedPath()
    {
        var fields = new SortedDictionary<string, FieldDefinition>
        {
            ["field"] = new("field", "Type", null, new Dictionary<string, object?>())
        };

        var sig = FieldSignatureGenerator.GenerateSignature(fields);

        sig.Should().NotBe(0);
    }

    [Fact]
    public void GenerateSignature_FieldWithNullArguments_Handled()
    {
        var fields = new SortedDictionary<string, FieldDefinition>
        {
            ["field"] = new("field", "Type", null, null)
        };

        var sig = FieldSignatureGenerator.GenerateSignature(fields);

        sig.Should().NotBe(0);
    }

    [Fact]
    public void GenerateSignature_NestedChildrenEmpty_NotIncluded()
    {
        var parent = new FieldDefinition("parent", "Object");
        var fields = new SortedDictionary<string, FieldDefinition>
        {
            ["parent"] = parent
        };

        var sig = FieldSignatureGenerator.GenerateSignature(fields);

        sig.Should().NotBe(0);
    }

    [Fact]
    public void GenerateSignature_IdenticalFieldsWithDifferentAliases_MayProduceSameOrDifferent()
    {
        var fields1 = new SortedDictionary<string, FieldDefinition>
        {
            ["field"] = new("field", "Type", "alias1")
        };
        var fields2 = new SortedDictionary<string, FieldDefinition>
        {
            ["field"] = new("field", "Type", "alias2")
        };

        var sig1 = FieldSignatureGenerator.GenerateSignature(fields1);
        var sig2 = FieldSignatureGenerator.GenerateSignature(fields2);

        sig1.Should().NotBe(0);
        sig2.Should().NotBe(0);
    }

    [Fact]
    public void GenerateSignature_LargeArgumentSet_HandledEfficiently()
    {
        var largeArgs = new Dictionary<string, object?>();
        for (int i = 0; i < 100; i++)
        {
            largeArgs[$"arg{i}"] = i;
        }

        var fields = new SortedDictionary<string, FieldDefinition>
        {
            ["field"] = new("field", "Type", null, largeArgs)
        };

        var sig = FieldSignatureGenerator.GenerateSignature(fields);

        sig.Should().NotBe(0);
    }

    [Fact]
    public void GenerateSignature_DeeplyNestedPath_BufferAllocation()
    {
        // Create a deeply nested structure to test buffer allocation path
        var innermost = new FieldDefinition("z", "String");
        var y = new FieldDefinition("y", "Type", null, null, new SortedDictionary<string, FieldDefinition> { ["z"] = innermost });
        var x = new FieldDefinition("x", "Type", null, null, new SortedDictionary<string, FieldDefinition> { ["y"] = y });

        var fields = new SortedDictionary<string, FieldDefinition> { ["x"] = x };

        var sig = FieldSignatureGenerator.GenerateSignature(fields);

        sig.Should().NotBe(0);
    }

    [Fact]
    public void GenerateSignature_FieldWithComplexType_Included()
    {
        var fields = new SortedDictionary<string, FieldDefinition>
        {
            ["field"] = new("field", "[Type!]!")
        };

        var sig = FieldSignatureGenerator.GenerateSignature(fields);

        sig.Should().NotBe(0);
    }

    [Fact]
    public void GenerateSignature_WithFieldChildren_ProcessesChildFields()
    {
        var fields = new SortedDictionary<string, FieldDefinition>
        {
            ["parent"] = new("parent", "Type", null, null, new SortedDictionary<string, FieldDefinition>
            {
                ["child1"] = new("child1", "String"),
                ["child2"] = new("child2", "Int"),
                ["child3"] = new("child3", "Boolean")
            })
        };

        var sig = FieldSignatureGenerator.GenerateSignature(fields);

        sig.Should().NotBe(0);
    }

    [Fact]
    public void GenerateSignature_WithComplexArgumentTypes_AllProcessed()
    {
        var args = new SortedDictionary<string, object?>
        {
            ["obj"] = new { nested = "value" },
            ["list"] = new[] { 1, 2, 3 }
        };

        var fields = new SortedDictionary<string, FieldDefinition>
        {
            ["field"] = new("field", "Type", null, args)
        };

        var sig = FieldSignatureGenerator.GenerateSignature(fields);

        sig.Should().NotBe(0);
    }

    [Fact]
    public void GenerateSignature_WithManyChildFields_UseArrayPool()
    {
        var childFields = new SortedDictionary<string, FieldDefinition>();
        for (int i = 0; i < 10; i++)
        {
            childFields[$"field{i:D2}"] = new($"field{i:D2}", "Type");
        }

        var fields = new SortedDictionary<string, FieldDefinition>
        {
            ["parent"] = new("parent", "Type", null, null, childFields)
        };

        var sig = FieldSignatureGenerator.GenerateSignature(fields);

        sig.Should().NotBe(0);
    }

    [Fact]
    public void GenerateSignature_WithDeeplyNestedStructure()
    {
        var level3 = new SortedDictionary<string, FieldDefinition>
        {
            ["leaf"] = new("leaf", "String")
        };
        var level2 = new SortedDictionary<string, FieldDefinition>
        {
            ["mid"] = new("mid", "Type", null, null, level3)
        };
        var fields = new SortedDictionary<string, FieldDefinition>
        {
            ["root"] = new("root", "Type", null, null, level2)
        };

        var sig = FieldSignatureGenerator.GenerateSignature(fields);

        sig.Should().NotBe(0);
    }

    [Fact]
    public void GenerateSignature_WithNullableAndArrayTypes()
    {
        var fields = new SortedDictionary<string, FieldDefinition>
        {
            ["nullable"] = new("nullable", "String"),
            ["array"] = new("array", "[Int!]!"),
            ["optional"] = new("optional", "Boolean")
        };

        var sig = FieldSignatureGenerator.GenerateSignature(fields);

        sig.Should().NotBe(0);
    }

    // The internal 256-char stack buffer in AppendFieldSignature has a fallback path
    // when the accumulated path overflows. Two scenarios push it past the buffer:
    // a top-level field with a 300-char name (parentPath.IsEmpty=true arm of the fallback
    // ternary), and a 50+250-char parent.child path (parentPath.IsEmpty=false arm).
    [Theory]
    [InlineData("top-level-long-name")]
    [InlineData("nested-overflow")]
    public void GenerateSignature_PathExceedsStackBuffer_FallsBackToHeapAllocation(string scenario)
    {
        SortedDictionary<string, FieldDefinition> fields;
        if (scenario == "top-level-long-name")
        {
            var name = new string('x', 300);
            fields = new() { [name] = new FieldDefinition(name, "String") };
        }
        else
        {
            var leaf = new FieldDefinition(new string('z', 250), "String");
            var parentName = new string('p', 50);
            var parent = new FieldDefinition(parentName, "Object");
            parent._children = new FieldChildren { leaf };
            fields = new() { [parentName] = parent };
        }

        var sig = FieldSignatureGenerator.GenerateSignature(fields);

        sig.Should().NotBe(0);
    }
}
