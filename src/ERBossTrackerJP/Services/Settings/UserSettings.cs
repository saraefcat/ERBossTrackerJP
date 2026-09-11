using ERBossTrackerJP.Core.Models;
using ERBossTrackerJP.Services.Theming;

namespace ERBossTrackerJP.Services.Settings;

public sealed record UserSettings(
    string? SaveFilePath = null,
    int? CharacterSlotIndex = null,
    DisplayLanguage DisplayLanguage = DisplayLanguage.Japanese,
    bool IsAutoMonitoringEnabled = true,
    ApplicationTheme Theme = ApplicationTheme.Dark)
{
    public static UserSettings Default { get; } = new();
}
