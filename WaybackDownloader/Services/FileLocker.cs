using System.Collections.Concurrent;

internal static class FileLocker
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> Locks = new();

    public static async ValueTask<FileLock> WaitForAccessAsync(string filePath, CancellationToken cancellationToken)
    {
        var semaphore = Locks.GetOrAdd(filePath, static _ => new SemaphoreSlim(1, 1));
        await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        return new FileLock(semaphore);
    }

    public readonly struct FileLock(SemaphoreSlim semaphore) : IDisposable
    {
        private readonly SemaphoreSlim _lockSlim = semaphore;
        public void Dispose() => _lockSlim.Release();
    }
}
