using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;

namespace WaybackDownloader;
public static class PathUtilities
{

#pragma warning disable CA1054 // URI-like parameters should not be strings
    public static bool TryGetNormalizedFilePath([NotNullWhen(true)] string? url, [NotNullWhen(true)] out string? normalizedPath)
#pragma warning restore CA1054 // URI-like parameters should not be strings
    {
        normalizedPath = null;
        ArgumentException.ThrowIfNullOrWhiteSpace(url);

        var uri = new Uri(url);

        var path = uri.AbsolutePath[1..]; //remove leading slash
        var query = Uri.UnescapeDataString(uri.Query);
        if (query == "?")
        {
            query = "";
        }

        var dirName = Path.GetDirectoryName(path);
        var fileName = Path.GetFileNameWithoutExtension(path);

        if (string.IsNullOrEmpty(fileName))
        {
            fileName = "index";
        }

        path = string.IsNullOrEmpty(dirName) || !dirName.AsSpan().ContainsAnyExcept(@"/\")
            ? ""
            : !string.IsNullOrEmpty(dirName) ? $"{dirName}/" : dirName;

        var sb = new StringBuilder();
        sb.Append(CultureInfo.InvariantCulture, $"{path}{fileName}{query}.html");

        foreach (var c in Path.GetInvalidFileNameChars())
        {
            sb.Replace(c, '_', path.Length, sb.Length - path.Length);
        }
        sb.Replace('\\', '/', 0, path.Length);

        normalizedPath = sb.ToString();
        return true;
    }
}
