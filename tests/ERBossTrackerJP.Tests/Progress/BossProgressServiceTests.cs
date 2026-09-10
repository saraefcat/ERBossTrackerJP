using ERBossTrackerJP.Core.Models;
using ERBossTrackerJP.Save.Exceptions;
using ERBossTrackerJP.Save.Progress;

namespace ERBossTrackerJP.Tests.Progress;

public sealed class BossProgressServiceTests
{
    [Fact]
    public void CreateSnapshot_DefaultServiceUsesEmbeddedBlockMap()
    {
        var service = new BossProgressService();
        var eventFlags = new byte[126];
        eventFlags[125] = 0x80;

        TrackerSnapshot snapshot = service.CreateSnapshot(
            DateTimeOffset.UnixEpoch,
            new CharacterSlot(0, "Tarnished", 1),
            eventFlags,
            [CreateBoss()]);

        Assert.True(Assert.Single(snapshot.Bosses).IsDefeated);
    }

    [Fact]
    public void CreateSnapshot_CombinesFlagsAndBuildsOrderedSummaries()
    {
        var service = new BossProgressService(
            new Dictionary<uint, uint> { [1] = 0 });
        DateTimeOffset updatedAt = new(2026, 9, 10, 1, 2, 3, TimeSpan.Zero);
        var character = new CharacterSlot(3, "褪せ人", 150);
        BossDefinition[] definitions =
        [
            CreateBoss(
                "base.region-b.third",
                1002,
                "region-b",
                "Region B",
                "地域B",
                30),
            CreateBoss(
                "base.region-a.second",
                1001,
                "region-a",
                "Region A",
                "地域A",
                20),
            CreateBoss(
                "base.region-a.first",
                1000,
                "region-a",
                "Region A",
                "地域A",
                10),
        ];

        TrackerSnapshot snapshot = service.CreateSnapshot(
            updatedAt,
            character,
            new byte[] { 0b1010_0000 },
            definitions);

        Assert.Equal(updatedAt, snapshot.UpdatedAt);
        Assert.Same(character, snapshot.Character);
        Assert.Equal(2, snapshot.Defeated);
        Assert.Equal(3, snapshot.Total);
        Assert.Equal(1, snapshot.Remaining);
        Assert.Equal(200d / 3d, snapshot.ProgressPercentage);
        Assert.Collection(
            snapshot.Bosses,
            boss =>
            {
                Assert.Equal("base.region-a.first", boss.Boss.Id);
                Assert.True(boss.IsDefeated);
            },
            boss =>
            {
                Assert.Equal("base.region-a.second", boss.Boss.Id);
                Assert.False(boss.IsDefeated);
            },
            boss =>
            {
                Assert.Equal("base.region-b.third", boss.Boss.Id);
                Assert.True(boss.IsDefeated);
            });
        Assert.Collection(
            snapshot.Regions,
            region => Assert.Equal(
                new RegionProgress("region-a", "Region A", "地域A", 1, 2),
                region),
            region => Assert.Equal(
                new RegionProgress("region-b", "Region B", "地域B", 1, 1),
                region));
    }

    [Fact]
    public void Constructor_CopiesBlockMap()
    {
        var blockOffsets = new Dictionary<uint, uint> { [1] = 0 };
        var service = new BossProgressService(blockOffsets);
        blockOffsets.Clear();

        TrackerSnapshot snapshot = service.CreateSnapshot(
            DateTimeOffset.UnixEpoch,
            new CharacterSlot(0, "Tarnished", 1),
            new byte[] { 0x80 },
            [CreateBoss()]);

        Assert.True(Assert.Single(snapshot.Bosses).IsDefeated);
    }

    [Fact]
    public void CreateSnapshot_PreservesMissingBlockError()
    {
        var service = new BossProgressService(new Dictionary<uint, uint>());

        SaveParseException exception = Assert.Throws<SaveParseException>(() =>
            service.CreateSnapshot(
                DateTimeOffset.UnixEpoch,
                new CharacterSlot(0, "Tarnished", 1),
                new byte[] { 0x80 },
                [CreateBoss()]));

        Assert.Equal(SaveParseErrorCode.MissingEventFlagBlock, exception.ErrorCode);
    }

    [Fact]
    public void CreateSnapshot_PreservesOutOfBoundsError()
    {
        var service = new BossProgressService(
            new Dictionary<uint, uint> { [1] = 1 });

        SaveParseException exception = Assert.Throws<SaveParseException>(() =>
            service.CreateSnapshot(
                DateTimeOffset.UnixEpoch,
                new CharacterSlot(0, "Tarnished", 1),
                new byte[125],
                [CreateBoss()]));

        Assert.Equal(SaveParseErrorCode.EventFlagOutOfBounds, exception.ErrorCode);
    }

    [Fact]
    public void CreateSnapshot_RejectsEmptyOrNullDefinitions()
    {
        var service = new BossProgressService(
            new Dictionary<uint, uint> { [1] = 0 });
        var character = new CharacterSlot(0, "Tarnished", 1);

        Assert.Throws<ArgumentException>(() =>
            service.CreateSnapshot(DateTimeOffset.UnixEpoch, character, default, []));
        Assert.Throws<ArgumentException>(() =>
            service.CreateSnapshot(
                DateTimeOffset.UnixEpoch,
                character,
                default,
                [null!]));
    }

    [Fact]
    public void CreateSnapshot_RejectsInconsistentRegionMetadata()
    {
        var service = new BossProgressService(
            new Dictionary<uint, uint> { [1] = 0 });
        BossDefinition[] definitions =
        [
            CreateBoss(),
            CreateBoss(
                "base.region-a.second",
                1001,
                "region-a",
                "Different Region",
                "地域A",
                20),
        ];

        Assert.Throws<ArgumentException>(() =>
            service.CreateSnapshot(
                DateTimeOffset.UnixEpoch,
                new CharacterSlot(0, "Tarnished", 1),
                new byte[] { 0xC0 },
                definitions));
    }

    [Fact]
    public void Constructor_RejectsNullBlockMap()
    {
        Assert.Throws<ArgumentNullException>(() => new BossProgressService(null!));
    }

    private static BossDefinition CreateBoss(
        string id = "base.region-a.first",
        uint flagId = 1000,
        string regionId = "region-a",
        string regionEn = "Region A",
        string regionJa = "地域A",
        int sortOrder = 10) =>
        new(
            id,
            flagId,
            $"Boss {sortOrder}",
            $"ボス{sortOrder}",
            regionId,
            regionEn,
            regionJa,
            $"Location {sortOrder}",
            $"場所{sortOrder}",
            GameContent.BaseGame,
            sortOrder);
}
