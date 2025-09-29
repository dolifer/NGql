using System.Runtime.CompilerServices;
using NGql.Core.Pooling;

namespace NGql.Core.Extensions;

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

/// <summary>
/// Optimized path builder that uses pooled resources for larger paths
/// </summary>
internal ref struct OptimizedPathBuilder
{
    private readonly PooledStringBuilder _pooledBuilder;
    private readonly bool _usePooled;

    public OptimizedPathBuilder(int estimatedLength)
    {
        if (estimatedLength > 256)
        {
            _pooledBuilder = LockFreeStringBuilderPool.GetPooled();
            _usePooled = true;
        }
        else
        {
            _pooledBuilder = default;
            _usePooled = false;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Append(ReadOnlySpan<char> value)
    {
        if (_usePooled)
        {
            if (_pooledBuilder.StringBuilder.Length > 0)
            {
                _pooledBuilder.StringBuilder.Append('.');
            }
            _pooledBuilder.StringBuilder.Append(value);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public new readonly string ToString()
    {
        return _usePooled ? _pooledBuilder.StringBuilder.ToString() : string.Empty;
    }

    public void Dispose()
    {
        if (_usePooled)
        {
            _pooledBuilder.Dispose();
        }
    }
}
