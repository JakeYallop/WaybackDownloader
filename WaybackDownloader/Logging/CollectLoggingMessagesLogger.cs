using System.Text;
using Microsoft.Extensions.Logging;
using Spectre.Console;

namespace WaybackDownloader.Logging;

public interface IConsoleMessageColorProvider
{
    public string GetLeadingColorString(ConsoleColor? background, ConsoleColor? foreground);
    public string GetTrailingColorString(ConsoleColor? background, ConsoleColor? foreground);
}

public class SpectreConsoleMessageColorProvider : IConsoleMessageColorProvider
{
    public string GetLeadingColorString(ConsoleColor? background, ConsoleColor? foreground)
    {
        return (background, foreground) switch
        {
            (null, null) => "",
            (not null, null) => $"[default on {Color.FromConsoleColor(background.Value).ToMarkup()}]",
            (null, not null) => $"[{Color.FromConsoleColor(foreground.Value).ToMarkup()} on default]",
            (not null, not null) => $"[{Color.FromConsoleColor(foreground.Value).ToMarkup()} on {Color.FromConsoleColor(background.Value).ToMarkup()}]"
        };
    }

    public string GetTrailingColorString(ConsoleColor? background, ConsoleColor? foreground)
    {
        return (background, foreground) switch
        {
            (null, null) => "",
            _ => "[/]"
        };
    }
}

//modified from https://github.com/dotnet/runtime/blob/a8e74e34dc796a488a5c9e76f3c4e85133d603ae/src/libraries/Microsoft.Extensions.Logging.Console/src/SimpleConsoleFormatter.cs
internal sealed class CollectLoggingMessagesLogger(CollectedLogMessages logMessages, IConsoleMessageColorProvider colorProvider) : ILogger
{
    private readonly CollectedLogMessages _logMessages = logMessages;
    private readonly IConsoleMessageColorProvider _consoleMessageColorProvider = colorProvider;

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => throw new NotImplementedException();
    public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        var logLevelString = GetLogLevelString(logLevel);
        var colors = GetLogLevelConsoleColors(logLevel);
        var coloredString = GetColoredLogLevelStringSpectre(logLevelString, colors.Background, colors.Foreground);
        var messageString = $"{DateTime.UtcNow:HH:mm:ss} {coloredString} {(exception is null ? formatter(state, exception) : "").EscapeMarkup()}";
        _logMessages.LogMessage(messageString, exception);
    }

    private string GetColoredLogLevelStringSpectre(string message, ConsoleColor? background, ConsoleColor? foreground)
    {
        var sb = new StringBuilder(20);
        sb.Append(_consoleMessageColorProvider.GetLeadingColorString(background, foreground));
        sb.Append(message);
        sb.Append(_consoleMessageColorProvider.GetTrailingColorString(background, foreground));
        return sb.ToString();
    }

    private static ConsoleColors GetLogLevelConsoleColors(LogLevel logLevel)
    {
        return logLevel switch
        {
            LogLevel.Trace => new ConsoleColors(ConsoleColor.Gray, ConsoleColor.Black),
            LogLevel.Debug => new ConsoleColors(ConsoleColor.Gray, ConsoleColor.Black),
            LogLevel.Information => new ConsoleColors(ConsoleColor.DarkGreen, ConsoleColor.Black),
            LogLevel.Warning => new ConsoleColors(ConsoleColor.Yellow, ConsoleColor.Black),
            LogLevel.Error => new ConsoleColors(ConsoleColor.Black, ConsoleColor.DarkRed),
            LogLevel.Critical => new ConsoleColors(ConsoleColor.White, ConsoleColor.DarkRed),
            _ => new ConsoleColors(null, null)
        };
    }

    private static string GetLogLevelString(LogLevel logLevel)
    {
        return logLevel switch
        {
            LogLevel.Trace => "trce",
            LogLevel.Debug => "dbug",
            LogLevel.Information => "info",
            LogLevel.Warning => "warn",
            LogLevel.Error => "fail",
            LogLevel.Critical => "crit",
            _ => throw new ArgumentOutOfRangeException(nameof(logLevel))
        };
    }

    private readonly struct ConsoleColors(ConsoleColor? foreground, ConsoleColor? background)
    {
        public ConsoleColor? Foreground { get; } = foreground;
        public ConsoleColor? Background { get; } = background;
    }

}
