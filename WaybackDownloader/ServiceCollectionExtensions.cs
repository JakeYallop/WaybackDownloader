using System.Threading.Channels;
using System.Threading.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;
using WaybackDownloader.Logging;
using WaybackDownloader.Services;

namespace WaybackDownloader;
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCoreCommandServices(this IServiceCollection services, RateLimiter pageWorkerHttpClientRateLimiter, string downloadLogPath, bool verbose = false, bool mockData = false)
    {
        services.AddLogging(builder =>
        {
            builder
            .AddCollectLogMessagesLogger()
            .AddFilter("Polly", LogLevel.Warning)
            .AddFilter("Microsoft.Extensions.Http.DefaultHttpClientFactory", LogLevel.Warning)
            .SetMinimumLevel(verbose ? LogLevel.Trace : LogLevel.Information);
        })
        .ConfigureHttpClientDefaults(configure =>
        {
            configure.RemoveAllLoggers();

            if (mockData)
            {
                configure.ConfigurePrimaryHttpMessageHandler(s => new MockDataHttpMessageHandler());
            }
            configure.ConfigureHttpClient(x =>
            {
                x.DefaultRequestHeaders.UserAgent.Add(new("WaybackDownloader", DefaultCommand.Version));
            });
        })
        .AddHttpClient<WaybackCdxClient>()
        .AddResilienceHandler("WaybackClientPipeline", builder =>
        {
            builder
            .AddRateLimiter(new SlidingWindowRateLimiter(new SlidingWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = 1,
                QueueLimit = int.MaxValue,
                Window = TimeSpan.FromSeconds(1),
                SegmentsPerWindow = 1
            }))
            .AddRetry(new HttpRetryStrategyOptions
            {
                BackoffType = DelayBackoffType.Constant,
                MaxRetryAttempts = 5,
                Delay = TimeSpan.FromSeconds(2),
                ShouldRetryAfterHeader = true,
                UseJitter = true,
                ShouldHandle = static args => ValueTask.FromResult(HttpClientResiliencePredicates.IsTransient(args.Outcome) || args.Outcome.Result?.StatusCode is System.Net.HttpStatusCode.RequestTimeout),
            })
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions<HttpResponseMessage>());
        })
        .Services
        .AddResiliencePipeline("DirectoryCreatePipeline", configure =>
        {
            configure.AddRetry(new Polly.Retry.RetryStrategyOptions
            {
                BackoffType = DelayBackoffType.Linear,
                MaxRetryAttempts = 5,
                Delay = TimeSpan.FromMilliseconds(200),
                UseJitter = false,
                ShouldHandle = static args =>
                {
                    return ValueTask.FromResult(
                        args is { Outcome.Exception: IOException or UnauthorizedAccessException }
                    );
                }
            });
        })
        .AddKeyedSingleton(PageWorker.PageWorkerHttpClientRateLimiterKey, pageWorkerHttpClientRateLimiter)
        .AddHttpClient<PageWorker>()
        .AddResilienceHandler("PageWorkerClientPipeline", configure =>
        {
            configure
                .AddRateLimiter(pageWorkerHttpClientRateLimiter)
                .AddRetry(new HttpRetryStrategyOptions
                {
                    BackoffType = DelayBackoffType.Linear,
                    MaxRetryAttempts = 3,
                    Delay = TimeSpan.FromSeconds(10),
                    ShouldRetryAfterHeader = true,
                    UseJitter = true,
                    ShouldHandle = static args => ValueTask.FromResult(HttpClientResiliencePredicates.IsTransient(args.Outcome) || args.Outcome.Result?.StatusCode is System.Net.HttpStatusCode.RequestTimeout),
                });
        })
        .Services
        .AddSingleton(Channel.CreateBounded<CdxRecord>(new BoundedChannelOptions(200)
        {
            SingleWriter = true,
            SingleReader = false,
            AllowSynchronousContinuations = false
        }))
        .AddSingleton<DownloaderService>()
        .AddSingleton<PageWorkerRunner>()
        .AddSingleton(_ => new PagesStore(downloadLogPath))
        .AddSingleton<IConsoleMessageColorProvider, SpectreConsoleMessageColorProvider>();

        return services;
    }
}
