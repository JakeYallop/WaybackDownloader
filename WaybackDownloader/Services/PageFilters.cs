//TODO: use ImmuatableArray
internal sealed class PageFilters(string[] filters)
{
    //TODO: use SearchValues<string> on .NET 9

    //Use ROM instead, then make this async and run filter checking in parallel
    public bool IsMatch(ReadOnlySpan<char> page)
    {
        var match = false;
        foreach (var filter in filters.AsSpan())
        {
            match = page.Contains(filter, StringComparison.OrdinalIgnoreCase);
            if (match)
            {
                break;
            }
        }
        return match;
    }

    public bool Any() => filters.Length != 0;
}
