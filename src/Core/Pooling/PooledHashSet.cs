namespace NGql.Core.Pooling;

/// <summary>
/// Zero-allocation ref struct wrapper with lock-free pooling
/// </summary>
internal ref struct PooledHashSet
{
    public readonly HashSet<string> Set;
    public PooledHashSet(HashSet<string> set) => Set = set;
    public void Dispose() => LockFreeHashSetPool.Return(Set);
}