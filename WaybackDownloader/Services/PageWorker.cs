using System.Diagnostics.CodeAnalysis;
using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Polly;

namespace WaybackDownloader.Services;

internal sealed class PageWorker(
        HttpClient client,
        Channel<CdxRecord> channel,
        PagesStore pagesStore,
        [FromKeyedServices("DirectoryCreatePipeline")] ResiliencePipeline directoryPipeline,
        PageFilters filters,
        ILogger<PageWorker> logger
    )
{
    public const string PageWorkerHttpClientRateLimiterKey = "PageWorkerHttpClientRateLimiter";
    private static readonly Dictionary<string, Dictionary<string, int>> PathMap = [];
    private static readonly SemaphoreSlim PathMapSemaphore = new(1, 1);

    private readonly struct PageKey : IEquatable<PageKey>
    {
        private PageKey(string key)
        {
            Value = key;
        }
        public PageKey(string key, string normalizedPath) : this($"{key}-{normalizedPath}") { }

        public string Value { get; }

        public bool Equals(PageKey other) => string.Equals(Value, other.Value, StringComparison.OrdinalIgnoreCase);
        public override bool Equals([NotNullWhen(true)] object? obj) => obj is PageKey other && Equals(other);
        public override int GetHashCode() => Value.GetHashCode(StringComparison.OrdinalIgnoreCase);


        public static explicit operator string(PageKey key) => key.Value;
        public static PageKey UnsafeFromString(string key) => new(key);
    }

    private readonly ChannelReader<CdxRecord> _reader = channel.Reader;
    public async Task StartAsync(string outputDir, CancellationToken cancellationToken)
    {
        var shouldExit = false;
        cancellationToken.Register(() => shouldExit = true);

        while (!shouldExit && await _reader.WaitToReadAsync(default).ConfigureAwait(false))
        {
            while (!shouldExit && !cancellationToken.IsCancellationRequested && _reader.TryRead(out var record))
            {
                if (!PathUtilities.TryGetNormalizedFilePath(record.Original, out var normalizedPath))
                {
                    logger.UrlCouldNotBeConverted(record.Original);
                    Counters.FilesSkipped.Increment();
                    continue;
                }

                logger.UrlTransformed(record.Original, normalizedPath);

                var writePath = Path.Combine(outputDir, normalizedPath);
                var pageKey = new PageKey(record.UrlKey, normalizedPath);
                var foundPage = pagesStore.TryGetDownloadedPageTimestamp(pageKey.Value, out var timestamp);
                if (!foundPage || timestamp < record.Timestamp)
                {
                    await TryWritePageAsync(record, writePath, normalizedPath, pageKey, foundPage, CancellationToken.None).ConfigureAwait(false);
                }
                else
                {
                    logger.UrlAlreadyDownloaded(record.Original);
                    Counters.FilesSkipped.Increment();
                }
            }
        }

        logger.ExitingWorker();
    }

    private async Task TryWritePageAsync(CdxRecord record, string writePath, string normalizedPath, PageKey pageKey, bool isUpdateToExistingPage, CancellationToken cancellationToken)
    {
        //max path length
        if (writePath.Length > 260 - Path.GetExtension(writePath).Length - 7)
        {
            var oldPath = writePath;
            writePath = await GetShortenedLongPathAsync(writePath, cancellationToken).ConfigureAwait(false);
            logger.UrlShortened(oldPath, writePath);
        }

        logger.DownloadingPageContents(record.Original);

        var requestUri = new Uri($"https://web.archive.org/web/{record.Timestamp}id_/{record.Original}");

        using var locker = await FileLocker.WaitForAccessAsync(writePath, cancellationToken).ConfigureAwait(false);
        var fetchTask = client.GetAsync(requestUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);

        CreateDirectory(writePath, directoryPipeline);

        using var fs = new FileStream(writePath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None);
        //now we have a file lock, check if our timestamp is greater than any recently newly added timestamps.
        if (pagesStore.TryGetDownloadedPageTimestamp(pageKey.Value, out var updatedTimestamp) && updatedTimestamp >= record.Timestamp)
        {
            return;
        }

        var response = await fetchTask;
        if (!response.IsSuccessStatusCode)
        {
            logger.PageDownloadUnsuccessful(record.Original, (int)response.StatusCode);
            return;
        }

        if (filters.Any())
        {
            var pageContents = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            if (filters.IsMatch(pageContents))
            {
                using var sw = new StreamWriter(fs);
                await sw.WriteAsync(pageContents.AsMemory(), cancellationToken).ConfigureAwait(false);
                logger.WrittenFilteredPage(writePath, record.Original, record.Timestamp);
                IncrementCounter(isUpdateToExistingPage);
            }
            else
            {
                await fs.DisposeAsync().ConfigureAwait(false);
                File.Delete(writePath);
                Counters.FilesSkipped.Increment();
            }
        }
        else
        {
            var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            await stream.CopyToAsync(fs, cancellationToken).ConfigureAwait(false);
            logger.WrittenPage(writePath, record.Original, record.Timestamp);
            IncrementCounter(isUpdateToExistingPage);
        }
        pagesStore.AddPage(pageKey.Value, record.Timestamp);

        static void IncrementCounter(bool isUpdateToExistingPage)
        {
            if (isUpdateToExistingPage)
            {
                Counters.FilesUpdated.Increment();
            }
            else
            {
                Counters.FilesWritten.Increment();
            }
        }
    }

    private static async ValueTask<string> GetShortenedLongPathAsync(string path, CancellationToken cancellationToken)
    {
        try
        {
            var ext = Path.GetExtension(path);
            var clippedPath = path[0..(260 - ext.Length - 7)];
            await PathMapSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            if (PathMap.TryGetValue(clippedPath, out var paths))
            {
                if (paths.TryGetValue(path, out var number))
                {
                    return GetNumberedPath(clippedPath, ext, number);
                }
                else
                {
                    paths.Add(path, paths.Count + 1);
                    return GetNumberedPath(clippedPath, ext, paths.Count);
                }
            }
            else
            {
                PathMap.Add(clippedPath, new() { [path] = 1 });
                return GetNumberedPath(clippedPath, ext, 1);
            }
        }
        finally
        {
            PathMapSemaphore.Release();
        }
    }

    private static string GetNumberedPath(ReadOnlySpan<char> clippedPath, ReadOnlySpan<char> extension, int number) => $"{clippedPath}...({number}).{extension}";

    private static void CreateDirectory(string writePath, ResiliencePipeline directoryPipeline)
    {
        var dirName = Path.GetDirectoryName(writePath);
        if (!string.IsNullOrWhiteSpace(dirName))
        {
            directoryPipeline.Execute(static path =>
            {
                Directory.CreateDirectory(path);
            }, dirName);
        }
    }
}

internal static partial class LoggerExtensions
{
    [LoggerMessage("Exiting worker.", Level = LogLevel.Information)]
    internal static partial void ExitingWorker(this ILogger<PageWorker> logger);

    [LoggerMessage("URL '{Url}' could not be converted to a valid file path and will be skipped.", Level = LogLevel.Warning)]
    internal static partial void UrlCouldNotBeConverted(this ILogger<PageWorker> logger, ReadOnlySpan<char> url);

    [LoggerMessage("A more up-to-date version of '{Url}' has already been downloaded. Page will be skipped.", Level = LogLevel.Debug)]
    internal static partial void UrlAlreadyDownloaded(this ILogger<PageWorker> logger, ReadOnlySpan<char> url);

    [LoggerMessage("URL '{Url}' transformed to {Transformed}.", Level = LogLevel.Debug)]
    internal static partial void UrlTransformed(this ILogger<PageWorker> logger, ReadOnlySpan<char> url, ReadOnlySpan<char> transformed);

    [LoggerMessage("Transformed path {Transformed} was too long, and was shorted to {Shortened}.", Level = LogLevel.Debug)]
    internal static partial void UrlShortened(this ILogger<PageWorker> logger, ReadOnlySpan<char> transformed, ReadOnlySpan<char> shortened);

    [LoggerMessage("Starting download of page at {Url}.", Level = LogLevel.Debug)]
    internal static partial void DownloadingPageContents(this ILogger<PageWorker> logger, ReadOnlySpan<char> url);

    [LoggerMessage("Request for page '{Url}' was unsuccessful. StatusCode: {StatusCode}", Level = LogLevel.Warning)]
    internal static partial void PageDownloadUnsuccessful(this ILogger<PageWorker> logger, ReadOnlySpan<char> url, int statusCode);

    [LoggerMessage("Written filtered page {Page}, {Original}, timestamp: {Timestamp}", Level = LogLevel.Information)]
    internal static partial void WrittenFilteredPage(this ILogger<PageWorker> logger, ReadOnlySpan<char> page, ReadOnlySpan<char> original, long timestamp);

    [LoggerMessage("Written page {Page}, {Original}, timestamp: {Timestamp}", Level = LogLevel.Information)]
    internal static partial void WrittenPage(this ILogger<PageWorker> logger, ReadOnlySpan<char> page, ReadOnlySpan<char> original, long timestamp);

    [LoggerMessage("Failed to acquire rate limit lease. Skipping record.", Level = LogLevel.Warning)]
    internal static partial void FailedToAcquireRateLimitLease(this ILogger<PageWorker> logger);
}
