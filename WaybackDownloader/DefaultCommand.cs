using System.Threading.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;
using Spectre.Console.Cli;
using WaybackDownloader.Services;
using WaybackDownloader.Spectre;
using Settings = WaybackDownloader.DefaultCommand.Settings;

namespace WaybackDownloader;

internal sealed partial class DefaultCommand : CancellableAsyncCommand<Settings>
{
    /// <summary>
    /// Version string without the leading 'v'.
    /// </summary>
    public const string Version = "1.0.0";
    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
    {
        AnsiConsole.WriteLine($"Wayback Downloader v{Version}");
        PrintSettings(settings);

        ServiceProvider? serviceProvider = null;
        Task? pageWorkerRunnerTask = null;
        Task? uiTask = null;

        cancellationToken.Register(() => AnsiConsole.WriteLine("Shutting down..."));
        using var workerCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        using var uiCts = new CancellationTokenSource();

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
                .AddCoreCommandServices(rateLimiter, settings.DownloadHistoryLogDir.FullName, settings.Verbose, settings.UseMockHandler)
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
            var ui = serviceProvider.GetRequiredService<Ui>();

            uiTask = ui.DrawUiAsync(uiCts.Token);

            var outputDir = settings.OutputDir;
            outputDir.Create();

            if (settings.LimitPages == 0)
            {
                return 0;
            }

            var downloaderTask = downloaderService.StartDownloadAsync(settings.MatchUrl, settings.MatchType, settings.ParsedFilters, settings.LimitPages, workerCts.Token);
            pageWorkerRunner.StartTasks(outputDir.FullName, settings.RateLimit, workerCts.Token);
            pageWorkerRunnerTask = pageWorkerRunner.WaitForCompletionAsync();
            await downloaderTask.ConfigureAwait(false);
            await pageWorkerRunnerTask.ConfigureAwait(false);

            await workerCts.CancelAsync().ConfigureAwait(false);
            await uiCts.CancelAsync().ConfigureAwait(false);

            await uiTask.ConfigureAwait(false);
            await serviceProvider.DisposeAsync().ConfigureAwait(false);
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
                if (pageWorkerRunnerTask is not null)
                {
                    await pageWorkerRunnerTask.ConfigureAwait(false);
                }
                await uiCts.CancelAsync().ConfigureAwait(false);
                if (uiTask is not null)
                {
                    await uiTask.ConfigureAwait(false);
                }
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
}
