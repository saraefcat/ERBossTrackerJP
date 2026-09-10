namespace ERBossTrackerJP.Services.SaveFiles;

public sealed record SaveFileSnapshot(
    string SourcePath,
    ReadOnlyMemory<byte> Bytes,
    DateTimeOffset LastWriteTimeUtc);
