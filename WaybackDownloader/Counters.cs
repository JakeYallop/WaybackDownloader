namespace WaybackDownloader;

public static class Counters
{
    public static readonly Counter FilesUpdated = new();
    public static readonly Counter FilesWritten = new();
    public static readonly Counter FilesSkipped = new();
}
