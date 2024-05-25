using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using Spectre.Console;
using Spectre.Console.Cli;
using WaybackDownloader.Services;
using WaybackDownloader.Spectre;

namespace WaybackDownloader;

internal sealed partial class DefaultCommand : CancellableAsyncCommand<DefaultCommand.Settings>
{
    public DefaultCommand()
    {
    }

    public sealed class Settings : CommandSettings
    {
        /// <summary>
        /// The URL to match against.
        /// </summary>
        [CommandArgument(0, "<matchUrl>")]
        [Description("The URL to match against.")]
        public string MatchUrl { get; init; } = null!;

        [CommandArgument(1, "<outputDir>")]
        [Description("Location to store downloaded webpages.")]
        public DirectoryInfo OutputDir { get; init; } = null!;

        [CommandOption("--downloadsLogDir|-d")]
        [Description("Log location that stores information about which webpages have already been downloaded.")]
        public DirectoryInfo DownloadsLogDir { get; init; } = new("./checkpoints");

        [CommandOption("-m|--matchType")]
        [DefaultValue(MatchTypes.Exact)]
        [Description($"One of '{MatchTypes.Exact}', '{MatchTypes.Prefix}', '{MatchTypes.Domain}', '{MatchTypes.Host}'.")]
        public string MatchType { get; init; } = null!;

        [CommandOption("--from")]
        [Description("Timestamp in wayback machine format yyyyMMddHHmmss to specify the start of a time range. If specified, at least a 4 digit year must be specified.")]
        public long? From { get; init; }

        [CommandOption("--to")]
        [Description("Timestamp in the wayback machine format yyyyMMddHHmmss to specify the end of a time range. If specified, at least a 4 digit year must be specified.")]
        public long? To { get; init; }

        [CommandOption("-f|--filters")]
        [DefaultValue(new[] { "statuscode:200", "mimetype:text/html" })]
        [Description("[[!]]<FieldName>:<Expression>. Filters fields returned from the CDX server. See here for details: https://archive.org/developers/wayback-cdx-server.html#filtering.")]
        public string[] Filters { get; init; } = ["statuscode:200", "mimetype:text/html"];

        /// <summary>
        /// Once a page has been downloaded, only save it to disk if it contains one of the words in this list.
        /// This comparison is ordinal and ignores case, for example `A` and `a` would compare as equal,
        /// whereas `a` and `à` would not. This option can help save a ton of disk space if you already vaguely know what
        /// you are looking for.
        /// </summary>
        [CommandOption("-p|--pageFilters <VALUES>")]
        [Description("Once a page has been downloaded, only save it to disk if it contains one of the words in this list. " +
            "This comparison is ordinal and ignores case, for example `A` and `a` would compare as equal, " +
            "whereas `a` and `à` would not. This option can help save a ton of disk space if you already vaguely know what " +
            "you are looking for.")]
        public string[] PageFilters { get; init; } = [];

        internal CdxFilter[] ParsedFilters { get; private set; } = [];

        [CommandOption("--limitPages")]
        [Description("Limit the number of pages processed. This is an absolute limit on the number of pages processed, two versions of the same page will count twice.")]
        public long? LimitPages { get; init; }

        /// <summary>
        /// Set page downloads rate limit.
        /// </summary>
        [CommandOption("-r|--rateLimit")]
        [Description("Rate limit for the number of pages to download per second. It is not recommended to set " +
            "this to a very high value, as this can result in throttling or temporary blacklisting by the" +
            "wayback machine and archive.org.")]
        [DefaultValue(5)]
        public int RateLimit { get; init; } = 5;

        [CommandOption("--clearHistory")]
        [Description("Clear any information about previously downloaded pages. This will force the tool to start from a completely empty state.")]
        public bool ClearHistory { get; init; }

        /// <summary>
        /// Enable verbose logging.
        /// </summary>
        [CommandOption("-v|--verbose")]
        [Description("Enabled verbose logging.")]
        [DefaultValue(false)]
        public bool Verbose { get; init; }

        [CommandOption("--useMockHandler")]
        public bool UseMockHandler { get; set; }

        public override ValidationResult Validate()
        {
            const string LimitPagesError = "--limitPages must be greater than 0.";
            const string RateLimitError = "--rateLimit must be greater than 0.";
            var filtersValid = ValidateFilters(Filters, out var filtersMessage);
            var fromValid = TimestampValidator.ValidateTimestamp(From, nameof(From), out var fromError);
            var toValid = TimestampValidator.ValidateTimestamp(To, nameof(To), out var toError);
            var limitPagesValud = LimitPages is null or > 0;
            var rateLimitValid = RateLimit > 0;

            return !filtersValid || !fromValid || !toValid || !limitPagesValud || !rateLimitValid
                ? ValidationResult.Error(string.Join(Environment.NewLine, [filtersMessage, fromError, toError, LimitPagesError, RateLimitError]))
                : ValidationResult.Success();
        }

        private static bool ValidateFilters(string[] filters, [NotNullWhen(false)] out string? error)
        {
            error = null;
            var parsedFilters = new List<CdxFilter>(filters.TryGetNonEnumeratedCount(out var count) ? count : 4);
            foreach (var filter in filters)
            {
                if (!CdxFilter.TryParseExpression(filter, out var filterResult))
                {
                    error = filterResult.ErrorMessage!;
                    return false;
                }
                parsedFilters.Add(filterResult.Filter!);
            }
            return true;
        }
    }

    private static void PrintSettings(Settings settings)
    {
        AnsiConsole.WriteLine($"Match URL: {settings.MatchUrl}");
        AnsiConsole.WriteLine($"Match type: {settings.MatchType}");

        if (settings.From is not null)
        {
            AnsiConsole.WriteLine($"From: {settings.From}");
        }

        if (settings.To is not null)
        {
            AnsiConsole.WriteLine($"To: {settings.To}");
        }

        if (settings.Filters.Length > 0)
        {
            AnsiConsole.WriteLine("Filters:");
            foreach (var filter in settings.Filters)
            {
                AnsiConsole.WriteLine($"  {filter}");
            }
        }

        if (settings.PageFilters.Length > 0)
        {
            AnsiConsole.WriteLine("Page filters:");
            foreach (var filter in settings.PageFilters)
            {
                AnsiConsole.WriteLine($"  {filter}");
            }
        }

        if (settings.LimitPages is not null)
        {
            AnsiConsole.WriteLine($"Limit pages: {settings.LimitPages}");
        }

        AnsiConsole.WriteLine($"Rate limit (pages/second): {settings.RateLimit}");
        AnsiConsole.WriteLine($"Output directory: {settings.OutputDir}");
        AnsiConsole.WriteLine($"Checkpoints directory: {settings.DownloadsLogDir}");

        if (settings.ClearHistory)
        {
            AnsiConsole.Write("Clear history: true ");
            AnsiConsole.MarkupLine("[yellow]Clearing all history on pages that previously been downloaded.[/]");
        }

        if (settings.Verbose)
        {
            AnsiConsole.Write($"Verbose: {settings.Verbose} ");
            AnsiConsole.WriteLine("Verbose logging enabled.");
        }

        if (settings.UseMockHandler)
        {
            AnsiConsole.Write($"Use mock handler: {settings.UseMockHandler} ");
            AnsiConsole.MarkupLine("[yellow]No real API requests will be made. Garbage data will be saved to disk.[/]");
        }
    }
}
