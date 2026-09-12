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
            [],
            saveDeathCount: 1_048,
            deathCountBaseline: 200,
            isDeathCountOffsetEnabled: true);

        Assert.Equal(1, snapshot.Defeated);
        Assert.Equal(2, snapshot.Total);
        Assert.Equal(1, snapshot.Remaining);
        Assert.Equal(50d, snapshot.ProgressPercentage);
        Assert.Equal(1_048u, snapshot.SaveDeathCount);
        Assert.Equal(200u, snapshot.DeathCountBaseline);
        Assert.True(snapshot.IsDeathCountOffsetEnabled);
        Assert.Equal(1_048ul, snapshot.CumulativeDeathCount);
        Assert.Equal(848u, snapshot.DisplayDeathCount);
        Assert.False(snapshot.IsDeathCountBaselineAboveCumulative);
    }

    [Fact]
    public void Constructor_ClampsDisplayDeathCountWhenBaselineExceedsCumulative()
    {
        var snapshot = new TrackerSnapshot(
            DateTimeOffset.UnixEpoch,
            new CharacterSlot(0, "Tarnished", 1),
            [],
            [],
            saveDeathCount: 100,
            deathCountBaseline: 200,
            isDeathCountOffsetEnabled: true);

        Assert.Equal(0u, snapshot.DisplayDeathCount);
        Assert.True(snapshot.IsDeathCountBaselineAboveCumulative);
    }

    [Fact]
    public void Constructor_IgnoresStoredBaselineWhenOffsetIsDisabled()
    {
        var snapshot = new TrackerSnapshot(
            DateTimeOffset.UnixEpoch,
            new CharacterSlot(0, "Tarnished", 1),
            [],
            [],
            saveDeathCount: 100,
            deathCountBaseline: 80,
            isDeathCountOffsetEnabled: false);

        Assert.Equal(100u, snapshot.DisplayDeathCount);
        Assert.False(snapshot.IsDeathCountBaselineAboveCumulative);
    }
}
