using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace WaybackDownloader.Logging;

internal static class LoggingBuilderExtensions
{
    public static ILoggingBuilder AddCollectLogMessagesLogger(this ILoggingBuilder builder)
    {
        builder.Services.TryAddSingleton<CollectedLogMessages>();
        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<ILoggerProvider, CollectLogMessagesLoggerProvider>());
        return builder;
    }
}
