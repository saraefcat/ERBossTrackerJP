namespace ERBossTrackerJP.Services.Settings;

public sealed record DeathCountOffsetSetting(
    string SaveFilePath,
    int CharacterSlotIndex,
    uint Offset);
