using System.Diagnostics;
using System.IO;

namespace ERBossTrackerJP.Services.Monitoring;

public sealed class SaveFileMonitor : ISaveFileMonitor
{
    public static readonly TimeSpan DefaultDebounceInterval =
        TimeSpan.FromMilliseconds(750);
    public static readonly TimeSpan DefaultPollingInterval =
        TimeSpan.FromSeconds(2);

    private readonly object _syncRoot = new();
    private readonly TimeSpan _debounceInterval;
    private readonly TimeSpan _pollingInterval;
    private readonly bool _enableFileSystemWatcher;
    private readonly bool _enablePolling;
    private FileSystemWatcher? _watcher;
    private Timer? _pollTimer;
    private Timer? _debounceTimer;
    private string? _filePath;
    private SaveFileStamp? _baselineStamp;
    private SaveFileStamp? _pendingStamp;
    private bool _disposed;

    public SaveFileMonitor(
        TimeSpan? debounceInterval = null,
        TimeSpan? pollingInterval = null)
        : this(
            debounceInterval ?? DefaultDebounceInterval,
            pollingInterval ?? DefaultPollingInterval,
            enableFileSystemWatcher: true,
            enablePolling: true)
    {
    }

    internal SaveFileMonitor(
        TimeSpan debounceInterval,
        TimeSpan pollingInterval,
        bool enableFileSystemWatcher,
        bool enablePolling)
    {
        if (debounceInterval < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(debounceInterval),
                debounceInterval,
                "Debounce interval cannot be negative.");
        }

        if (pollingInterval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(pollingInterval),
                pollingInterval,
                "Polling interval must be positive.");
        }

        _debounceInterval = debounceInterval;
        _pollingInterval = pollingInterval;
        _enableFileSystemWatcher = enableFileSystemWatcher;
        _enablePolling = enablePolling;
    }

    public event EventHandler<SaveFileChangedEventArgs>? ChangeDetected;

    public event EventHandler<SaveFileMonitorErrorEventArgs>? MonitoringError;

    public bool IsRunning
    {
        get
        {
            lock (_syncRoot)
            {
                return _filePath is not null;
            }
        }
    }

    public string? MonitoredFilePath
    {
        get
        {
            lock (_syncRoot)
            {
                return _filePath;
            }
        }
    }

    public void Start(
        string filePath,
        long fileSize,
        DateTimeOffset lastWriteTimeUtc)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentOutOfRangeException.ThrowIfNegative(fileSize);
        string fullPath = ResolveFullPath(filePath);
        string? directoryPath = Path.GetDirectoryName(fullPath);
        string fileName = Path.GetFileName(fullPath);

        if (string.IsNullOrEmpty(directoryPath) || string.IsNullOrEmpty(fileName))
        {
            throw new ArgumentException(
                "The monitored save file path must include a directory and file name.",
                nameof(filePath));
        }

        lock (_syncRoot)
        {
            StopCore();
            _filePath = fullPath;
            _baselineStamp = new SaveFileStamp(
                fileSize,
                lastWriteTimeUtc.UtcDateTime);
            _pendingStamp = null;

            if (_enableFileSystemWatcher)
            {
                _watcher = CreateWatcher(directoryPath, fileName);
                _watcher.EnableRaisingEvents = true;
            }

            if (_enablePolling)
            {
                _pollTimer = new Timer(
                    static state => ((SaveFileMonitor)state!).ObserveFileAndSchedule(),
                    this,
                    _pollingInterval,
                    _pollingInterval);
            }
        }

        Trace.WriteLine($"[SaveFileMonitor] Started: {fullPath}");
    }

    public void UpdateBaseline(
        string filePath,
        long fileSize,
        DateTimeOffset lastWriteTimeUtc)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentOutOfRangeException.ThrowIfNegative(fileSize);
        string fullPath = ResolveFullPath(filePath);

        lock (_syncRoot)
        {
            if (_filePath is null ||
                !string.Equals(_filePath, fullPath, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "The supplied path is not currently being monitored.");
            }

            _baselineStamp = new SaveFileStamp(
                fileSize,
                lastWriteTimeUtc.UtcDateTime);
            _pendingStamp = null;
            _debounceTimer?.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        }
    }

    public void Stop()
    {
        lock (_syncRoot)
        {
            StopCore();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        Stop();
        _disposed = true;
    }

    internal void NotifyFileSystemChange() => ObserveFileAndSchedule();

    private FileSystemWatcher CreateWatcher(string directoryPath, string fileName)
    {
        var watcher = new FileSystemWatcher(directoryPath, fileName)
        {
            IncludeSubdirectories = false,
            NotifyFilter = NotifyFilters.LastWrite |
                           NotifyFilters.Size |
                           NotifyFilters.FileName |
                           NotifyFilters.CreationTime,
        };
        watcher.Changed += OnFileSystemChanged;
        watcher.Created += OnFileSystemChanged;
        watcher.Renamed += OnFileSystemRenamed;
        watcher.Deleted += OnFileSystemDeleted;
        watcher.Error += OnWatcherError;
        return watcher;
    }

    private void OnFileSystemChanged(object sender, FileSystemEventArgs e) =>
        ObserveFileAndSchedule();

    private void OnFileSystemRenamed(object sender, RenamedEventArgs e) =>
        ObserveFileAndSchedule();

    private void OnFileSystemDeleted(object sender, FileSystemEventArgs e)
    {
        lock (_syncRoot)
        {
            _baselineStamp = null;
            _pendingStamp = null;
            _debounceTimer?.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        }
    }

    private void OnWatcherError(object sender, ErrorEventArgs e)
    {
        Trace.WriteLine($"[SaveFileMonitor] Watcher error: {e.GetException()}");
        MonitoringError?.Invoke(
            this,
            new SaveFileMonitorErrorEventArgs(e.GetException()));
    }

    private void ObserveFileAndSchedule()
    {
        string? filePath;

        lock (_syncRoot)
        {
            filePath = _filePath;
        }

        if (filePath is null)
        {
            return;
        }

        SaveFileStamp stamp;

        try
        {
            var file = new FileInfo(filePath);
            file.Refresh();

            if (!file.Exists)
            {
                return;
            }

            stamp = new SaveFileStamp(file.Length, file.LastWriteTimeUtc);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            Trace.WriteLine($"[SaveFileMonitor] Probe skipped: {exception.Message}");
            return;
        }

        lock (_syncRoot)
        {
            if (_filePath is null ||
                !string.Equals(_filePath, filePath, StringComparison.OrdinalIgnoreCase) ||
                _baselineStamp == stamp ||
                _pendingStamp == stamp)
            {
                return;
            }

            _pendingStamp = stamp;
            _debounceTimer ??= new Timer(
                static state => ((SaveFileMonitor)state!).RaiseChangeDetected(),
                this,
                Timeout.InfiniteTimeSpan,
                Timeout.InfiniteTimeSpan);
            _debounceTimer.Change(_debounceInterval, Timeout.InfiniteTimeSpan);
        }
    }

    private void RaiseChangeDetected()
    {
        string? filePath;
        SaveFileStamp? stamp;

        lock (_syncRoot)
        {
            filePath = _filePath;
            stamp = _pendingStamp;
            _pendingStamp = null;
        }

        if (filePath is null || stamp is null)
        {
            return;
        }

        Trace.WriteLine($"[SaveFileMonitor] Change detected: {filePath}");
        ChangeDetected?.Invoke(
            this,
            new SaveFileChangedEventArgs(
                filePath,
                stamp.Value.FileSize,
                new DateTimeOffset(stamp.Value.LastWriteTimeUtc)));
    }

    private void StopCore()
    {
        if (_watcher is not null)
        {
            _watcher.EnableRaisingEvents = false;
            _watcher.Changed -= OnFileSystemChanged;
            _watcher.Created -= OnFileSystemChanged;
            _watcher.Renamed -= OnFileSystemRenamed;
            _watcher.Deleted -= OnFileSystemDeleted;
            _watcher.Error -= OnWatcherError;
            _watcher.Dispose();
            _watcher = null;
        }

        _pollTimer?.Dispose();
        _pollTimer = null;
        _debounceTimer?.Dispose();
        _debounceTimer = null;
        _filePath = null;
        _baselineStamp = null;
        _pendingStamp = null;
    }

    private static string ResolveFullPath(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        return Path.GetFullPath(filePath);
    }

    private readonly record struct SaveFileStamp(
        long FileSize,
        DateTime LastWriteTimeUtc);
}
