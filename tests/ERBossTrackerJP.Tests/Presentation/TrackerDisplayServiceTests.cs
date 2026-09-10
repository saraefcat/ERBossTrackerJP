using ERBossTrackerJP.Core.Models;
using ERBossTrackerJP.Core.Presentation;

namespace ERBossTrackerJP.Tests.Presentation;

public sealed class TrackerDisplayServiceTests
{
    private readonly TrackerDisplayService _service = new();

    [Fact]
    public void Create_LocalizesBossesAndRegionsWithoutChangingSummary()
    {
        TrackerSnapshot snapshot = CreateSnapshot();

        TrackerDisplayModel display = _service.Create(
            snapshot,
            new BossListFilter(DisplayLanguage.Japanese));

        Assert.Equal(snapshot.UpdatedAt, display.UpdatedAt);
        Assert.Same(snapshot.Character, display.Character);
        Assert.Equal(snapshot.Defeated, display.Defeated);
        Assert.Equal(snapshot.Total, display.Total);
        Assert.Equal(snapshot.Remaining, display.Remaining);
        Assert.Equal(snapshot.ProgressPercentage, display.ProgressPercentage);
        Assert.Collection(
            display.Bosses,
            boss =>
            {
                Assert.Equal("base.limgrave.tree_sentinel", boss.Id);
                Assert.Equal("ツリーガード", boss.Name);
                Assert.Equal("リムグレイブ", boss.Region);
                Assert.Equal("エレの教会", boss.Location);
            },
            boss => Assert.Equal("飛竜アギール", boss.Name),
            boss => Assert.Equal("レラーナ、双月の騎士", boss.Name));
        Assert.Collection(
            display.Regions,
            region => Assert.Equal(
                new RegionListItem("limgrave", "リムグレイブ", 1, 2),
                region),
            region => Assert.Equal(
                new RegionListItem("gravesite_plain", "墓地平原", 1, 1),
                region));
    }

    [Fact]
    public void Create_LanguageSwitchPreservesIdentityStateAndOrder()
    {
        TrackerSnapshot snapshot = CreateSnapshot();

        TrackerDisplayModel japanese = _service.Create(
            snapshot,
            new BossListFilter(DisplayLanguage.Japanese));
        TrackerDisplayModel english = _service.Create(
            snapshot,
            new BossListFilter(DisplayLanguage.English));

        Assert.Equal(
            japanese.Bosses.Select(BossIdentity),
            english.Bosses.Select(BossIdentity));
        Assert.Equal(
            japanese.Regions.Select(region => (region.RegionId, region.Defeated, region.Total)),
            english.Regions.Select(region => (region.RegionId, region.Defeated, region.Total)));
        Assert.Equal("ツリーガード", japanese.Bosses[0].Name);
        Assert.Equal("Tree Sentinel", english.Bosses[0].Name);
        Assert.Equal("リムグレイブ", japanese.Regions[0].Name);
        Assert.Equal("Limgrave", english.Regions[0].Name);
    }

    [Fact]
    public void Create_CombinesCompletionRegionContentAndNameFilters()
    {
        TrackerDisplayModel display = _service.Create(
            CreateSnapshot(),
            new BossListFilter(
                DisplayLanguage.Japanese,
                BossCompletionFilter.Undefeated,
                RegionId: "limgrave",
                Content: GameContent.BaseGame,
                SearchText: " 飛竜 "));

        BossListItem boss = Assert.Single(display.Bosses);
        Assert.Equal("base.limgrave.flying_dragon_agheel", boss.Id);
        Assert.False(boss.IsDefeated);
    }

    [Theory]
    [InlineData(BossCompletionFilter.All, 3)]
    [InlineData(BossCompletionFilter.Defeated, 2)]
    [InlineData(BossCompletionFilter.Undefeated, 1)]
    public void Create_FiltersCompletionState(
        BossCompletionFilter completion,
        int expectedCount)
    {
        TrackerDisplayModel display = _service.Create(
            CreateSnapshot(),
            new BossListFilter(DisplayLanguage.Japanese, completion));

        Assert.Equal(expectedCount, display.Bosses.Count);
    }

    [Fact]
    public void Create_SearchUsesTheSelectedDisplayLanguage()
    {
        TrackerSnapshot snapshot = CreateSnapshot();

        TrackerDisplayModel japanese = _service.Create(
            snapshot,
            new BossListFilter(
                DisplayLanguage.Japanese,
                SearchText: "飛竜"));
        TrackerDisplayModel englishMismatch = _service.Create(
            snapshot,
            new BossListFilter(
                DisplayLanguage.English,
                SearchText: "飛竜"));
        TrackerDisplayModel english = _service.Create(
            snapshot,
            new BossListFilter(
                DisplayLanguage.English,
                SearchText: "DRAGON"));

        Assert.Equal(
            "base.limgrave.flying_dragon_agheel",
            Assert.Single(japanese.Bosses).Id);
        Assert.Empty(englishMismatch.Bosses);
        Assert.Equal(
            "base.limgrave.flying_dragon_agheel",
            Assert.Single(english.Bosses).Id);
    }

    [Fact]
    public void Create_BlankSearchDoesNotFilterBosses()
    {
        TrackerDisplayModel display = _service.Create(
            CreateSnapshot(),
            new BossListFilter(DisplayLanguage.Japanese, SearchText: " \t "));

        Assert.Equal(3, display.Bosses.Count);
    }

    [Fact]
    public void Create_RejectsInvalidFilterValues()
    {
        TrackerSnapshot snapshot = CreateSnapshot();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            _service.Create(
                snapshot,
                new BossListFilter((DisplayLanguage)99)));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            _service.Create(
                snapshot,
                new BossListFilter(
                    DisplayLanguage.Japanese,
                    (BossCompletionFilter)99)));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            _service.Create(
                snapshot,
                new BossListFilter(
                    DisplayLanguage.Japanese,
                    Content: (GameContent)99)));
        Assert.Throws<ArgumentException>(() =>
            _service.Create(
                snapshot,
                new BossListFilter(
                    DisplayLanguage.Japanese,
                    RegionId: " ")));
    }

    private static TrackerSnapshot CreateSnapshot()
    {
        BossDefinition treeSentinel = CreateBoss(
            "base.limgrave.tree_sentinel",
            1042360800,
            "Tree Sentinel",
            "ツリーガード",
            "limgrave",
            "Limgrave",
            "リムグレイブ",
            "Church of Elleh",
            "エレの教会",
            GameContent.BaseGame,
            10);
        BossDefinition agheel = CreateBoss(
            "base.limgrave.flying_dragon_agheel",
            1043360800,
            "Flying Dragon Agheel",
            "飛竜アギール",
            "limgrave",
            "Limgrave",
            "リムグレイブ",
            "Agheel Lake",
            "アギール湖",
            GameContent.BaseGame,
            20);
        BossDefinition rellana = CreateBoss(
            "dlc.gravesite_plain.rellana",
            2048440800,
            "Rellana, Twin Moon Knight",
            "レラーナ、双月の騎士",
            "gravesite_plain",
            "Gravesite Plain",
            "墓地平原",
            "Castle Ensis",
            "エンシスの城砦",
            GameContent.ShadowOfTheErdtree,
            30);

        return new TrackerSnapshot(
            new DateTimeOffset(2026, 9, 10, 1, 2, 3, TimeSpan.Zero),
            new CharacterSlot(0, "Tarnished", 150),
            [
                new BossProgress(treeSentinel, true),
                new BossProgress(agheel, false),
                new BossProgress(rellana, true),
            ],
            [
                new RegionProgress("limgrave", "Limgrave", "リムグレイブ", 1, 2),
                new RegionProgress(
                    "gravesite_plain",
                    "Gravesite Plain",
                    "墓地平原",
                    1,
                    1),
            ]);
    }

    private static BossDefinition CreateBoss(
        string id,
        uint flagId,
        string nameEn,
        string nameJa,
        string regionId,
        string regionEn,
        string regionJa,
        string locationEn,
        string locationJa,
        GameContent content,
        int sortOrder) =>
        new(
            id,
            flagId,
            nameEn,
            nameJa,
            regionId,
            regionEn,
            regionJa,
            locationEn,
            locationJa,
            content,
            sortOrder);

    private static (string Id, uint FlagId, bool IsDefeated, int SortOrder) BossIdentity(
        BossListItem boss) =>
        (boss.Id, boss.FlagId, boss.IsDefeated, boss.SortOrder);
}
