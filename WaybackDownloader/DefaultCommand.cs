using System.Threading.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;
using Spectre.Console.Cli;
using WaybackDownloader.Logging;
using WaybackDownloader.Services;
using WaybackDownloader.Spectre;
using Settings = WaybackDownloader.DefaultCommand.Settings;

namespace WaybackDownloader;

internal sealed partial class DefaultCommand : CancellableAsyncCommand<Settings>
{
    public const string Version = "1.0.0";
    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        AnsiConsole.WriteLine($"Wayback Downloader v{Version}");
        PrintSettings(settings);

        ServiceProvider? serviceProvider = null;
        Task? pageWokerRunnerTask = null;

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.Token.Register(() => AnsiConsole.WriteLine("Shutting down."));

        try
        {
            var services = new ServiceCollection();

#pragma warning disable CA2000 // Dispose objects before losing scope
            //lifetime handled by DI container
            var rateLimiter = new SlidingWindowRateLimiter(new SlidingWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                SegmentsPerWindow = 1,
                PermitLimit = settings.RateLimit,
                Window = TimeSpan.FromSeconds(1),
                QueueLimit = int.MaxValue
            });
#pragma warning restore CA2000 // Dispose objects before losing scope

            services
#if DEBUG
                .AddCoreCommandServices(rateLimiter, settings.Verbose, mockData: settings.UseMockHandler)
#else
                .AddCoreCommandServices(rateLimiter, settings.Verbose)
#endif
                .AddSingleton(AnsiConsole.Create(new()))
                .AddSingleton<Ui>()
                .AddSingleton(new PageFilters([.. settings.PageFilters]));

            serviceProvider = services.BuildServiceProvider();

            var pagesStore = serviceProvider.GetRequiredService<PagesStore>();
            if (settings.ClearHistory)
            {
                pagesStore.PurgeCheckpoints();
            }

            var downloaderService = serviceProvider.GetRequiredService<DownloaderService>();
            var pageWorkerRunner = serviceProvider.GetRequiredService<PageWorkerRunner>();
            var loggingMessagesAccessor = serviceProvider.GetRequiredService<CollectedLogMessages>();
            var ui = serviceProvider.GetRequiredService<Ui>();

            var uiTask = ui.DrawUiAsync(cts.Token);

            var outputDir = settings.OutputDir;
            outputDir.Create();

            if (settings.LimitPages == 0)
            {
                return 0;
            }

            var downloaderTask = downloaderService.StartDownloadAsync(settings.MatchUrl, settings.MatchType, settings.ParsedFilters, settings.LimitPages, cts.Token);
            pageWorkerRunner.StartTasks(outputDir.FullName, settings.RateLimit, cts.Token);
            await downloaderTask.ConfigureAwait(false);
            pageWokerRunnerTask = pageWorkerRunner.WaitForCompletionAsync();
            await pageWokerRunnerTask.ConfigureAwait(false);

            await cts.CancelAsync().ConfigureAwait(false);

            await uiTask.ConfigureAwait(false);
            await serviceProvider.DisposeAsync().ConfigureAwait(false);

            foreach (var item in loggingMessagesAccessor.DrainMessages())
            {
                AnsiConsole.WriteLine(item.Message);
                if (item.Exception is not null)
                {
                    AnsiConsole.WriteException(item.Exception);
                }
            }
            return 0;
        }
#pragma warning disable CA1031 // Do not catch general exception types
        catch (Exception ex)
#pragma warning restore CA1031 // Do not catch general exception types
        {
            if (ex is not OperationCanceledException)
            {
                AnsiConsole.WriteLine("Exception encountered. Attempting to gracefully shutdown.");
                AnsiConsole.WriteException(ex);
                Cancel();

                var disposeException = await DisposeProviderAsync(serviceProvider).ConfigureAwait(false);
                if (disposeException is not null)
                {
                    AnsiConsole.WriteLine("Original exception: ");
                }
                else
                {
                    AnsiConsole.WriteLine("Shutdown gracefully. Original exception:");
                }
                AnsiConsole.WriteException(ex);
                return 1;
            }
            else
            {
                //TODO: Move this to the dipose method of the pageWorkerRunnerTask, assuming it will not cause a deadlock
                //if (pageWokerRunnerTask is not null)
                //{
                //    await pageWokerRunnerTask.WaitAsync(TimeSpan.FromSeconds(5), CancellationToken.None).ConfigureAwait(false);
                //}
                AnsiConsole.WriteLine("Background tasks finnished.");
                var disposeException = await DisposeProviderAsync(serviceProvider).ConfigureAwait(false);
                if (disposeException is not null)
                {
                    AnsiConsole.WriteException(disposeException);
                }
                else
                {
                    AnsiConsole.WriteLine("Shutdown successful");
                }
            }
            return 0;
        }
    }

    private static async Task<Exception?> DisposeProviderAsync(ServiceProvider? serviceProvider)
    {
        try
        {
            if (serviceProvider is not null)
            {
                await serviceProvider.DisposeAsync().ConfigureAwait(false);
            }
            return null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            AnsiConsole.WriteLine("Exception encountered during dispose.");
            AnsiConsole.WriteException(ex);
            return ex;
        }
    }

    private static void PrintSettings(DefaultCommand.Settings settings)
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

#if DEBUG
        if (settings.UseMockHandler)
        {
            AnsiConsole.Write($"Use mock handler: {settings.UseMockHandler} ");
            AnsiConsole.MarkupLine("[yellow]No real API requests will be made. Garbage data will be saved to disk.[/]");
        }
#endif
    }
}
