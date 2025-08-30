namespace NGql.Core.Pooling;

internal ref struct PooledCharArray
{
    public readonly char[] Array;
    public readonly int Length;
    
    public PooledCharArray(char[] array, int length)
    {
        Array = array;
        Length = length;
    }
    
    public Span<char> AsSpan() => Array.AsSpan(0, Length);
    public Span<char> AsSpan(int length) => Array.AsSpan(0, Math.Min(length, Length));
    
    public void Dispose() => CharArrayPool.Return(Array);
}