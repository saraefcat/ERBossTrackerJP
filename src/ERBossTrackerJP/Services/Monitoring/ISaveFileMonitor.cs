namespace ERBossTrackerJP.Services.Monitoring;

public interface ISaveFileMonitor : IDisposable
{
    event EventHandler<SaveFileChangedEventArgs>? ChangeDetected;

    event EventHandler<SaveFileMonitorErrorEventArgs>? MonitoringError;

    bool IsRunning { get; }

    string? MonitoredFilePath { get; }

    void Start(
        string filePath,
        long fileSize,
        DateTimeOffset lastWriteTimeUtc);

    void UpdateBaseline(
        string filePath,
        long fileSize,
        DateTimeOffset lastWriteTimeUtc);

    void Stop();
}
