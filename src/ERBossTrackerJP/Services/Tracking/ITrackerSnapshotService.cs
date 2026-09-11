using ERBossTrackerJP.Core.Models;
using ERBossTrackerJP.Services.SaveFiles;

namespace ERBossTrackerJP.Services.Tracking;

public interface ITrackerSnapshotService
{
    TrackerSnapshot Create(LoadedSaveFile loadedSave, CharacterSlot character);
}
