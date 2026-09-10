using ERBossTrackerJP.Core.Bosses;
using ERBossTrackerJP.Core.Models;

namespace ERBossTrackerJP.Tests.Bosses;

public sealed class EmbeddedBossDefinitionsTests
{
    [Fact]
    public void Load_ReturnsCompleteBilingualProductionCatalog()
    {
        IReadOnlyList<BossDefinition> bosses = EmbeddedBossDefinitions.Load();

        Assert.Equal(BossDefinitionLoader.ProductionBossCount, bosses.Count);
        Assert.Equal(165, bosses.Count(boss => boss.Content == GameContent.BaseGame));
        Assert.Equal(
            42,
            bosses.Count(boss => boss.Content == GameContent.ShadowOfTheErdtree));
        Assert.Equal(32, bosses.Select(boss => boss.RegionId).Distinct().Count());
        Assert.All(
            bosses,
            boss =>
            {
                Assert.False(string.IsNullOrWhiteSpace(boss.NameEn));
                Assert.False(string.IsNullOrWhiteSpace(boss.NameJa));
                Assert.False(string.IsNullOrWhiteSpace(boss.RegionEn));
                Assert.False(string.IsNullOrWhiteSpace(boss.RegionJa));
                Assert.False(string.IsNullOrWhiteSpace(boss.LocationEn));
                Assert.False(string.IsNullOrWhiteSpace(boss.LocationJa));
            });
    }

    [Theory]
    [InlineData(18000850u, "ゴドリックの軍兵", "リムグレイブ")]
    [InlineData(15000800u, "ミケラの刃、マレニア（腐敗の女神、マレニア）", "ミケラの聖樹")]
    [InlineData(20000800u, "神獣獅子舞", "墓地平原")]
    [InlineData(21010800u, "串刺し公、メスメル(邪な蛇、メスメル)", "影のアルター")]
    [InlineData(20010800u, "約束の王、ラダーン(ミケラの王、ラダーン)", "エニル・イリム")]
    public void Load_ContainsReviewedJapaneseNames(
        uint flagId,
        string expectedNameJa,
        string expectedRegionJa)
    {
        BossDefinition boss = Assert.Single(
            EmbeddedBossDefinitions.Load(),
            definition => definition.FlagId == flagId);

        Assert.Equal(expectedNameJa, boss.NameJa);
        Assert.Equal(expectedRegionJa, boss.RegionJa);
    }

    [Fact]
    public void Load_UsesRegionAsLocationWhenUpstreamPlaceIsBlank()
    {
        BossDefinition boss = Assert.Single(
            EmbeddedBossDefinitions.Load(),
            definition => definition.FlagId == 1043370800u);

        Assert.Equal("Limgrave", boss.LocationEn);
        Assert.Equal("リムグレイブ", boss.LocationJa);
    }
}
