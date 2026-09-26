using ERBossTrackerJP.DataGenerator.Models;

namespace ERBossTrackerJP.DataGenerator.Generation;

public sealed record GenerationResult(
    GeneratedBossDocument BossDocument,
    ComparisonReport ComparisonReport);
