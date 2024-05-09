namespace WaybackDownloader;
//TODO: Consider using actual meters/instruments, which would then allow us to extract more data, histograms, averages etc.
public sealed class Counter
{
    //Using interlocked in heavily threaded scenarios can result in performance issues due to contention. We don't
    //expect to have those kinds of workloads where this will be a problem.

    private long _count;
    private Action<long>? _counterChanged;

    public long Count => Interlocked.Read(ref _count);

    public void Increment(int delta = 1)
    {
        var value = Interlocked.Add(ref _count, delta);
        _counterChanged?.Invoke(value);
    }

    public void Decrement(int delta = 1)
    {
        var value = Interlocked.Add(ref _count, -delta);
        _counterChanged?.Invoke(value);
    }

    public void Register(Action<long> callback) => _counterChanged += callback;
}
