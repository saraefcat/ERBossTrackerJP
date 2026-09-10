namespace ERBossTrackerJP.Services.SaveFiles;

public interface ISaveLoadService
{
    Task<LoadedSaveFile> LoadAsync(
        string filePath,
        CancellationToken cancellationToken = default);

    ReadOnlyMemory<byte> ReadEventFlags(LoadedSaveFile loadedSave, int slotIndex);
}
