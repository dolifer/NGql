namespace NGql.Core.Pooling;

internal ref struct PooledCharArray(char[] array, int length)
{
    public readonly char[] Array = array;
    public readonly int Length = length;

    public Span<char> AsSpan() => Array.AsSpan(0, Length);
    
    public void Dispose() => CharArrayPool.Return(Array);
}
