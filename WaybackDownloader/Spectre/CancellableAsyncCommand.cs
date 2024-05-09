using Spectre.Console.Cli;

namespace WaybackDownloader.Spectre;

//Modified from https://github.com/spectreconsole/spectre.console/issues/701#issuecomment-1081834778
internal sealed class ConsoleAppCancellationTokenSource : IDisposable
{
    private readonly CancellationTokenSource _cts = new();
    private bool _disposedCts;

    public CancellationToken Token => _cts.Token;

    public ConsoleAppCancellationTokenSource()
    {
        System.Console.CancelKeyPress += OnCancelKeyPress;
        AppDomain.CurrentDomain.ProcessExit += OnProcessExit;

        using var _ = _cts.Token.Register(
            () =>
            {
                AppDomain.CurrentDomain.ProcessExit -= OnProcessExit;
                System.Console.CancelKeyPress -= OnCancelKeyPress;
            }
        );
    }

    private void OnCancelKeyPress(object? sender, ConsoleCancelEventArgs e)
    {
        // NOTE: cancel event, don't terminate the process
        e.Cancel = true;

        _cts.Cancel();
    }

    private void OnProcessExit(object? sender, EventArgs e)
    {
        //We could have disposed a linked source, which means this source could also be disposed
        //without the token being set to cancelled.
        if (_disposedCts)
        {
            return;
        }

        if (_cts.IsCancellationRequested)
        {
            // NOTE: SIGINT (cancel key was pressed, this shouldn't ever actually hit however, as we remove the event handler upon cancellation of the `cancellationSource`)
            return;
        }

        _cts.Cancel();
    }

    public void Dispose()
    {
        _cts.Dispose();
        _disposedCts = true;
    }

    public void Cancel() => _cts.Cancel();
}

//Modified from https://github.com/spectreconsole/spectre.console/issues/701#issuecomment-1081834778
public abstract class CancellableAsyncCommand : AsyncCommand, IDisposable
{
    private readonly ConsoleAppCancellationTokenSource _cts = new();
    private bool _disposedValue;

    public abstract Task<int> ExecuteAsync(CommandContext context, CancellationToken cancellation);

    public sealed override async Task<int> ExecuteAsync(CommandContext context)
        => await ExecuteAsync(context, _cts.Token).ConfigureAwait(false);

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            if (disposing)
            {
                _cts?.Dispose();
            }
            _disposedValue = true;
        }
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}

//Modified from https://github.com/spectreconsole/spectre.console/issues/701#issuecomment-1081834778
public abstract class CancellableAsyncCommand<TSettings> : AsyncCommand<TSettings>, IDisposable
    where TSettings : CommandSettings
{
    private readonly ConsoleAppCancellationTokenSource _cts = new();
    private bool _disposedValue;

    public abstract Task<int> ExecuteAsync(CommandContext context, TSettings settings, CancellationToken cancellation);

    public sealed override async Task<int> ExecuteAsync(CommandContext context, TSettings settings)
        => await ExecuteAsync(context, settings, _cts.Token).ConfigureAwait(false);

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            if (disposing)
            {
                _cts?.Dispose();
            }
            _disposedValue = true;
        }
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    protected void Cancel() => _cts.Cancel();
}
