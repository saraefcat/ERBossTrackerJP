using ERBossTrackerJP.Core.Models;
using ERBossTrackerJP.Save.Exceptions;
using ERBossTrackerJP.Save.Reading;
using ERBossTrackerJP.Services.SaveFiles;

namespace ERBossTrackerJP.Tests.Services.SaveFiles;

public sealed class SaveLoadServiceTests
{
    [Fact]
    public async Task LoadAsync_ReadsSnapshotAndReturnsCopiedCharacterList()
    {
        byte[] bytes = [1, 2, 3, 4];
        DateTimeOffset lastWriteTime = new(2026, 9, 10, 1, 2, 3, TimeSpan.Zero);
        var snapshotReader = new StubSnapshotReader(
            new SaveFileSnapshot("C:\\saves\\ER0000.sl2", bytes, lastWriteTime));
        var slots = new List<CharacterSlot>
        {
            new(2, "褪せ人", 120),
        };
        var saveReader = new StubSaveReader(slots);
        var service = new SaveLoadService(snapshotReader, saveReader);
        using var cancellationSource = new CancellationTokenSource();

        LoadedSaveFile loadedSave = await service.LoadAsync(
            "selected.sl2",
            cancellationSource.Token);
        slots.Add(new CharacterSlot(3, "Tarnished", 150));

        Assert.Equal("selected.sl2", snapshotReader.ReceivedPath);
        Assert.Equal(cancellationSource.Token, snapshotReader.ReceivedCancellationToken);
        Assert.Equal(bytes, saveReader.CharacterData.ToArray());
        Assert.Equal("C:\\saves\\ER0000.sl2", loadedSave.SourcePath);
        Assert.Equal(lastWriteTime, loadedSave.LastWriteTimeUtc);
        Assert.Equal(bytes.Length, loadedSave.FileSize);
        Assert.Equal(new CharacterSlot(2, "褪せ人", 120), Assert.Single(loadedSave.CharacterSlots));
    }

    [Fact]
    public async Task ReadCharacterData_UsesTheAlreadyLoadedMemorySnapshot()
    {
        byte[] saveBytes = [10, 20, 30];
        byte[] eventFlags = [0x80, 0x01];
        var snapshotReader = new StubSnapshotReader(
            new SaveFileSnapshot(
                "C:\\saves\\ER0000.sl2",
                saveBytes,
                DateTimeOffset.UtcNow));
        var saveReader = new StubSaveReader([], eventFlags, totalDeathCount: 321);
        var service = new SaveLoadService(snapshotReader, saveReader);
        LoadedSaveFile loadedSave = await service.LoadAsync("selected.sl2");

        EventFlagSection result = service.ReadCharacterData(loadedSave, 4);

        Assert.Equal(eventFlags, result.Bytes.ToArray());
        Assert.Equal(321u, result.TotalDeathCount);
        Assert.Equal(saveBytes, saveReader.EventFlagData.ToArray());
        Assert.Equal(4, saveReader.EventFlagSlotIndex);
        Assert.Equal(1, snapshotReader.CallCount);
    }

    [Fact]
    public async Task LoadAsync_DoesNotParseWhenSnapshotReadFails()
    {
        var expected = new SaveFileReadException(
            SaveFileReadErrorCode.TemporarilyUnavailable,
            "locked.sl2",
            "locked",
            3);
        var snapshotReader = new StubSnapshotReader(expected);
        var saveReader = new StubSaveReader([]);
        var service = new SaveLoadService(snapshotReader, saveReader);

        SaveFileReadException actual = await Assert.ThrowsAsync<SaveFileReadException>(() =>
            service.LoadAsync("locked.sl2"));

        Assert.Same(expected, actual);
        Assert.False(saveReader.ReadCharacterSlotsCalled);
    }

    [Fact]
    public async Task LoadAsync_PreservesSaveParseError()
    {
        var snapshotReader = new StubSnapshotReader(
            new SaveFileSnapshot(
                "invalid.sl2",
                new byte[64],
                DateTimeOffset.UtcNow));
        var expected = new SaveParseException(
            SaveParseErrorCode.InvalidBnd4Magic,
            "invalid",
            0);
        var saveReader = new StubSaveReader(expected);
        var service = new SaveLoadService(snapshotReader, saveReader);

        SaveParseException actual = await Assert.ThrowsAsync<SaveParseException>(() =>
            service.LoadAsync("invalid.sl2"));

        Assert.Same(expected, actual);
    }

    [Fact]
    public void Constructor_RejectsNullDependencies()
    {
        var snapshotReader = new StubSnapshotReader(
            new SaveFileSnapshot("save.sl2", new byte[1], DateTimeOffset.UtcNow));
        var saveReader = new StubSaveReader([]);

        Assert.Throws<ArgumentNullException>(() => new SaveLoadService(null!, saveReader));
        Assert.Throws<ArgumentNullException>(() => new SaveLoadService(snapshotReader, null!));
    }

    private sealed class StubSnapshotReader : ISaveFileSnapshotReader
    {
        private readonly SaveFileSnapshot? _snapshot;
        private readonly Exception? _exception;

        public StubSnapshotReader(SaveFileSnapshot snapshot)
        {
            _snapshot = snapshot;
        }

        public StubSnapshotReader(Exception exception)
        {
            _exception = exception;
        }

        public string? ReceivedPath { get; private set; }

        public CancellationToken ReceivedCancellationToken { get; private set; }

        public int CallCount { get; private set; }

        public Task<SaveFileSnapshot> ReadAsync(
            string filePath,
            CancellationToken cancellationToken = default)
        {
            ReceivedPath = filePath;
            ReceivedCancellationToken = cancellationToken;
            CallCount++;

            if (_exception is not null)
            {
                return Task.FromException<SaveFileSnapshot>(_exception);
            }

            return Task.FromResult(_snapshot!);
        }
    }

    private sealed class StubSaveReader : IEldenRingSaveReader
    {
        private readonly IReadOnlyList<CharacterSlot> _characterSlots;
        private readonly ReadOnlyMemory<byte> _eventFlags;
        private readonly Exception? _characterException;

        public StubSaveReader(
            IReadOnlyList<CharacterSlot> characterSlots,
            ReadOnlyMemory<byte> eventFlags = default,
            uint totalDeathCount = 0)
        {
            _characterSlots = characterSlots;
            _eventFlags = eventFlags;
            TotalDeathCount = totalDeathCount;
        }

        public StubSaveReader(Exception characterException)
        {
            _characterSlots = [];
            _characterException = characterException;
        }

        public bool ReadCharacterSlotsCalled { get; private set; }

        public ReadOnlyMemory<byte> CharacterData { get; private set; }

        public ReadOnlyMemory<byte> EventFlagData { get; private set; }

        public int? EventFlagSlotIndex { get; private set; }

        public uint TotalDeathCount { get; }

        public IReadOnlyList<CharacterSlot> ReadCharacterSlots(ReadOnlyMemory<byte> saveData)
        {
            ReadCharacterSlotsCalled = true;
            CharacterData = saveData;

            if (_characterException is not null)
            {
                throw _characterException;
            }

            return _characterSlots;
        }

        public EventFlagSection ReadEventFlags(
            ReadOnlyMemory<byte> saveData,
            int slotIndex)
        {
            EventFlagData = saveData;
            EventFlagSlotIndex = slotIndex;
            return new EventFlagSection(_eventFlags, 123, TotalDeathCount);
        }
    }
}
