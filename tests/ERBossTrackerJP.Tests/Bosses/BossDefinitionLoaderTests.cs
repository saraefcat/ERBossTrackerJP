using System.Text;
using System.Text.Json;
using ERBossTrackerJP.Core.Bosses;
using ERBossTrackerJP.Core.Models;

namespace ERBossTrackerJP.Tests.Bosses;

public sealed class BossDefinitionLoaderTests
{
    private readonly BossDefinitionLoader _loader = new();

    [Fact]
    public void Read_RejectsEmptyDocumentIncludingByteOrderMarkOnly()
    {
        BossDataException emptyException = Assert.Throws<BossDataException>(() =>
            _loader.Read(ReadOnlyMemory<byte>.Empty));
        BossDataException byteOrderMarkException = Assert.Throws<BossDataException>(() =>
            _loader.Read(Encoding.UTF8.Preamble.ToArray()));

        Assert.Equal(BossDataErrorCode.EmptyDocument, emptyException.ErrorCode);
        Assert.Equal(BossDataErrorCode.EmptyDocument, byteOrderMarkException.ErrorCode);
    }

    [Fact]
    public void Read_RejectsDocumentAboveSizeLimit()
    {
        byte[] data = new byte[BossDefinitionLoader.MaximumDocumentSize + 1];

        BossDataException exception = Assert.Throws<BossDataException>(() => _loader.Read(data));

        Assert.Equal(BossDataErrorCode.DocumentTooLarge, exception.ErrorCode);
    }

    [Fact]
    public void Read_ValidatesBilingualDataAndReturnsSortOrder()
    {
        byte[] data = CreateDocument(
            CreateBoss(
                id: "dlc.gravesite.divine_beast",
                flagId: 20000800,
                nameEn: "Divine Beast Dancing Lion",
                nameJa: "神獣獅子舞",
                regionId: "gravesite_plain",
                regionEn: "Gravesite Plain",
                regionJa: "墓地平原",
                locationEn: "Belurat, Tower Settlement",
                locationJa: "塔の街、ベルラート",
                content: "shadowOfTheErdtree",
                sortOrder: 20),
            CreateBoss(sortOrder: 10));

        IReadOnlyList<BossDefinition> bosses = _loader.Read(data, expectedBossCount: 2);

        Assert.Collection(
            bosses,
            boss =>
            {
                Assert.Equal("base.limgrave.soldier_of_godrick", boss.Id);
                Assert.Equal(GameContent.BaseGame, boss.Content);
                Assert.Equal("ゴドリックの軍兵", boss.NameJa);
            },
            boss =>
            {
                Assert.Equal("dlc.gravesite.divine_beast", boss.Id);
                Assert.Equal(GameContent.ShadowOfTheErdtree, boss.Content);
                Assert.Equal("墓地平原", boss.RegionJa);
            });
    }

    [Fact]
    public void Read_AcceptsUtf8ByteOrderMark()
    {
        byte[] json = CreateDocument(CreateBoss());
        byte[] data = [.. Encoding.UTF8.Preamble, .. json];

        BossDefinition boss = Assert.Single(_loader.Read(data));

        Assert.Equal("ゴドリックの軍兵", boss.NameJa);
    }

    [Fact]
    public void Read_RejectsInvalidUtf8()
    {
        var exception = Assert.Throws<BossDataException>(() => _loader.Read(new byte[] { 0xFF }));

        Assert.Equal(BossDataErrorCode.InvalidEncoding, exception.ErrorCode);
    }

    [Fact]
    public void Read_RejectsMalformedJsonAndUnknownProperties()
    {
        byte[] malformed = "{"u8.ToArray();
        byte[] unknownProperty = CreateDocument(
            CreateBoss().Replace(
                "\"sortOrder\":10",
                "\"sortOrder\":10,\"unexpected\":true",
                StringComparison.Ordinal));

        BossDataException malformedException = Assert.Throws<BossDataException>(() =>
            _loader.Read(malformed));
        BossDataException unknownException = Assert.Throws<BossDataException>(() =>
            _loader.Read(unknownProperty));

        Assert.Equal(BossDataErrorCode.InvalidJson, malformedException.ErrorCode);
        Assert.Equal(BossDataErrorCode.InvalidJson, unknownException.ErrorCode);
    }

    [Fact]
    public void Read_RejectsUnsupportedSchemaVersion()
    {
        byte[] data = Encoding.UTF8.GetBytes(
            $$"""{"schemaVersion":2,"bosses":[{{CreateBoss()}}]}""");

        BossDataException exception = Assert.Throws<BossDataException>(() => _loader.Read(data));

        Assert.Equal(BossDataErrorCode.UnsupportedSchemaVersion, exception.ErrorCode);
        Assert.Equal("schemaVersion", exception.PropertyName);
    }

    [Fact]
    public void Read_RejectsUnexpectedProductionCount()
    {
        byte[] data = CreateDocument(CreateBoss());

        BossDataException exception = Assert.Throws<BossDataException>(() =>
            _loader.Read(data, BossDefinitionLoader.ProductionBossCount));

        Assert.Equal(BossDataErrorCode.InvalidBossCount, exception.ErrorCode);
    }

    [Theory]
    [InlineData("nameJa", "")]
    [InlineData("locationEn", "   ")]
    [InlineData("regionJa", " リムグレイブ")]
    public void Read_RejectsMissingOrPaddedRequiredText(string propertyName, string value)
    {
        string boss = propertyName switch
        {
            "nameJa" => CreateBoss(nameJa: value),
            "locationEn" => CreateBoss(locationEn: value),
            "regionJa" => CreateBoss(regionJa: value),
            _ => throw new ArgumentOutOfRangeException(nameof(propertyName)),
        };

        BossDataException exception = Assert.Throws<BossDataException>(() =>
            _loader.Read(CreateDocument(boss)));

        Assert.Equal(BossDataErrorCode.MissingRequiredField, exception.ErrorCode);
        Assert.Equal(propertyName, exception.PropertyName);
    }

    [Fact]
    public void Read_RejectsInvalidIdentifierFlagContentAndSortOrder()
    {
        AssertError(
            CreateBoss(id: "Base.Invalid"),
            BossDataErrorCode.InvalidIdentifier,
            "id");
        AssertError(
            CreateBoss(flagId: 0),
            BossDataErrorCode.InvalidFlagId,
            "flagId");
        AssertError(
            CreateBoss(content: "dlc"),
            BossDataErrorCode.InvalidContent,
            "content");
        AssertError(
            CreateBoss(sortOrder: -1),
            BossDataErrorCode.InvalidSortOrder,
            "sortOrder");
    }

    [Fact]
    public void Read_RejectsDuplicateStableId()
    {
        string first = CreateBoss();
        string second = CreateBoss(flagId: 20000800, sortOrder: 20);

        AssertError(
            CreateDocument(first, second),
            BossDataErrorCode.DuplicateId,
            "id");
    }

    [Fact]
    public void Read_RejectsDuplicateFlagId()
    {
        string first = CreateBoss();
        string second = CreateBoss(
            id: "base.limgrave.tree_sentinel",
            sortOrder: 20);

        AssertError(
            CreateDocument(first, second),
            BossDataErrorCode.DuplicateFlagId,
            "flagId");
    }

    [Fact]
    public void Read_RejectsDuplicateSortOrder()
    {
        string first = CreateBoss();
        string second = CreateBoss(
            id: "base.limgrave.tree_sentinel",
            flagId: 1042360800);

        AssertError(
            CreateDocument(first, second),
            BossDataErrorCode.DuplicateSortOrder,
            "sortOrder");
    }

    [Fact]
    public void Read_RejectsInconsistentRegionNames()
    {
        string first = CreateBoss();
        string second = CreateBoss(
            id: "base.limgrave.tree_sentinel",
            flagId: 1042360800,
            regionJa: "不一致な地域名",
            sortOrder: 20);

        AssertError(
            CreateDocument(first, second),
            BossDataErrorCode.InconsistentRegion,
            "regionId");
    }

    private void AssertError(
        string boss,
        BossDataErrorCode expectedError,
        string expectedProperty) =>
        AssertError(CreateDocument(boss), expectedError, expectedProperty);

    private void AssertError(
        byte[] data,
        BossDataErrorCode expectedError,
        string expectedProperty)
    {
        BossDataException exception = Assert.Throws<BossDataException>(() => _loader.Read(data));

        Assert.Equal(expectedError, exception.ErrorCode);
        Assert.Equal(expectedProperty, exception.PropertyName);
    }

    private static byte[] CreateDocument(params string[] bosses) =>
        Encoding.UTF8.GetBytes(
            $$"""{"schemaVersion":1,"bosses":[{{string.Join(',', bosses)}}]}""");

    private static string CreateBoss(
        string id = "base.limgrave.soldier_of_godrick",
        uint flagId = 18000850,
        string nameEn = "Soldier of Godrick",
        string nameJa = "ゴドリックの軍兵",
        string regionId = "limgrave",
        string regionEn = "Limgrave",
        string regionJa = "リムグレイブ",
        string locationEn = "Stranded Graveyard",
        string locationJa = "漂着墓地",
        string content = "baseGame",
        int sortOrder = 10) =>
        JsonSerializer.Serialize(new
        {
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
            sortOrder,
        });
}
