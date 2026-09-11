using ERBossTrackerJP.Core.Models;

namespace ERBossTrackerJP.Services.Settings;

public sealed record UserSettings(
    string? SaveFilePath = null,
    int? CharacterSlotIndex = null,
    DisplayLanguage DisplayLanguage = DisplayLanguage.Japanese,
    bool IsAutoMonitoringEnabled = true)
{
    public static UserSettings Default { get; } = new();
}
