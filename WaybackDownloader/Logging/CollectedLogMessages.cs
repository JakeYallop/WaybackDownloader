using System.Collections.Concurrent;
using System.Collections.Immutable;

namespace WaybackDownloader.Logging;

internal sealed class CollectedLogMessages
{
    private readonly ConcurrentQueue<LogMessage> _messages = [];
    public void LogMessage(string message, Exception? ex) => _messages.Enqueue(new(message, ex));

    public ImmutableArray<LogMessage> DrainMessages()
    {
        //Cannot use LINQ here - see https://github.com/dotnet/runtime/issues/101641
        //Do not use .Count - this locks the ConcurrentQueue and is very slow
        var array = ImmutableArray.CreateBuilder<LogMessage>(20);
        while (_messages.TryDequeue(out var m))
        {
            array.Add(m);
        }
        return array.Count == array.Capacity ? array.MoveToImmutable() : array.ToImmutable();
    }
}
