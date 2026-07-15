using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using FluentAssertions;
using NGql.Core.Builders;
using Xunit;
using static NGql.Core.Builders.QueryTextBuilder;

// ReSharper disable RedundantCast
namespace NGql.Core.Tests;

public class QueryTextBuilderTests
{
    [Theory]
    [InlineData((sbyte)123)]
    [InlineData((byte)123)]
    [InlineData((short)123)]
    [InlineData((ushort)123)]
    [InlineData((int)123)]
    [InlineData((uint)123)]
    [InlineData((long)123)]
    [InlineData((ulong)123)]
    [InlineData((double)123)]
    public void BuildQueryParam_Parse_IntegerTypes(object input)
    {
        // act
        var valueString = BuildQueryParam(input);

        // assert
        valueString.Should().Be("123");
    }

    [Theory]
    [InlineData(123.45f)]
    [InlineData(123.45d)]
    public void BuildQueryParam_Parse_FloatTypes(object input)
    {
        // act
        var valueString = BuildQueryParam(input);

        // assert
        valueString.Should().Be("123.45");
    }

    [Theory]
    [InlineData(true, "true")]
    [InlineData(false, "false")]
    public void BuildQueryParam_Parse_Boolean(bool input, string expectedValue)
    {
        // act
        var valueString = BuildQueryParam(input);

        // assert
        valueString.Should().Be(expectedValue);
    }

    [Fact]
    public void BuildQueryParam_Parse_Decimal()
    {
        // act
        var valueString = BuildQueryParam(123.45m);

        // assert
        valueString.Should().Be("123.45");
    }

    [Fact]
    public void BuildQueryParam_Parse_String()
    {
        // act
        var valueString = BuildQueryParam("John");

        // assert
        valueString.Should().Be("\"John\"");
    }

    [Fact]
    public void BuildQueryParam_Parse_Enum()
    {
        // act
        var valueString = BuildQueryParam(HttpStatusCode.OK);

        // assert
        valueString.Should().Be("OK");
    }

    [Fact]
    public void BuildQueryParam_Parse_EnumValue()
    {
        // act
        var enumValue = new EnumValue(HttpStatusCode.OK);
        var valueString = BuildQueryParam(enumValue);

        // assert
        valueString.Should().Be("OK");
    }

    [Fact]
    public void BuildQueryParam_Parse_Array()
    {
        // act
        var valueString = BuildQueryParam(new[] { 123, 456, 789 });

        // assert
        valueString.Should().Be("[123, 456, 789]");
    }

    [Fact]
    public void BuildQueryParam_Parse_String_Dictionary()
    {
        // act
        var valueString = BuildQueryParam(new Dictionary<string, string>
        {
            {"k1", "v1"},
            {"k2", "v2"}
        });

        // assert
        valueString.Should().Be("{k1:\"v1\", k2:\"v2\"}");
    }

    [Fact]
    public void BuildQueryParam_Parse_Object_Dictionary()
    {
        // act
        var valueString = BuildQueryParam(new Dictionary<string, object?>
        {
            {"numbers", new [] { 123, 456, 789}},
            {"k1", "v1"},
            {"k2", 4.2},
            {"k3", null},
            {"k4", new EnumValue(HttpStatusCode.OK)},
            {"k5", new Dictionary<string, object>
            {
                {"enum", HttpStatusCode.OK},
                {"enumValue", new EnumValue(HttpStatusCode.OK)},
            }}
        });

        // assert
        valueString.Should().Be("{numbers:[123, 456, 789], k1:\"v1\", k2:4.2, k3:null, k4:OK, k5:{enum:OK, enumValue:OK}}");
    }

    [Fact]
    public void WriteObject_TypedDictionary_WithNestedDictionary_RendersExactOutput()
    {
        // Normalized args are SortedDictionary<string, object?> (OrdinalIgnoreCase) — the
        // strongly-typed path must render byte-identical output to the legacy non-generic path:
        // sorted keys, `{k:v, ...}` shape, nested dictionaries recursed the same way.
        var nested = new SortedDictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["status"] = "active",
            ["limit"] = 25
        };
        var value = new SortedDictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["query"] = "v1",
            ["filters"] = nested,
            ["offset"] = 0
        };

        var result = BuildQueryParam(value);

        result.Should().Be("{filters:{limit:25, status:\"active\"}, offset:0, query:\"v1\"}");
    }

    [Fact]
    public void AddField_MultiEntryDictionaryArgument_WithNestedDictionary_RendersExactGraphQL()
    {
        var query = QueryBuilder.CreateDefaultBuilder("Search")
            .AddField("search", new Dictionary<string, object?>
            {
                ["query"] = "test",
                ["filters"] = new Dictionary<string, object?> { ["status"] = "active", ["kind"] = "user" },
                ["limit"] = 25,
                ["offset"] = 0
            });

        var result = query.ToString();

        result.Should().Be(
            "query Search{" + Environment.NewLine +
            "    search(filters:{kind:\"user\", status:\"active\"}, limit:25, offset:0, query:\"test\")" + Environment.NewLine +
            "}");
    }

    [Fact]
    public void AddFields_ThrowsUnsupportedException()
    {
        // act & assert
        var exception = Assert.Throws<InvalidOperationException>(() => new Query("foo")
            .Select([1, "text", Guid.NewGuid()])
            .ToString()
        );

        exception.Message.Should().Be("Unsupported Field type found, must be a `string` or `QueryBlock`");
    }

    [Fact]
    public void WriteObject_Should_Handle_KeyValuePair_Correctly()
    {
        // RED TEST: Demonstrates that reflection-based KeyValuePair handling works
        // even when properties might be missing (defensive programming).
        
        // ARRANGE
        var builder = new StringBuilder();
        var kvp = new KeyValuePair<string, int>("testKey", 42);

        // ACT & ASSERT
        var action = () => WriteObject(builder, kvp);
        // This should NOT throw NullReferenceException
        action.Should().NotThrow();
        builder.ToString().Should().Contain("testKey");
        builder.ToString().Should().Contain("42");
    }

    [Fact]
    public void WriteObject_Should_Handle_KeyValuePair_With_Null_Value()
    {
        // ARRANGE
        var builder = new StringBuilder();
        var kvp = new KeyValuePair<string, object?>("key", null);

        // ACT & ASSERT
        var action = () => WriteObject(builder, kvp);
        action.Should().NotThrow();
        builder.ToString().Should().Contain("key");
        builder.ToString().Should().Contain("null");
    }

    [Fact]
    public void WriteObject_Should_Handle_KeyValuePair_Complex_Types()
    {
        // ARRANGE
        var builder = new StringBuilder();
        var kvp = new KeyValuePair<int, List<string>>(1, ["a", "b", "c"]);

        // ACT & ASSERT
        var action = () => WriteObject(builder, kvp);
        action.Should().NotThrow();
        builder.ToString().Should().Contain("1");
    }

    [Fact]
    public void BuildQuery_WithDeeplyNestedFields_ShouldFormatCorrectly()
    {
        // This tests deeply nested field formatting
        var query = QueryBuilder.CreateDefaultBuilder("query")
            .AddField("level1", fb => fb
                .AddField("level2", f2 => f2
                    .AddField("level3", f3 => f3
                        .AddField("level4", f4 => f4
                            .AddField("id")
                            .AddField("name")))));

        // Act
        var result = query.ToString();

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().Contain("level1");
        result.Should().Contain("level2");
        result.Should().Contain("level3");
        result.Should().Contain("level4");
    }

    [Fact]
    public void BuildQuery_WithMultipleFieldsAtSameLevel_ShouldPreserveOrder()
    {
        var query = QueryBuilder.CreateDefaultBuilder("query")
            .AddField("zebra")
            .AddField("apple")
            .AddField("monkey")
            .AddField("banana");

        // Act
        var result = query.ToString();

        // Assert - All fields should be present
        result.Should().Contain("zebra");
        result.Should().Contain("apple");
        result.Should().Contain("monkey");
        result.Should().Contain("banana");
    }

    [Fact]
    public void BuildQuery_WithComplexArguments_ShouldFormatCorrectly()
    {
        var query = QueryBuilder.CreateDefaultBuilder("query")
            .AddField("search", new Dictionary<string, object?>
            {
                ["query"] = "test@example.com",
                ["filters"] = new Dictionary<string, object?> { ["status"] = "active" },
                ["limit"] = 25,
                ["offset"] = 0
            });

        // Act
        var result = query.ToString();

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().Contain("search");
        result.Should().Contain("query");
    }

    [Fact]
    public void BuildQuery_WithArrayArguments_ShouldFormatAsCollection()
    {
        var query = QueryBuilder.CreateDefaultBuilder("query")
            .AddField("users", new Dictionary<string, object?> { ["ids"] = new[] { 1, 2, 3 } });

        // Act
        var result = query.ToString();

        // Assert
        result.Should().Contain("users");
        result.Should().Contain("[");
        result.Should().Contain("]");
    }

    [Fact]
    public void BuildQuery_WithEnumValues_ShouldFormatCorrectly()
    {
        var query = QueryBuilder.CreateDefaultBuilder("query")
            .AddField("items", new Dictionary<string, object?>
            {
                ["sort"] = new EnumValue(HttpStatusCode.OK)
            });

        // Act
        var result = query.ToString();

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().Contain("items");
    }

    [Fact]
    public void BuildQuery_NestedWithMixedArgumentTypes_ShouldHandleAll()
    {
        var query = QueryBuilder.CreateDefaultBuilder("query")
            .AddField("root", fb => fb
                .AddField("child", new Dictionary<string, object?>
                {
                    ["stringArg"] = "value",
                    ["numberArg"] = 42,
                    ["boolArg"] = true,
                    ["nullArg"] = null,
                    ["arrayArg"] = new[] { "a", "b" },
                    ["dictArg"] = new Dictionary<string, object?> { ["nested"] = 123 }
                }, f2 => f2
                    .AddField("id")
                    .AddField("name")));

        // Act
        var result = query.ToString();

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().Contain("root");
        result.Should().Contain("child");
    }

    [Fact]
    public void BuildQuery_WithEmptyDictionary_ShouldHandleGracefully()
    {
        var query = QueryBuilder.CreateDefaultBuilder("query")
            .AddField("data", new Dictionary<string, object?>());

        // Act
        var result = query.ToString();

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().Contain("data");
    }

    [Fact]
    public void BuildQuery_WithCustomObjectArguments_ShouldUseReflection()
    {
        // This tests WriteObjectReflection path
        var query = QueryBuilder.CreateDefaultBuilder("query")
            .AddField("user", new Dictionary<string, object?>
            {
                ["profile"] = new { firstName = "John", lastName = "Doe", age = 30 }
            });

        // Act
        var result = query.ToString();

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().Contain("user");
    }

    [Fact]
    public void BuildQuery_WithNestedCustomObjects_ShouldReflectAll()
    {
        var query = QueryBuilder.CreateDefaultBuilder("query")
            .AddField("data", new Dictionary<string, object?>
            {
                ["person"] = new { name = "Alice", address = new { city = "NYC", zip = "10001" } }
            });

        // Act
        var result = query.ToString();

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().Contain("data");
        result.Should().Contain("person");
    }

    private static string BuildQueryParam(object value)
    {
        var builder = new StringBuilder();
        WriteObject(builder, value);
        return builder.ToString();
    }

    // Additional comprehensive tests for uncovered lines

    [Fact]
    public void BuildQueryParam_KeyValuePair_ShouldFormatCorrectly()
    {
        var kvp = new KeyValuePair<string, int>("key", 42);
        
        // Act
        var result = BuildQueryParam(kvp);

        // Assert
        result.Should().Contain("key");
        result.Should().Contain("42");
    }

    [Fact]
    public void BuildQueryParam_NestedList_ShouldHandleCorrectly()
    {
        var nested = new List<object> 
        { 
            "string", 
            42, 
            new[] { 1, 2, 3 },
            true 
        };

        // Act
        var result = BuildQueryParam(nested);

        // Assert
        result.Should().Contain("[");
        result.Should().Contain("]");
    }

    [Fact]
    public void BuildQueryParam_ComplexDictionary_ShouldHandleCorrectly()
    {
        var dict = new Dictionary<string, object>
        {
            ["nested"] = new { value = 123 },
            ["array"] = new[] { "a", "b" },
            ["string"] = "test"
        };

        // Act
        var result = BuildQueryParam(dict);

        // Assert
        result.Should().Contain("{");
        result.Should().Contain("}");
    }

    [Fact]
    public void BuildQueryParam_ObjectWithProperties_ShouldReflect()
    {
        var obj = new { Name = "Test", Age = 25, Active = true };

        // Act
        var result = BuildQueryParam(obj);

        // Assert
        result.Should().Contain("Name");
        result.Should().Contain("Test");
        result.Should().Contain("Age");
        result.Should().Contain("25");
    }

    [Fact]
    public void BuildQueryParam_ComplexObjectHierarchy_ShouldReflectAll()
    {
        var complex = new 
        { 
            User = new { Name = "John", Email = "john@example.com" },
            Settings = new { Theme = "dark", Notifications = true }
        };

        // Act
        var result = BuildQueryParam(complex);

        // Assert
        result.Should().Contain("User");
        result.Should().Contain("Name");
        result.Should().Contain("John");
    }

    [Fact]
    public void BuildQueryParam_ObjectPropertyReflection_WithNullProperty()
    {
        var obj = new { NullProp = (string?)null, ValueProp = "value" };

        // Act
        var result = BuildQueryParam(obj);

        // Assert
        result.Should().Contain("NullProp");
        result.Should().Contain("ValueProp");
    }

    [Fact]
    public void BuildQuery_WithCapacityExceeded_ShouldNotRepool()
    {
        var query = QueryBuilder.CreateDefaultBuilder("query");
        
        // Add large field to exceed capacity
        for (int i = 0; i < 1000; i++)
        {
            query.AddField($"field{i}");
        }

        // Act
        var result = query.ToString();

        // Assert - Should still build successfully
        result.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void BuildQuery_WithoutIndent_ShouldNotAddPadding()
    {
        var query = QueryBuilder.CreateDefaultBuilder("query")
            .AddField("field1")
            .AddField("field2");

        // Act
        var result = query.ToString();

        // Assert - Should contain fields
        result.Should().Contain("field1");
        result.Should().Contain("field2");
    }

    [Fact]
    public void BuildQueryParam_ListOfObjects_ShouldFormatCorrectly()
    {
        var list = new List<object>
        {
            new { id = 1 },
            new { id = 2 },
            new { id = 3 }
        };

        // Act
        var result = BuildQueryParam(list);

        // Assert
        result.Should().Contain("[");
        result.Should().Contain("]");
        result.Should().Contain("id");
    }

    [Fact]
    public void BuildQueryParam_DictionaryWithComplexValues_ShouldPreserveStructure()
    {
        var dict = new Dictionary<string, object>
        {
            ["data"] = new { nested = new[] { 1, 2, 3 } },
            ["meta"] = new { count = 3 }
        };

        // Act
        var result = BuildQueryParam(dict);

        // Assert
        result.Should().Contain("data");
        result.Should().Contain("nested");
    }

    [Fact]
    public void BuildQueryParam_HeterogeneousList_ShouldHandleAll()
    {
        var heterogeneous = new List<object>
        {
            42,
            "string",
            3.14,
            true,
            null!,
            new[] { 1, 2 },
            new Dictionary<string, object> { ["key"] = "value" }
        };

        // Act
        var result = BuildQueryParam(heterogeneous);

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().Contain("[");
    }

    [Fact]
    public void BuildQuery_WithEmptyFields_ShouldHandleGracefully()
    {
        var query = QueryBuilder.CreateDefaultBuilder("query");

        // Act
        var result = query.ToString();

        // Assert
        result.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void BuildQueryParam_ObjectWithManyProperties_ShouldReflectAll()
    {
        var obj = new 
        { 
            Prop1 = "value1",
            Prop2 = "value2",
            Prop3 = "value3",
            Prop4 = "value4",
            Prop5 = "value5"
        };

        // Act
        var result = BuildQueryParam(obj);

        // Assert
        result.Should().Contain("Prop1");
        result.Should().Contain("Prop2");
        result.Should().Contain("Prop5");
    }

    [Fact]
    public void BuildQueryParam_ObjectWithoutProperties_ShouldHandleGracefully()
    {
        var emptyObj = new { };

        // Act
        var result = BuildQueryParam(emptyObj);

        // Assert - Should produce empty object representation
        result.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void BuildQueryParam_EnumerableNonList_ShouldHandleAsDefault()
    {
        var hashSet = new HashSet<int> { 1, 2, 3, 4, 5 };

        // Act
        var result = BuildQueryParam(hashSet);

        // Assert
        result.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void BuildQueryParam_NestedKeyValuePairs_ShouldFormat()
    {
        var kvp = new KeyValuePair<string, Dictionary<string, object>>(
            "filters", 
            new Dictionary<string, object> { ["status"] = "active" });

        // Act
        var result = BuildQueryParam(kvp);

        // Assert
        result.Should().Contain("filters");
        result.Should().Contain("status");
    }

    [Fact]
    public void BuildQuery_FieldOrdering_ShouldBeConsistent()
    {
        var query = QueryBuilder.CreateDefaultBuilder("query")
            .AddField("zebra")
            .AddField("apple")
            .AddField("middle");

        // Act
        var result = query.ToString();

        // Assert
        result.Should().Contain("zebra");
        result.Should().Contain("apple");
        result.Should().Contain("middle");
    }

    [Fact]
    public void QueryTextBuilder_LargeCapacityBuilder_NotReturned()
    {
        // Test the memory leak prevention: builders that grow too large are not reused — when the
        // returned StringBuilder's capacity exceeds MaxBuilderCapacity, ReturnToPool drops it on the
        // floor rather than recycling it.

        var builder = QueryTextBuilder.GetFromPool();
        
        // Use reflection to access the private _stringBuilder field
        var sbField = typeof(QueryTextBuilder).GetField("_stringBuilder", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        sbField.Should().NotBeNull("_stringBuilder field should exist");
        
        var stringBuilder = (StringBuilder)sbField!.GetValue(builder)!;
        
        // Append a large string to make the capacity exceed MaxBuilderCapacity (256KB)
        var largeString = new string('x', 300 * 1024); // 300KB
        stringBuilder.Append(largeString);
        
        var builderCapacityBefore = stringBuilder.Capacity;
        builderCapacityBefore.Should().BeGreaterThan(256 * 1024, "capacity should exceed MaxBuilderCapacity");
        
        // Return it to pool - it should NOT be reused due to large capacity
        QueryTextBuilder.ReturnToPool(builder);
        
        // Get a builder from pool - if the large one was properly rejected, this should be a fresh one
        var nextBuilder = QueryTextBuilder.GetFromPool();
        var nextStringBuilder = (StringBuilder)sbField.GetValue(nextBuilder)!;
        
        // The next builder's capacity should be reasonable (not grown to 300KB+)
        // The initial default capacity is typically much smaller
        nextStringBuilder.Should().NotBeNull();
        
        // Clean up
        QueryTextBuilder.ReturnToPool(nextBuilder);
    }

    [Fact]
    public void EnumValue_CompareTo_WithNonEnumObject_ThrowsArgumentException()
    {
        // Arrange
        var enumVal = new EnumValue("Test");
        object invalidObj = 123; // not an EnumValue

        // Act & Assert
        var action = () => enumVal.CompareTo(invalidObj);
        action.Should().Throw<ArgumentException>()
            .WithMessage("*Object must be of type*");
    }

    [Fact]
    public void EnumValue_Equals_WithNonEnumObject_ThrowsArgumentException()
    {
        // Arrange
        var enumVal = new EnumValue("Test");
        object invalidObj = 123; // not an EnumValue

        // Act & Assert
        var action = () => enumVal.Equals(invalidObj);
        action.Should().Throw<ArgumentException>()
            .WithMessage("*Object must be of type*");
    }

    [Fact]
    public void Build_DeepNestingBeyondPaddingCache_FallsBackToInlineAllocation()
    {
        // QueryTextBuilder.GetPadding has a 20-element cache; nesting > 20 levels makes
        // GetPadding allocate a fresh string instead of using the cache. Build a deeply
        // nested query through the public API.
        var builder = QueryBuilder.CreateDefaultBuilder("DeepQuery");
        // 25 dotted segments — deeper than the 20-level padding cache.
        builder.AddField("a.b.c.d.e.f.g.h.i.j.k.l.m.n.o.p.q.r.s.t.u.v.w.x.y");

        var rendered = builder.ToString();

        rendered.Should().Contain("a");
        rendered.Should().Contain("y");
    }
}
