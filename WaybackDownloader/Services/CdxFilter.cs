using System.Diagnostics.CodeAnalysis;

namespace WaybackDownloader.Services;

internal sealed class CdxFilter
{
    public static readonly string[] WellKnownFieldNames = ["urlkey", "timestamp", "original", "mimetype", "statuscode", "digest", "length"];

    private CdxFilter(string fieldName, string expression, bool negate = false)
    {
        FieldName = fieldName;
        Expression = expression;
        Negate = negate;
    }

    public static CdxFilter Create(string fieldName, string expression, bool negate = false)
    {
        if (!TryParseDetails(fieldName, expression, negate, out var result, out var argument))
        {
            throw new ArgumentException(result.ErrorMessage, argument);
        }
#pragma warning disable CA1308 // Normalize strings to uppercase
        return new(fieldName.ToLowerInvariant(), expression, negate);
#pragma warning restore CA1308 // Normalize strings to uppercase
    }

    internal const string FilterExpression = "[!]<FieldName>:<Expression>";
    public static unsafe bool TryParseExpression(ReadOnlySpan<char> rawExpression, out FilterTryParseResult result)
    {
        if (!rawExpression.ContainsAny(":"))
        {
            result = FilterTryParseResult.Error($"Could not find a : in filter expression '{rawExpression}'. The filter expression should be of the form {FilterExpression}.");
            return false;
        }

        Span<Range> ranges = stackalloc Range[3];
        if (rawExpression.Split(ranges, ':', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries) > 2)
        {
            result = FilterTryParseResult.Error($"Multiple : characters found in filter expression '{rawExpression}'. The filter expression should be of the form {FilterExpression}.");
            return false;
        }

        var fieldNameRange = rawExpression[ranges[0]];
        string fieldName;
        bool negate;
        if (fieldNameRange[0] == '!')
        {
            negate = true;
            fieldName = fieldNameRange[1..].ToString();
        }
        else
        {
            negate = false;
            fieldName = fieldNameRange[..].ToString();
        }

        var expression = rawExpression[ranges[1]].ToString();

        return TryParseDetails(fieldName, expression, negate, out result, out _);
    }

    private static bool TryParseDetails(string fieldName, string expression, bool negate, out FilterTryParseResult result, [NotNullWhen(false)] out string? argument)
    {
        if (!WellKnownFieldNames.Contains(fieldName, StringComparer.OrdinalIgnoreCase))
        {
            result = FilterTryParseResult.Error($"Field name '{fieldName}' is not a valid CDX field name. Fields: {string.Join(", ", WellKnownFieldNames)}.");
            argument = nameof(fieldName);
            return false;
        }

        if (fieldName.Equals("statusCode", StringComparison.OrdinalIgnoreCase) &&
            !int.TryParse(expression, out _))
        {
            result = FilterTryParseResult.Error($"Filter value '{expression}' for statusCode filter could not be parsed to a valid integer.");
            argument = nameof(expression);
            return false;
        }

        result = FilterTryParseResult.Success(new(fieldName, expression, negate));
        argument = null;
        return true;
    }

    public string FieldName { get; }
    public string Expression { get; }
    public bool Negate { get; }
    public override string ToString() => $"{(Negate ? "!" : "")}{FieldName}:{Expression}";

    internal sealed record FilterTryParseResult
    {
        public FilterTryParseResult(bool success, string? errorMessage, CdxFilter? filter)
        {
            IsValid = success;
            ErrorMessage = errorMessage;
            Filter = filter;
        }

        [MemberNotNullWhen(true, nameof(Filter))]
        [MemberNotNullWhen(false, nameof(ErrorMessage))]
        public bool IsValid { get; }
        public string? ErrorMessage { get; }
        public CdxFilter? Filter { get; }

        public static FilterTryParseResult Error(string error) => new(false, error, null);
        public static FilterTryParseResult Success(CdxFilter filter) => new(true, null, filter);
    }
}
