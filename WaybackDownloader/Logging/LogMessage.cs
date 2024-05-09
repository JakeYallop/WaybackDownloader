namespace WaybackDownloader.Logging;

internal sealed class LogMessage(string message, Exception? exception)
{
    public string Message { get; } = message;
    public Exception? Exception { get; } = exception;
}
