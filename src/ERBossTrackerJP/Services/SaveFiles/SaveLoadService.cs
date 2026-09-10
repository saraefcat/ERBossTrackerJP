using ERBossTrackerJP.Core.Models;
using ERBossTrackerJP.Save.Reading;

namespace ERBossTrackerJP.Services.SaveFiles;

public sealed class SaveLoadService : ISaveLoadService
{
    private readonly ISaveFileSnapshotReader _snapshotReader;
    private readonly IEldenRingSaveReader _saveReader;

    public SaveLoadService()
        : this(new SaveFileSnapshotReader(), new EldenRingSaveReader())
    {
    }

    public SaveLoadService(
        ISaveFileSnapshotReader snapshotReader,
        IEldenRingSaveReader saveReader)
    {
        ArgumentNullException.ThrowIfNull(snapshotReader);
        ArgumentNullException.ThrowIfNull(saveReader);

        _snapshotReader = snapshotReader;
        _saveReader = saveReader;
    }

    public async Task<LoadedSaveFile> LoadAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        SaveFileSnapshot snapshot = await _snapshotReader.ReadAsync(
            filePath,
            cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<CharacterSlot> characterSlots =
            _saveReader.ReadCharacterSlots(snapshot.Bytes);
        return new LoadedSaveFile(snapshot, characterSlots);
    }

    public ReadOnlyMemory<byte> ReadEventFlags(LoadedSaveFile loadedSave, int slotIndex)
    {
        ArgumentNullException.ThrowIfNull(loadedSave);

        EventFlagSection section = _saveReader.ReadEventFlags(
            loadedSave.Bytes,
            slotIndex);
        return section.Bytes;
    }
}
