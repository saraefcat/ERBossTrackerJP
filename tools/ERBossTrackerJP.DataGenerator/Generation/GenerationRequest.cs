namespace ERBossTrackerJP.DataGenerator.Generation;

public sealed record GenerationRequest(
    string SourceBossesPath,
    string MappingPath,
    string NpcNamesPath,
    string PlaceNamesPath,
    string ManifestPath,
    int ExpectedBossCount = OfficialLocalizationGenerator.ProductionBossCount);
