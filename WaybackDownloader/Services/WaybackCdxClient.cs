using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.Extensions.Logging;

namespace WaybackDownloader.Services;
internal sealed class WaybackCdxClient(HttpClient client, ILogger<WaybackCdxClient> logger)
{
    private readonly ILogger<WaybackCdxClient> _logger = logger;
    private readonly HttpClient _client = client;

    public async IAsyncEnumerable<CdxRecord?> GetSnapshotListAsync(string matchUrl, string? matchType = null, long? from = null, long? to = null, CdxFilter[]? filters = null, long? webpageLimit = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {

        var queryBuilder = new StringBuilder($"url={matchUrl}");
        if (matchType is not null)
        {
            if (!MatchTypes.IsValid(matchType))
            {
                throw new ArgumentException($"Match type '{matchType}' is not valid. Expected one of [{MatchTypes.Exact}, {MatchTypes.Prefix}, {MatchTypes.Host}, {MatchTypes.Domain}].");
            }
            queryBuilder.Append(CultureInfo.InvariantCulture, $"&matchType={matchType}");
        }

        if (from is not null)
        {
            queryBuilder.Append(CultureInfo.InvariantCulture, $"&from={from.Value}");
        }

        if (to is not null)
        {
            queryBuilder.Append(CultureInfo.InvariantCulture, $"&to={to.Value}");
        }

        foreach (var filter in filters ?? [])
        {
            queryBuilder.Append(CultureInfo.InvariantCulture, $"&filter={filter}");
        }

        var query = queryBuilder.ToString();
        var page = 0;
        var websiteCount = 0L;
        var webpageLimitReached = false;
        while (true)
        {
            var url = $"https://web.archive.org/cdx/search/cdx?{query}&collapse=digest&page={page}";

            _logger.DownloadingSnapshotPage(page, url);

            var response = await _client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                _logger.SnapshotPageDownloadUnsuccessful(page, (int)response.StatusCode);
                yield break;
            }

            var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using var reader = new StreamReader(contentStream);

            //we want to iterate each page in reverse order, to optimize for that fact that the most recent snapshots
            //are at the end of the list.
            //Although this means we need to buffer each snapshot page in memory, the slow part of this process
            //is downloading each file given in the snapshot list, rather than the download of the list itself.
            var lines = new List<string>(1000);
            string? line = null;

            var didReadContent = false;
            while ((line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false)) is not null)
            {
                didReadContent = true;
                lines.Add(line);
            }

            for (var i = lines.Count; i > 0; i--)
            {
                line = lines[i - 1];

                var record = GetRecord(line);
                websiteCount++;
                yield return record;

                if (webpageLimit is not null && websiteCount >= webpageLimit)
                {
                    webpageLimitReached = true;
                    break;
                }
            }

            if (!didReadContent || webpageLimitReached)
            {
                yield break;
            }
            page++;
        }

        unsafe CdxRecord? GetRecord(ReadOnlySpan<char> line)
        {
            Span<Range> ranges = stackalloc Range[7];
            var written = line.Split(ranges, ' ');
            if (written != 7)
            {
                _logger.UnexpectedCdxFieldStructure(7, written, line);
                return null;
            }

            var urlKey = line[ranges[0]].ToString();
            var parsedTimestamp = long.TryParse(line[ranges[1]], out var timestamp);
            var originalUrl = line[ranges[2]].ToString();
            var mimeType = line[ranges[3]].ToString();
            var parsedStatusCode = int.TryParse(line[ranges[4]], out var statusCode);
            var digest = line[ranges[5]].ToString();
            var parsedLength = int.TryParse(line[ranges[6]], out var length);

            if (!parsedTimestamp || !parsedStatusCode || !parsedLength)
            {
                _logger.FailedToParseCdxFields(line);
                return null;
            }
            return new CdxRecord(urlKey, timestamp, originalUrl, mimeType, statusCode, digest, length);
        }
    }
}

internal sealed record CdxRecord(string UrlKey, long Timestamp, string Original, string MimeType, int StatusCode, string Digest, int Length);

internal static partial class LoggerExtensions
{
    [LoggerMessage("Requesting page {Page} of snapshot list, {Url}", Level = LogLevel.Information)]
    internal static partial void DownloadingSnapshotPage(this ILogger<WaybackCdxClient> logger, int page, ReadOnlySpan<char> url);

    [LoggerMessage("Initial file list download was unsuccessful. Page {Page} StatusCode {StatusCode}.", Level = LogLevel.Information)]
    internal static partial void SnapshotPageDownloadUnsuccessful(this ILogger<WaybackCdxClient> logger, int page, int statusCode);

    [LoggerMessage("Unexpected number of CDX fields. Expected '{Expected}', got '{Actual}'. Original string '{Original}'.", Level = LogLevel.Critical)]
    internal static partial void UnexpectedCdxFieldStructure(this ILogger<WaybackCdxClient> logger, int expected, int actual, ReadOnlySpan<char> original);

    [LoggerMessage("Failed to parse some CDX fields, record will be skipped. Original string '{Original}'.", Level = LogLevel.Warning)]
    internal static partial void FailedToParseCdxFields(this ILogger<WaybackCdxClient> logger, ReadOnlySpan<char> original);
}
