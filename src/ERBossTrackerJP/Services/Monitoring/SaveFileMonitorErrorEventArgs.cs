namespace ERBossTrackerJP.Services.Monitoring;

public sealed class SaveFileMonitorErrorEventArgs(Exception exception) : EventArgs
{
    public Exception Exception { get; } = exception;
}
