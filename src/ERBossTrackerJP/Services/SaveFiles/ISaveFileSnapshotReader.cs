namespace ERBossTrackerJP.Services.SaveFiles;

public interface ISaveFileSnapshotReader
{
    Task<SaveFileSnapshot> ReadAsync(
        string filePath,
        CancellationToken cancellationToken = default);
}
