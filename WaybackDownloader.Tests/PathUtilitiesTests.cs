namespace WaybackDownloader.Tests;

public class PathUtilitiesTests
{
    [Theory]
    [InlineData("https://www.archive.org:80?12345", "index_12345.html")]
    [InlineData("https://www.archive.org?12345", "index_12345.html")]
    [InlineData("https://www.archive.org", "index.html")]
    [InlineData("https://www.archive.org:80", "index.html")]
    [InlineData("https://archive.org:80", "index.html")]
    [InlineData("https://www.archive.org:80/s/abc/test?a=1&b=2&c=3", "s/abc/test_a=1&b=2&c=3.html")]
    [InlineData("https://archive.org/s/abc/test.html?a=1&b=2&c=3", "s/abc/test_a=1&b=2&c=3.html")]
    [InlineData("https://www.archive.org/s/abc/test.html", "s/abc/test.html")]
    [InlineData("https://www.archive.org/test.html", "test.html")]
    [InlineData("https://www.archive.org/s/test.html", "s/test.html")]
    [InlineData("https://www.archive.org?", "index.html")]
    [InlineData("https://www.archive.org//", "index.html")]
    [InlineData("https://www.archive.org//////////////", "index.html")]
    [InlineData("https://archive.org////////////////#FIX_YOUR_SERVER_REDIRECT_PROBLEMS", "index.html")]
    [InlineData("https://archive.org/?+-6863+union+all+select+1%2C1%2C1%2CCONCAT%280x3a6f79753a%2C0x4244764877697569706b%2C0x3a70687a3a%291%23=%29+AND+%28SELECT+8041+FROM%28SELECT+COUNT%28%2A%29%2CCONCAT%280x3a6f79753a%2C%28SELECT+%28CASE+WHEN+%288041%3D8041%29+THEN+1+ELSE+0+END%29%29%2C0x3a70687a3a%2Cfloor%28rand%280%29%2A2%29%29x+FROM+INFORMATION_SCHEMA.CHARACTER_SETS+GROUP+BY+x%29a%29+AND+%287609%3D7609&page=2&scroll=1", "index_+-6863+union+all+select+1,1,1,CONCAT(0x3a6f79753a,0x4244764877697569706b,0x3a70687a3a)1#=)+AND+(SELECT+8041+FROM(SELECT+COUNT(_),CONCAT(0x3a6f79753a,(SELECT+(CASE+WHEN+(8041=8041)+THEN+1+ELSE+0+END)),0x3a70687a3a,floor(rand(0)_2))x+FROM+INFORMATION_SCHEMA.CHARACTER_SETS+GROUP+BY+x)a)+AND+(7609=7609&page=2&scroll=1.html")]
    [InlineData("http://archive.org/0/", "0/index.html")]
    [MemberData(nameof(GetTestDataWithDisallowedPathChars))]
#pragma warning disable CA1054 // URI-like parameters should not be strings
    public void TryGetNormalizedFilePath_ValidUri_ProducesExpectedResult(string actualUrl, string expectedPath)
#pragma warning restore CA1054 // URI-like parameters should not be strings
    {
        var success = PathUtilities.TryGetNormalizedFilePath(actualUrl, out var actualPath);
        Assert.True(success);
        Assert.Equal(expectedPath, actualPath);
    }

    public static TheoryData<string, string> GetTestDataWithDisallowedPathChars()
    {

        var invalidCharacters = new string(Path.GetInvalidFileNameChars());
        return new()
        {
            { $"https://www.archive.org/s/abc/test.html?{invalidCharacters}", $"s/abc/test_{new string('_', invalidCharacters.Length)}.html" }
        };
    }
}
