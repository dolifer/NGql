using System.Runtime.CompilerServices;
using System.Text;

namespace NGql.Core.Pooling;

/// <summary>
/// Lock-free pool for StringBuilder instances with thread-local optimization
/// </summary>
internal static class LockFreeStringBuilderPool
{
    private const int MaxCapacity = 2048; // Larger capacity limit for better reuse
    private const int InitialCapacity = 256;

    private static readonly ThreadLocalPool<StringBuilder> _pool = new(
        factory: () => new StringBuilder(InitialCapacity),
        reset: sb => sb.Clear(),
        validateForReturn: sb => sb.Capacity <= MaxCapacity, // Skip if capacity is too large (prevents memory bloat)
        poolName: "stringbuilder"
    );

    /// <summary>
    /// Gets a pooled StringBuilder instance
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static PooledStringBuilder Get() => new(_pool.Get());

    /// <summary>
    /// Gets a pooled StringBuilder instance (alias for compatibility)
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static PooledStringBuilder GetPooled() => Get();

    /// <summary>
    /// Returns StringBuilder to the pool
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Return(StringBuilder sb)
    {
        _pool.Return(sb);
    }

    /// <summary>
    /// Zero-allocation ref struct wrapper with lock-free pooling
    /// </summary>
    internal ref struct PooledStringBuilder
    {
        public readonly StringBuilder StringBuilder;
        internal PooledStringBuilder(StringBuilder sb) => StringBuilder = sb;
        public void Dispose() => Return(StringBuilder);
    }
}
