using ToolTikTokV11.Services;
using ToolTikTokV11.Utils;
using Xunit;

namespace ToolTikTok.Automation.Tests;

public sealed class PureLogicTests
{
    [Theory]
    [InlineData("1.2K", 1200)]
    [InlineData("3M viewers", 3_000_000)]
    [InlineData("không có dữ liệu", -1)]
    public void ViewerCountParser_HandlesSupportedFormats(string raw, int expected)
        => Assert.Equal(expected, ViewerCountParser.Parse(raw));

    [Fact]
    public void ContentLineHelper_NormalizesAndRemovesBlankLines()
    {
        var lines = ContentLineHelper.GetAutomationLinesFromText(" first \r\n\r\n second ");
        Assert.Equal(["first", "second"], lines);
    }
}
