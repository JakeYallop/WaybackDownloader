using Spectre.Console.Cli;
using WaybackDownloader;

var app = new CommandApp<DefaultCommand>();
await app.RunAsync(args).ConfigureAwait(false);
