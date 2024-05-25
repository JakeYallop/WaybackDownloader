using System.Threading.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Spectre.Console;

namespace WaybackDownloader.Services;

public sealed class PageWorkerRunner(IServiceProvider serviceProvider, ILogger<PageWorkerRunner> logger) : IDisposable, IAsyncDisposable
{
    private readonly TaskCompletionSource _completionSource = new();
    private readonly List<Task> _tasks = [];
#pragma warning disable CA2213 // Disposable fields should be disposed
    //disposed by DI container
    private readonly RateLimiter _limiter = serviceProvider.GetRequiredKeyedService<RateLimiter>(PageWorker.PageWorkerHttpClientRateLimiterKey);
#pragma warning restore CA2213 // Disposable fields should be disposed
    public void StartTasks(string outputDir, int requestedDownloadLimit, CancellationToken cancellationToken)
    {
        _tasks.Add(StartAsync(outputDir, cancellationToken));
        _tasks.Add(EvaluateLimitAsync(outputDir, requestedDownloadLimit, cancellationToken));

        Task.WhenAll(_tasks).ContinueWith(t =>
        {
            _completionSource.SetResult();
        }, TaskScheduler.Default);
    }

    public Task WaitForCompletionAsync() => _completionSource.Task;

    private Task StartAsync(string outputDir, CancellationToken cancellationToken)
    {
        var worker = serviceProvider.GetRequiredService<PageWorker>();
        return worker.StartAsync(outputDir, cancellationToken);
    }

    private long _lastTotalLeases;
    private int _numberOfEvaluationsAtRequiredSpeed;
    private async Task EvaluateLimitAsync(string outputDir, int requestedDownloadLimit, CancellationToken cancellationToken)
    {
        const int SegmentDurationSeconds = 2;
        const float MinimumThreshold = 0.9f;
        await Task.Yield();
        while (!cancellationToken.IsCancellationRequested)
        {
            if (_tasks.Any(x => x.IsCompleted))
            {
                break;
            }

            await Task.Delay(SegmentDurationSeconds * 1000, cancellationToken).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);

            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            var stats = _limiter.GetStatistics();
            if (stats is null || stats.TotalSuccessfulLeases == 0)
            {
                logger.NoStatisticsAvailable();
                _numberOfEvaluationsAtRequiredSpeed++;
                continue;
            }

            var downloadsInLastSegment = stats.TotalSuccessfulLeases - _lastTotalLeases;
            var downloadsPerSecond = (float)downloadsInLastSegment / SegmentDurationSeconds;
            var ratioActualRequested = downloadsPerSecond / requestedDownloadLimit;
            logger.DownloadSpeed(_tasks.Count, downloadsInLastSegment, downloadsPerSecond, requestedDownloadLimit);
            if (ratioActualRequested < MinimumThreshold)
            {
                _numberOfEvaluationsAtRequiredSpeed = 0;
                _tasks.Add(StartAsync(outputDir, cancellationToken));
            }
            else
            {
                _numberOfEvaluationsAtRequiredSpeed++;
            }

            _lastTotalLeases = stats.TotalSuccessfulLeases;

            if (_numberOfEvaluationsAtRequiredSpeed > 5)
            {
                break;
            }
        }
    }

    public void Dispose() => DisposeAllAsync().AsTask().GetAwaiter().GetResult();
    public ValueTask DisposeAsync() => DisposeAllAsync();

    private async ValueTask DisposeAllAsync()
    {
        AnsiConsole.WriteLine("Waiting up to 10 seconds for workers to finish.");
        await WaitForCompletionAsync().WaitAsync(TimeSpan.FromSeconds(10), CancellationToken.None).ConfigureAwait(false);
        return;
    }
}

internal static partial class LoggerExtensions
{
    [LoggerMessage(LogLevel.Information, Message = "Could not evaluate if new workers are required to meet requested dowload speeds as no statistics were available")]
    public static partial void NoStatisticsAvailable(this ILogger<PageWorkerRunner> logger);

    [LoggerMessage(LogLevel.Debug, Message = "Workers: {Workers}, Last Segment: {LastSegment}, Download Speed: {CurrentSpeed}/{RequestedSpeed}")]
    public static partial void DownloadSpeed(this ILogger<PageWorkerRunner> logger, int workers, long lastSegment, float currentSpeed, int requestedSpeed);
}
