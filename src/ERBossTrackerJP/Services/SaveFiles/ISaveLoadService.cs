using ERBossTrackerJP.Save.Reading;

namespace ERBossTrackerJP.Services.SaveFiles;

public interface ISaveLoadService
{
    Task<LoadedSaveFile> LoadAsync(
        string filePath,
        CancellationToken cancellationToken = default);

    EventFlagSection ReadCharacterData(LoadedSaveFile loadedSave, int slotIndex);
}
