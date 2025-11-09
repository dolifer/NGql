using System.Buffers;

namespace NGql.Core.Pooling;

/// <summary>
/// Pool for char arrays to reduce allocations in span operations
/// </summary>
internal static class CharArrayPool
{
    private static readonly ArrayPool<char> Pool = ArrayPool<char>.Shared;

    private static char[] Rent(int minimumLength) => Pool.Rent(minimumLength);
    
    internal static void Return(char[] array, bool clearArray = false) => Pool.Return(array, clearArray);

    internal static PooledCharArray GetPooled(int minimumLength) => new(Rent(minimumLength), minimumLength);
}
