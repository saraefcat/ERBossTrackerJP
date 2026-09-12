using ERBossTrackerJP.Core.Models;
using ERBossTrackerJP.Save.Progress;
using ERBossTrackerJP.Save.Reading;
using ERBossTrackerJP.Services.SaveFiles;
using ERBossTrackerJP.Services.Tracking;

namespace ERBossTrackerJP.Tests.Services.Tracking;

public sealed class TrackerSnapshotServiceTests
{
    [Fact]
    public void Create_ReadsSelectedSlotAndBuildsSnapshotFromCopiedDefinitions()
    {
        var character = new CharacterSlot(3, "Tracked Hero", 125);
        LoadedSaveFile loadedSave = CreateLoadedSave(character);
        var saveLoadService = new StubSaveLoadService(
            new byte[] { 0x80, 0x01 },
            totalDeathCount: 456);
        var progressService = new StubBossProgressService();
        BossDefinition definition = CreateBossDefinition();
        var definitions = new List<BossDefinition> { definition };
        var service = new TrackerSnapshotService(
            saveLoadService,
            progressService,
            definitions);
        definitions.Clear();

        TrackerSnapshot snapshot = service.Create(
            loadedSave,
            character,
            deathCountBaseline: 123,
            isDeathCountOffsetEnabled: true);

        Assert.Same(progressService.Result, snapshot);
        Assert.Same(loadedSave, saveLoadService.ReceivedLoadedSave);
        Assert.Equal(3, saveLoadService.ReceivedSlotIndex);
        Assert.Equal(new byte[] { 0x80, 0x01 }, progressService.ReceivedEventFlags.ToArray());
        Assert.Equal(loadedSave.LastWriteTimeUtc, progressService.ReceivedUpdatedAt);
        Assert.Same(character, progressService.ReceivedCharacter);
        Assert.Same(definition, Assert.Single(progressService.ReceivedDefinitions!));
        Assert.Equal(456u, progressService.ReceivedSaveDeathCount);
        Assert.Equal(123u, progressService.ReceivedDeathCountBaseline);
        Assert.True(progressService.ReceivedIsDeathCountOffsetEnabled);
    }

    [Fact]
    public void Create_RejectsCharacterOutsideLoadedSave()
    {
        var loadedCharacter = new CharacterSlot(0, "Loaded", 10);
        LoadedSaveFile loadedSave = CreateLoadedSave(loadedCharacter);
        var service = new TrackerSnapshotService(
            new StubSaveLoadService(Array.Empty<byte>()),
            new StubBossProgressService(),
            [CreateBossDefinition()]);

        ArgumentException exception = Assert.Throws<ArgumentException>(() =>
            service.Create(loadedSave, new CharacterSlot(1, "Other", 20)));

        Assert.Equal("character", exception.ParamName);
    }

    private static LoadedSaveFile CreateLoadedSave(CharacterSlot character) =>
        new(
            new SaveFileSnapshot(
                "C:\\saves\\ER0000.sl2",
                new byte[128],
                new DateTimeOffset(2026, 9, 11, 1, 2, 3, TimeSpan.Zero)),
            [character]);

    private static BossDefinition CreateBossDefinition() =>
        new(
            "base.limgrave.test",
            1000,
            "Test Boss",
            "テストボス",
            "limgrave",
            "Limgrave",
            "リムグレイブ",
            "Test Location",
            "テスト場所",
            GameContent.BaseGame,
            0);

    private sealed class StubSaveLoadService(
        ReadOnlyMemory<byte> eventFlags,
        uint totalDeathCount = 0) : ISaveLoadService
    {
        public LoadedSaveFile? ReceivedLoadedSave { get; private set; }

        public int? ReceivedSlotIndex { get; private set; }

        public Task<LoadedSaveFile> LoadAsync(
            string filePath,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public EventFlagSection ReadCharacterData(
            LoadedSaveFile loadedSave,
            int slotIndex)
        {
            ReceivedLoadedSave = loadedSave;
            ReceivedSlotIndex = slotIndex;
            return new EventFlagSection(eventFlags, 123, totalDeathCount);
        }
    }

    private sealed class StubBossProgressService : IBossProgressService
    {
        public StubBossProgressService()
        {
            var character = new CharacterSlot(0, "Result", 1);
            Result = new TrackerSnapshot(
                DateTimeOffset.UnixEpoch,
                character,
                [],
                []);
        }

        public TrackerSnapshot Result { get; }

        public DateTimeOffset? ReceivedUpdatedAt { get; private set; }

        public CharacterSlot? ReceivedCharacter { get; private set; }

        public ReadOnlyMemory<byte> ReceivedEventFlags { get; private set; }

        public IReadOnlyList<BossDefinition>? ReceivedDefinitions { get; private set; }

        public uint? ReceivedSaveDeathCount { get; private set; }

        public uint? ReceivedDeathCountBaseline { get; private set; }

        public bool? ReceivedIsDeathCountOffsetEnabled { get; private set; }

        public TrackerSnapshot CreateSnapshot(
            DateTimeOffset updatedAt,
            CharacterSlot character,
            ReadOnlyMemory<byte> eventFlags,
            IReadOnlyList<BossDefinition> bossDefinitions,
            uint saveDeathCount = 0,
            uint deathCountBaseline = 0,
            bool isDeathCountOffsetEnabled = false)
        {
            ReceivedUpdatedAt = updatedAt;
            ReceivedCharacter = character;
            ReceivedEventFlags = eventFlags;
            ReceivedDefinitions = bossDefinitions;
            ReceivedSaveDeathCount = saveDeathCount;
            ReceivedDeathCountBaseline = deathCountBaseline;
            ReceivedIsDeathCountOffsetEnabled = isDeathCountOffsetEnabled;
            return Result;
        }
    }
}
