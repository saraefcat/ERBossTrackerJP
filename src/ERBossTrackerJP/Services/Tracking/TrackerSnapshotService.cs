using ERBossTrackerJP.Core.Models;
using ERBossTrackerJP.Save.Progress;
using ERBossTrackerJP.Save.Reading;
using ERBossTrackerJP.Services.SaveFiles;

namespace ERBossTrackerJP.Services.Tracking;

public sealed class TrackerSnapshotService : ITrackerSnapshotService
{
    private readonly ISaveLoadService _saveLoadService;
    private readonly IBossProgressService _bossProgressService;
    private readonly IReadOnlyList<BossDefinition> _bossDefinitions;

    public TrackerSnapshotService(
        ISaveLoadService saveLoadService,
        IBossProgressService bossProgressService,
        IReadOnlyList<BossDefinition> bossDefinitions)
    {
        ArgumentNullException.ThrowIfNull(saveLoadService);
        ArgumentNullException.ThrowIfNull(bossProgressService);
        ArgumentNullException.ThrowIfNull(bossDefinitions);

        if (bossDefinitions.Count == 0)
        {
            throw new ArgumentException(
                "At least one boss definition is required.",
                nameof(bossDefinitions));
        }

        _saveLoadService = saveLoadService;
        _bossProgressService = bossProgressService;
        _bossDefinitions = Array.AsReadOnly(bossDefinitions.ToArray());
    }

    public TrackerSnapshot Create(
        LoadedSaveFile loadedSave,
        CharacterSlot character,
        uint deathCountBaseline = 0,
        bool isDeathCountOffsetEnabled = false)
    {
        ArgumentNullException.ThrowIfNull(loadedSave);
        ArgumentNullException.ThrowIfNull(character);

        if (!loadedSave.CharacterSlots.Any(
                slot => slot.SlotIndex == character.SlotIndex))
        {
            throw new ArgumentException(
                "The selected character does not belong to the loaded save file.",
                nameof(character));
        }

        EventFlagSection characterData = _saveLoadService.ReadCharacterData(
            loadedSave,
            character.SlotIndex);
        return _bossProgressService.CreateSnapshot(
            loadedSave.LastWriteTimeUtc,
            character,
            characterData.Bytes,
            _bossDefinitions,
            characterData.TotalDeathCount,
            deathCountBaseline,
            isDeathCountOffsetEnabled);
    }
}
