using System;
using System.Threading.Tasks;
using FluentAssertions;
using NGql.Core.Caching;
using Xunit;

namespace NGql.Core.Tests.Caching;

public class CustomTypeLookupTests
{
    [Fact]
    public void CustomTypes_SlicedInputReusesCanonicalInstanceAndPreservesCase()
    {
        var expected = TypeCache.GetInternedType("SchemaObject".AsSpan());
        var actual = TypeCache.GetInternedType("__SchemaObject__".AsSpan(2, 12));

        actual.Should().BeSameAs(expected);
        TypeCache.GetInternedType("schemaObject".AsSpan()).Should().Be("schemaObject");
    }

    [Fact]
    public void CustomTypes_ConcurrentMissesReturnSameInstance()
    {
        var type = "ConcurrentType" + Guid.NewGuid().ToString("N");
        var results = new string[100];
        Parallel.For(0, results.Length, i => results[i] = TypeCache.GetInternedType(type.AsSpan()));

        foreach (var result in results) result.Should().BeSameAs(results[0]);
    }
}
