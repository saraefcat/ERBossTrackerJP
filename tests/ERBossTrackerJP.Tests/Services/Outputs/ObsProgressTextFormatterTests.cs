using ERBossTrackerJP.Services.Outputs;

namespace ERBossTrackerJP.Tests.Services.Outputs;

public sealed class ObsProgressTextFormatterTests
{
    [Fact]
    public void Format_ReplacesAllSupportedVariables()
    {
        string result = ObsProgressTextFormatter.Format(
            "撃破 {defeated}/{total}体 残り{remaining}体（{percentage}%）",
            defeated: 155,
            remaining: 52,
            total: 207,
            percentage: 74.879);

        Assert.Equal("撃破 155/207体 残り52体（74.9%）", result);
    }

    [Fact]
    public void Format_AllowsEscapedLiteralBraces()
    {
        string result = ObsProgressTextFormatter.Format(
            "{{進捗}} {defeated}",
            defeated: 1,
            remaining: 1,
            total: 2,
            percentage: 50.0);

        Assert.Equal("{進捗} 1", result);
    }

    [Theory]
    [InlineData("")]
    [InlineData("進捗だけ")]
    [InlineData("{unknown}")]
    [InlineData("{defeated")]
    [InlineData("{defeated}}")]
    [InlineData("{defeated}\n{total}")]
    public void TryValidate_RejectsInvalidFormats(string format)
    {
        bool valid = ObsProgressTextFormatter.TryValidate(
            format,
            out string errorMessage);

        Assert.False(valid);
        Assert.NotEmpty(errorMessage);
    }
}
