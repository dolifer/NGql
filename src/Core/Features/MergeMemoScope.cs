namespace NGql.Core.Features;

internal sealed class MergeMemoScope
{
    private long _version;

    internal long Version => Interlocked.Read(ref _version);

    internal void Invalidate() => Interlocked.Increment(ref _version);
}

internal sealed class MergeMemoTracker
{
    private MergeMemoScope? _primary;
    private object? _gate;
    private volatile List<WeakReference<MergeMemoScope>>? _others;

    internal void Attach(MergeMemoScope scope)
    {
        if (ReferenceEquals(Volatile.Read(ref _primary), scope)) return;
        var primary = Interlocked.CompareExchange(ref _primary, scope, null);
        if (primary is null || ReferenceEquals(primary, scope)) return;
        lock (LazyInitializer.EnsureInitialized(ref _gate))
        {
            _others ??= new();
            for (var i = _others.Count - 1; i >= 0; i--)
            {
                if (!_others[i].TryGetTarget(out var target)) _others.RemoveAt(i);
                else if (ReferenceEquals(target, scope)) return;
            }
            _others.Add(new WeakReference<MergeMemoScope>(scope));
        }
    }

    internal void Invalidate()
    {
        Volatile.Read(ref _primary)?.Invalidate();
        if (_others is null) return;
        lock (_gate!)
        {
            for (var i = _others.Count - 1; i >= 0; i--)
            {
                if (_others[i].TryGetTarget(out var scope)) scope.Invalidate();
                else _others.RemoveAt(i);
            }
        }
    }
}
