using System.Runtime.CompilerServices;

namespace NGql.Core.Builders;

/// <summary>
/// Ref struct for building paths using spans without allocations
/// </summary>
internal ref struct SpanPathBuilder(Span<char> buffer)
{
    private Span<char> _buffer = buffer;
    private int _length = 0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Append(ReadOnlySpan<char> segment)
    {
        if (_length > 0 && _length < _buffer.Length)
        {
            _buffer[_length++] = '.';
        }
        
        if (segment.Length > 0 && _length + segment.Length <= _buffer.Length)
        {
            segment.CopyTo(_buffer[_length..]);
            _length += segment.Length;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly ReadOnlySpan<char> AsSpan() => _buffer[.._length];
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly override string ToString() => _buffer[.._length].ToString();
}
