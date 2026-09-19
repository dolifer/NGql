using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Text;
using FluentAssertions;
using NGql.Core.Builders;
using NGql.Core.Caching;
using Xunit;
using Xunit.Abstractions;

namespace NGql.Core.Tests.Caching;

[CollectionDefinition("Cache retention", DisableParallelization = true)]
public sealed class CacheRetentionCollection;

[Collection("Cache retention")]
public class CacheRetentionTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData("object")]
    [InlineData("navigation")]
    [InlineData("pair")]
    public void Metadata_DoesNotRootCollectibleTypes(string cache)
    {
        var reference = CacheCollectibleType(cache);
        Collect();
        output.WriteLine($"{cache}: collectible type retained = {reference.IsAlive}");
        reference.IsAlive.Should().BeFalse();
    }

    [Fact]
    public void CustomTypeChurn_HasBoundedRetention()
    {
        var references = CacheCustomTypes();
        Collect();
        var retained = references.Count(reference => reference.IsAlive);
        output.WriteLine($"Custom type names retained after 20,000 unique lookups: {retained}");
        retained.Should().BeLessThanOrEqualTo(4096);
    }

    [Fact]
    public void CustomTypeChurn_KeepsNameThatStaysInUse()
    {
        var hot = "Hot" + Guid.NewGuid().ToString("N");
        var first = TypeCache.GetInternedType(hot.AsSpan());
        var prefix = "Churn" + Guid.NewGuid().ToString("N");

        for (var i = 0; i < 20000; i++)
        {
            TypeCache.GetInternedType((prefix + i).AsSpan());
            if (i % 500 == 0) TypeCache.GetInternedType(hot.AsSpan()).Should().BeSameAs(first);
        }

        TypeCache.GetInternedType(hot.AsSpan()).Should().BeSameAs(first);
    }

    [Fact]
    public void CustomTypeChurn_ConcurrentMissesReturnEqualNames()
    {
        var prefix = "Parallel" + Guid.NewGuid().ToString("N");

        System.Threading.Tasks.Parallel.For(0, 8, _ =>
        {
            for (var i = 0; i < 6000; i++)
            {
                var name = prefix + i;
                TypeCache.GetInternedType(name.AsSpan()).Should().Be(name);
            }
        });
    }

    [Fact]
    public void OversizedCustomType_IsNotRetained()
    {
        var reference = CacheLongType();
        Collect();
        reference.IsAlive.Should().BeFalse();
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference CacheCollectibleType(string cache)
    {
        var assembly = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("Probe" + Guid.NewGuid().ToString("N")), AssemblyBuilderAccess.RunAndCollect);
        var type = assembly.DefineDynamicModule("Main").DefineType("Probe", TypeAttributes.Public).CreateType()!;
        switch (cache)
        {
            case "object":
                _ = TypeMetadataCache.GetObjectProperties(type);
                break;
            case "navigation":
                _ = TypeMetadataCache.GetNavigationProperties(type);
                break;
            default:
                var pair = typeof(KeyValuePair<,>).MakeGenericType(typeof(string), type);
                QueryTextBuilder.WriteObject(new StringBuilder(), Activator.CreateInstance(pair));
                break;
        }
        return new WeakReference(type);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference[] CacheCustomTypes()
    {
        var prefix = "Dynamic" + Guid.NewGuid().ToString("N");
        var references = new WeakReference[20000];
        for (var i = 0; i < references.Length; i++)
            references[i] = new WeakReference(TypeCache.GetInternedType((prefix + i).AsSpan()));
        return references;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference CacheLongType()
        => new(TypeCache.GetInternedType(new string('X', 10000).AsSpan()));

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Major Code Smell", "S1215",
        Justification = "Retention regression tests must distinguish cache roots from uncollected garbage.")]
    private static void Collect()
    {
        for (var i = 0; i < 5; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }
    }
}
