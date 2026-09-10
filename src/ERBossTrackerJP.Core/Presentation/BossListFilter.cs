using ERBossTrackerJP.Core.Models;

namespace ERBossTrackerJP.Core.Presentation;

public sealed record BossListFilter(
    DisplayLanguage Language,
    BossCompletionFilter Completion = BossCompletionFilter.All,
    string? RegionId = null,
    GameContent? Content = null,
    string? SearchText = null);
