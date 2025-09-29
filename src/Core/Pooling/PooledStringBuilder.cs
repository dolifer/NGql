using System.Text;

namespace NGql.Core.Pooling;

/// <summary>
/// Zero-allocation ref struct wrapper with lock-free pooling
/// </summary>
internal ref struct PooledStringBuilder
{
    public readonly StringBuilder StringBuilder;
    public PooledStringBuilder(StringBuilder sb) => StringBuilder = sb;
    public void Dispose() => LockFreeStringBuilderPool.Return(StringBuilder);
}