namespace WaybackDownloader.Services;

public static class MatchTypes
{
    public const string Exact = "exact";
    public const string Prefix = "prefix";
    public const string Host = "host";
    public const string Domain = "domain";

    public static bool IsValid(string? matchType) =>
        string.Equals(matchType, Exact, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(matchType, Prefix, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(matchType, Host, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(matchType, Domain, StringComparison.OrdinalIgnoreCase);
}
