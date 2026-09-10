namespace ERBossTrackerJP.Services.SaveFiles;

public sealed record SaveFileCandidate(
    string FilePath,
    string SaveDirectoryPath,
    string? SteamId,
    long FileSize,
    DateTimeOffset LastWriteTimeUtc);
