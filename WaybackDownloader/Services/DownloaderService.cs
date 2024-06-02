using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using WaybackDownloader.Services;

namespace WaybackDownloader;

internal sealed class DownloaderService(
    Channel<CdxRecord> channel,
    WaybackCdxClient cdxClient,
    ILogger<DownloaderService> logger)
{
    private readonly ChannelWriter<CdxRecord> _writer = channel.Writer;

    public async Task StartDownloadAsync(string urlPrefix, string matchType, long? from, long? to, CdxFilter[] filters, long? webpageLimit, CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var record in GetInitialFileListAsync(urlPrefix, matchType, from, to, filters, webpageLimit, cancellationToken))
            {
                try
                {
                    _downloadedFirstPage = true;
                    await _writer.WriteAsync(record, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
            }

            if (!cancellationToken.IsCancellationRequested)
            {
                logger.FinishedFetchingSnapshots();
            }
        }
        finally
        {
            _writer.Complete();
        }
    }

    private volatile bool _downloadedFirstPage;
    public bool DownloadedFirstPage
    {
        get => _downloadedFirstPage;
        private set => _downloadedFirstPage = value;
    }

    private async IAsyncEnumerable<CdxRecord> GetInitialFileListAsync(string url, string matchType, long? from, long? to, CdxFilter[] filters, long? webpageLimit, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var record in cdxClient.GetSnapshotListAsync(url, matchType, from, to, filters, webpageLimit, cancellationToken: cancellationToken).WithCancellation(CancellationToken.None))
        {
            if (record is null)
            {
                continue;
            }
            yield return record;
        }
    }
}

internal static partial class LoggerExtensions
{
    [LoggerMessage("Getting max number of pages for snapshots, {Url}", Level = LogLevel.Information)]
    internal static partial void GettingMaxPagesForSnapshots(this ILogger<DownloaderService> logger, ReadOnlySpan<char> url);

    [LoggerMessage("Could not fetch page count. {Url}", Level = LogLevel.Error)]
    internal static partial void FailedToFetchPageCount(this ILogger<DownloaderService> logger, ReadOnlySpan<char> url);

    [LoggerMessage("Failed to parse max page count to valid integer. Original string: {Original}", Level = LogLevel.Critical)]
    internal static partial void FailedToParsePageCountToValidInteger(this ILogger<DownloaderService> logger, ReadOnlySpan<char> original);

    [LoggerMessage("Requesting page {Page} of snapshot list, {Url}", Level = LogLevel.Information)]
    internal static partial void DownloadingSnapshotPage(this ILogger<WaybackCdxClient> logger, int page, ReadOnlySpan<char> url);

    [LoggerMessage("Initial file list download was unsuccessful. Page {Page} StatusCode {StatusCode}.", Level = LogLevel.Information)]
    internal static partial void SnapshotPageDownloadUnsuccessful(this ILogger<WaybackCdxClient> logger, int page, int statusCode);

    [LoggerMessage("Unexpected number of CDX fields. Expected '{Expected}', got '{Actual}'. Original string '{Original}'", Level = LogLevel.Critical)]
    internal static partial void UnexpectedCdxFieldStructure(this ILogger<WaybackCdxClient> logger, int expected, int actual, ReadOnlySpan<char> original);

    [LoggerMessage("Finished fetching snapshots.", Level = LogLevel.Information)]
    internal static partial void FinishedFetchingSnapshots(this ILogger<DownloaderService> logger);
}
