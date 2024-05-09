using System.Diagnostics.CodeAnalysis;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace WaybackDownloader;
internal class TimestampValidator
{
    //TODO: Return some kind of result so we don't need to pass in the property name
    public static bool ValidateTimestamp(long? timestamp, string property, [NotNullWhen(false)] out string? error)
    {
        const int Min = 1001;
        const long Max = 9999_1231_235959L;
        error = null;

        if (!timestamp.HasValue)
        {
            return true;
        }

        if (timestamp is < Min or > Max)
        {
            error = $"{property} timestamp invalid. Value expected to be in the range '{Min}' to '{Max}'.";
            return false;
        }

        if (timestamp <= 9999)
        {
            return true;
        }

        var length = CountDigits((ulong)timestamp.Value);
        var year = (int)ExtractFragment(timestamp.Value, 0..4);
        var month = (int)ExtractFragment(timestamp.Value, 4..6);
        var day = (int)ExtractFragment(timestamp.Value, 6..8);
        var hour = (int)ExtractFragment(timestamp.Value, 8..10);
        var minute = (int)ExtractFragment(timestamp.Value, 10..12);
        var second = (int)ExtractFragment(timestamp.Value, 12..14);

        if (length > 4 && month is < 1 or > 12)
        {
            error = $"{property} timestamp invalid. Month value must be in the range 1 - 12.";
            return false;
        }
        else if (length <= 6)
        {
            return true;
        }

        var upperBound = DateTime.DaysInMonth(year, month);
        if (length > 6 && (day is < 1 || day > upperBound))
        {
            error = $"{property} timestamp invalid. Day value must be in the range 1 - {upperBound}.";
            return false;
        }
        else if (length <= 8)
        {
            return true;
        }

        if (length > 8 && (hour < 0 || hour > 23))
        {
            error = $"{property} timestamp invalid. Hour value must be in the range 0 - 23.";
            return false;
        }
        else if (length <= 10)
        {
            return true;
        }

        if (length > 10 && (minute < 0 || minute > 59))
        {
            error = $"{property} timestamp invalid. Minute value must be in the range 0 - 59.";
            return false;
        }
        else if (length <= 12)
        {
            return true;
        }

        if (length > 12 && (second < 0 || second > 59))
        {
            error = $"{property} timestamp invalid. Second value must be in the range 0 - 59.";
            return false;
        }

        return true;
    }

    private static long ExtractFragment(long value, Range range)
    {
        var length = CountDigits((ulong)value);
        var newValue = value;
        newValue = TruncateStartSaturating(newValue, range.Start.GetOffset(length));

        if (newValue == 0)
        {
            return newValue;
        }

        var newLength = CountDigits((ulong)newValue);
        //we need to handle any leading zeros that may have been lost
        var expectedLength = length - range.Start.GetOffset(length);
        var digitsLost = expectedLength - newLength;
        newValue = TruncateEndSaturating(newValue, length - range.End.GetOffset(length));
        return newValue;
    }

    internal static long TruncateStartSaturating(long value, int digits)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(digits);

        if (digits == 0)
        {
            return value;
        }

        var length = CountDigits((ulong)value);
        if (length <= digits)
        {
            return 0;
        }

        long power = 1;
        var raiseToPower = length - digits;
        while (power < value && raiseToPower > 0)
        {
            power *= 10;
            raiseToPower--;
        }
        return value % power;
    }

    internal static long TruncateEndSaturating(long value, int digits)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(digits);
        if (digits == 0)
        {
            return value;
        }

        while (value >= 10 && digits > 0)
        {
            value /= 10;
            digits--;
        }

        return digits > 0 && value < 10 ? 0 : value;
    }

    private static ReadOnlySpan<ulong> _powersOf10 =>
    [
        0, // unused entry to avoid needing to subtract
            0,
            10,
            100,
            1000,
            10000,
            100000,
            1000000,
            10000000,
            100000000,
            1000000000,
            10000000000,
            100000000000,
            1000000000000,
            10000000000000,
            100000000000000,
            1000000000000000,
            10000000000000000,
            100000000000000000,
            1000000000000000000,
            10000000000000000000,
        ];

    // Map log2(ulong) to a power of 10.
    private static ReadOnlySpan<byte> _log2ToPow10 =>
    [
        1, 1, 1, 2, 2, 2, 3, 3, 3, 4, 4, 4, 4, 5, 5, 5,
            6, 6, 6, 7, 7, 7, 7, 8, 8, 8, 9, 9, 9, 10, 10, 10,
            10, 11, 11, 11, 12, 12, 12, 13, 13, 13, 13, 14, 14, 14, 15, 15,
            15, 16, 16, 16, 16, 17, 17, 17, 18, 18, 18, 19, 19, 19, 19, 20
    ];

    //https://github.com/dotnet/runtime/blob/0fb0188a137f3d53a2ebd719d7a684327938609a/src/libraries/System.Private.CoreLib/src/System/Buffers/Text/FormattingHelpers.CountDigits.cs#L15
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int CountDigits(ulong value)
    {
        Debug.Assert(_log2ToPow10.Length == 64);

        // Replace with log2ToPow10[BitOperations.Log2(value)] once https://github.com/dotnet/runtime/issues/79257 is fixed
        uint index = Unsafe.Add(ref MemoryMarshal.GetReference(_log2ToPow10), BitOperations.Log2(value));

        Debug.Assert((index + 1) <= _powersOf10.Length);
        var powerOf10 = Unsafe.Add(ref MemoryMarshal.GetReference(_powersOf10), index);

        // Return the number of digits based on the power of 10, shifted by 1
        // if it falls below the threshold.
        var lessThan = value < powerOf10;
        return (int)(index - Unsafe.As<bool, byte>(ref lessThan)); // while arbitrary bools may be non-0/1, comparison operators are expected to return 0/1
    }
}
