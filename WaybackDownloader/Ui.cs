using System.Collections.Immutable;
using System.Globalization;
using Spectre.Console;
using WaybackDownloader;
using WaybackDownloader.Logging;

internal sealed class Ui(IAnsiConsole console, CollectedLogMessages logMessages)
{
    private readonly IAnsiConsole _console = console;
    private readonly CollectedLogMessages _logMessages = logMessages;
    private bool _requiresUpdate;

    public async Task DrawUiAsync(CancellationToken cancellationToken)
    {
        await Task.Yield();
        var statsTask = LogStatsAsync(cancellationToken);
        while (!cancellationToken.IsCancellationRequested)
        {
            var messages = ReadMessages();
            if (!_requiresUpdate)
            {
                continue;
            }

            foreach (var message in messages)
            {
                _console.Markup(message.Message);
                _console.WriteLine();
                if (message.Exception is not null)
                {
                    _console.WriteException(message.Exception, ExceptionFormats.ShortenPaths);
                }
            }
            _requiresUpdate = false;
        }
        await statsTask.ConfigureAwait(false);
    }

    private static readonly Style BoldStyle = new(decoration: Decoration.Bold);
    private static readonly Style NumberStyle = new(Color.Aqua, Color.Black);
    private async Task LogStatsAsync(CancellationToken cancellationToken)
    {
        await Task.Delay(3000, default).ConfigureAwait(false);
        while (!cancellationToken.IsCancellationRequested)
        {
            var filesWritten = Counters.FilesWritten.Count;
            var filesSkipped = Counters.FilesSkipped.Count;
            var filesUpdated = Counters.FilesUpdated.Count;

            var p = new Paragraph();
            p.Append("Files Written: ", BoldStyle);
            p.Append(filesWritten.ToString(CultureInfo.CurrentCulture), NumberStyle);
            p.Append(", ");
            p.Append("Files Skipped: ", BoldStyle);
            p.Append(filesSkipped.ToString(CultureInfo.CurrentCulture), NumberStyle);
            p.Append(", ");
            p.Append("Files Updated: ", BoldStyle);
            p.Append(filesUpdated.ToString(CultureInfo.CurrentCulture), NumberStyle);
            p.Append(Environment.NewLine);
            _console.Write(p);

            await Task.Delay(3000, default).ConfigureAwait(false);
        }
    }

    private ImmutableArray<LogMessage> ReadMessages()
    {
        var newMessages = _logMessages.DrainMessages();
        if (newMessages.Length > 0)
        {
            _requiresUpdate = true;
        }
        return newMessages;
    }
}
