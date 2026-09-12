namespace ERBossTrackerJP.Services.Settings;

public sealed record DeathCountBaselineSetting(
    string SaveFilePath,
    int CharacterSlotIndex,
    uint Baseline,
    bool IsEnabled);
