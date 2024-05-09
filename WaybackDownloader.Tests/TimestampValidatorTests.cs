namespace WaybackDownloader.Tests;
public sealed class TimestampValidatorTests
{
    [Theory]
    [InlineData(0001L, false)]
    [InlineData(9999L, true)]
    [InlineData(20241231L, true)]
    [InlineData(2024123123L, true)]
    [InlineData(202412312359L, true)]
    [InlineData(20241231235959L, true)]
    [InlineData(10010101000001L, true)]
    [InlineData(202413L, false)]
    [InlineData(20241260L, false)]
    [InlineData(2024125960L, false)]
    [InlineData(202412595660L, false)]
    public void Validate_ReturnsExpectedResult(long timestamp, bool valid)
    {
        var result = TimestampValidator.ValidateTimestamp(timestamp, "timestamp", out var _);
        Assert.Equal(result, valid);
    }

    [Theory]
    [InlineData(9999L, 3, 9)]
    [InlineData(9999L, 2, 99)]
    [InlineData(9999L, 1, 999)]
    [InlineData(100L, 1, 0)]
    [InlineData(1001L, 1, 1)]
    [InlineData(9001L, 2, 1)]
    [InlineData(901L, 2, 1)]
    [InlineData(109L, 1, 9)]
    [InlineData(109L, 2, 9)]
    public void TruncateStartSaturating_ReturnsExpectedResult(long value, int truncate, long expectedValue)
    {
        var result = TimestampValidator.TruncateStartSaturating(value, truncate);
        Assert.Equal(result, expectedValue);
    }

    [Theory]
    [InlineData(9999L, 3, 9)]
    [InlineData(9999L, 2, 99)]
    [InlineData(9999L, 1, 999)]
    [InlineData(1001L, 1, 100)]
    [InlineData(9001L, 2, 90)]
    [InlineData(901L, 2, 9)]
    [InlineData(109L, 1, 10)]
    [InlineData(109L, 2, 1)]
    public void TruncateEndSaturating_ReturnsExpectedResult(long value, int truncate, long expectedValue)
    {
        var result = TimestampValidator.TruncateEndSaturating(value, truncate);
        Assert.Equal(result, expectedValue);
    }
}
