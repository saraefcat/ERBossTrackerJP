using ERBossTrackerJP.Core.Models;

namespace ERBossTrackerJP.Tests.Models;

public sealed class TrackerSnapshotTests
{
    [Fact]
    public void Constructor_DerivesSummaryFromBossProgress()
    {
        var definition = new BossDefinition(
            "base.test.boss",
            1000,
            "Test Boss",
            "テストボス",
            "test-region",
            "Test Region",
            "テスト地域",
            "Test Location",
            "テスト場所",
            GameContent.BaseGame,
            1);

        var snapshot = new TrackerSnapshot(
            DateTimeOffset.UnixEpoch,
            new CharacterSlot(0, "Tarnished", 1),
            [new BossProgress(definition, true), new BossProgress(definition, false)],
            []);

        Assert.Equal(1, snapshot.Defeated);
        Assert.Equal(2, snapshot.Total);
        Assert.Equal(1, snapshot.Remaining);
        Assert.Equal(50d, snapshot.ProgressPercentage);
    }
}
