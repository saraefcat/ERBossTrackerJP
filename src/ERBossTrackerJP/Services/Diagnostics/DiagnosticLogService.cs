using System.Diagnostics;
using System.IO;
using ERBossTrackerJP.Services.Settings;

namespace ERBossTrackerJP.Services.Diagnostics;

public sealed class DiagnosticLogService : IDisposable
{
    public const string LogsDirectoryName = "Logs";
    public const string LogFileName = "ERBossTrackerJP.log";
    public const long DefaultMaximumFileSizeBytes = 2 * 1024 * 1024;
    public const int DefaultArchiveFileCount = 3;

    private readonly object _syncRoot = new();
    private readonly long _maximumFileSizeBytes;
    private readonly int _archiveFileCount;
    private RollingFileTraceListener? _listener;
    private bool _disposed;

    public DiagnosticLogService(
        string? logFilePath = null,
        long maximumFileSizeBytes = DefaultMaximumFileSizeBytes,
        int archiveFileCount = DefaultArchiveFileCount)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumFileSizeBytes, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(archiveFileCount, 1);

        LogFilePath = logFilePath is null
            ? ResolveDefaultLogFilePath()
            : Path.GetFullPath(logFilePath);
        _maximumFileSizeBytes = maximumFileSizeBytes;
        _archiveFileCount = archiveFileCount;
    }

    public string? LogFilePath { get; }

    public bool IsEnabled
    {
        get
        {
            lock (_syncRoot)
            {
                return _listener is not null;
            }
        }
    }

    public bool TryStart()
    {
        lock (_syncRoot)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (_listener is not null)
            {
                return true;
            }

            if (LogFilePath is null)
            {
                return false;
            }

            try
            {
                var listener = new RollingFileTraceListener(
                    LogFilePath,
                    _maximumFileSizeBytes,
                    _archiveFileCount)
                {
                    Name = nameof(DiagnosticLogService),
                };
                Trace.Listeners.Add(listener);
                _listener = listener;
                Trace.WriteLine(
                    $"[DiagnosticLogService] Logging started: {LogFilePath}");
                return true;
            }
            catch (Exception exception) when (
                exception is ArgumentException or NotSupportedException or IOException or
                UnauthorizedAccessException)
            {
                Debug.WriteLine(
                    $"[DiagnosticLogService] Logging could not start: {exception}");
                return false;
            }
        }
    }

    public void Dispose()
    {
        lock (_syncRoot)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            if (_listener is null)
            {
                return;
            }

            Trace.WriteLine("[DiagnosticLogService] Logging stopped.");
            Trace.Flush();
            Trace.Listeners.Remove(_listener);
            _listener.Dispose();
            _listener = null;
        }
    }

    private static string? ResolveDefaultLogFilePath()
    {
        try
        {
            string localApplicationData = Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData);

            if (string.IsNullOrWhiteSpace(localApplicationData))
            {
                return null;
            }

            return Path.Combine(
                Path.GetFullPath(localApplicationData),
                JsonUserSettingsService.ApplicationDirectoryName,
                LogsDirectoryName,
                LogFileName);
        }
        catch (Exception exception) when (
            exception is ArgumentException or NotSupportedException or IOException)
        {
            Debug.WriteLine(
                $"[DiagnosticLogService] Default path resolution failed: {exception}");
            return null;
        }
    }
}
