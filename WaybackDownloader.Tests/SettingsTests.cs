namespace WaybackDownloader.Tests;
public class SettingsTests
{
    [Theory]
    [InlineData(1001L, true)]
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
    public void Validate_From_ReturnsExpectedResult(long timestamp, bool valid)
    {
        var settings = new DefaultCommand.Settings
        {
            From = timestamp
        };
        Assert.Equal(valid, settings.Validate().Successful);
    }

    [Theory]
    [InlineData(1001L, true)]
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
    public void Validate_To_ReturnsExpectedResult(long timestamp, bool valid)
    {
        var settings = new DefaultCommand.Settings
        {
            To = timestamp
        };
        Assert.Equal(valid, settings.Validate().Successful);
    }
}
