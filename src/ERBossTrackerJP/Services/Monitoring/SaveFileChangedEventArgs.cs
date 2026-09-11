namespace ERBossTrackerJP.Services.Monitoring;

public sealed class SaveFileChangedEventArgs(
    string filePath,
    long fileSize,
    DateTimeOffset lastWriteTimeUtc) : EventArgs
{
    public string FilePath { get; } = filePath;

    public long FileSize { get; } = fileSize;

    public DateTimeOffset LastWriteTimeUtc { get; } = lastWriteTimeUtc;
}
