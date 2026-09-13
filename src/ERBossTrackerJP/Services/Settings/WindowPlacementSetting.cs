namespace ERBossTrackerJP.Services.Settings;

public sealed record WindowPlacementSetting(
    double Left,
    double Top,
    double Width,
    double Height,
    bool IsMaximized);
