using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using FluentAssertions;
using Xunit;
using static NGql.Core.QueryTextBuilder;

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
        var valueString = BuildQueryParam(new [] { 123, 456, 789});

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
    public void AddFields_ThrowsUnsupportedException()
    {
        // act & assert
        var exception = Assert.Throws<InvalidOperationException>(() => new Query("foo")
            .Select(new object[] { 1, "text", Guid.NewGuid() })
            .ToString()
        );

        exception.Message.Should().Be("Unsupported Field type found, must be a `string` or `QueryBlock`");
    }

    private static string BuildQueryParam(object value)
    {
        var builder = new StringBuilder();
        WriteObject(builder, value);
        return builder.ToString();
    }
}
