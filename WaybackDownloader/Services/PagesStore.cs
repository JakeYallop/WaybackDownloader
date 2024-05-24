using FASTER.core;

namespace WaybackDownloader.Services;

internal sealed class PagesStore : IDisposable, IAsyncDisposable
{
    private readonly FasterKVSettings<string, long> _settings;
    private readonly FasterKV<string, long> _store;
    private readonly Timer _checkpointTimer;
    private const string StorePath = "./kv";

    public PagesStore()
    {
        _settings = new FasterKVSettings<string, long>(StorePath, logger: null)
        {
            RemoveOutdatedCheckpoints = true,
            TryRecoverLatest = true
        };
        _store = new FasterKV<string, long>(_settings);
        _checkpointTimer = new(StartPeriodicCheckpointsAsync, _store, 30_000, 30_000);
    }

    private static readonly SimpleFunctions<string, long> SimpleFunctions = new();
    public void AddPage(string key, long value)
    {
        using var session = _store.For(SimpleFunctions).NewSession<SimpleFunctions<string, long>>();
        session.Upsert(ref key, ref value);
    }

    public bool TryGetDownloadedPageTimestamp(string key, out long timestamp)
    {
        timestamp = long.MinValue;
        using var session = _store.For(SimpleFunctions).NewSession<SimpleFunctions<string, long>>();
        var (status, output) = session.Read(key);
        if (!status.Found)
        {
            return false;
        }

        timestamp = output;
        return true;
    }

    public void PurgeCheckpoints()
    {
        _store.CheckpointManager.PurgeAll();
        _store.Reset();
    }

    public static async void StartPeriodicCheckpointsAsync(object? state)
    {
        var store = (FasterKV<string, long>)state!;
        (_, _) = await store.TakeHybridLogCheckpointAsync(CheckpointType.Snapshot).ConfigureAwait(false);
    }

    public void Dispose()
    {
        _checkpointTimer.Dispose();
        _store.TakeFullCheckpointAsync(CheckpointType.Snapshot).AsTask().GetAwaiter().GetResult();
        _store.Dispose();
        _settings.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        await _checkpointTimer.DisposeAsync().ConfigureAwait(false);
        await _store.TakeFullCheckpointAsync(CheckpointType.Snapshot).ConfigureAwait(false);
        _settings.Dispose();
        _store.Dispose();
    }
}
