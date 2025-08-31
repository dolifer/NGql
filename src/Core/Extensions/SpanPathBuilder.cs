namespace NGql.Core.Extensions;

/// <summary>
/// Ref struct for building paths using spans without allocations
/// </summary>
internal ref struct SpanPathBuilder(Span<char> buffer)
{
    private Span<char> _buffer = buffer;
    private int _length = 0;

    public void Append(ReadOnlySpan<char> segment)
    {
        if (_length > 0)
        {
            _buffer[_length++] = '.';
        }
        
        segment.CopyTo(_buffer[_length..]);
        _length += segment.Length;
    }

    public readonly ReadOnlySpan<char> AsSpan() => _buffer[.._length];
    
    public readonly override string ToString() => _buffer[.._length].ToString();
}
