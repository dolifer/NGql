namespace NGql.Core.Pooling;

/// <summary>Zero-allocation ref struct wrappers with lock-free pooling</summary>
internal ref struct PooledArguments
{
    public readonly SortedDictionary<string, object?> Dictionary;
    public PooledArguments(SortedDictionary<string, object?> dictionary) => Dictionary = dictionary;
    public void Dispose() => LockFreeArgumentsPool.Return(Dictionary);
}