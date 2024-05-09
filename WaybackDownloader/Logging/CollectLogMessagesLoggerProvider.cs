using Microsoft.Extensions.Logging;

namespace WaybackDownloader.Logging;

internal sealed class CollectLogMessagesLoggerProvider(CollectedLogMessages loggingQueueWrapper, IConsoleMessageColorProvider consoleMessageColorProvider) : ILoggerProvider
{
    private readonly CollectedLogMessages _loggingQueueWrapper = loggingQueueWrapper;
    private readonly IConsoleMessageColorProvider _consoleMessageColorProvider = consoleMessageColorProvider;

    public ILogger CreateLogger(string categoryName) => new CollectLoggingMessagesLogger(_loggingQueueWrapper, _consoleMessageColorProvider);
    public void Dispose() { }
}
