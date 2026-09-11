using ERBossTrackerJP.Services.Monitoring;

namespace ERBossTrackerJP.Tests.Services.Monitoring;

public sealed class SaveFileMonitorTests
{
    [Fact]
    public async Task NotifyFileSystemChange_DebouncesBurstAndReportsLatestStamp()
    {
        string path = CreateTemporaryPath();

        try
        {
            await File.WriteAllBytesAsync(path, [0x01]);
            var file = new FileInfo(path);
            var monitor = new SaveFileMonitor(
                TimeSpan.FromMilliseconds(50),
                TimeSpan.FromSeconds(30),
                enableFileSystemWatcher: false,
                enablePolling: false);
            using (monitor)
            {
                var completion = new TaskCompletionSource<SaveFileChangedEventArgs>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                int eventCount = 0;
                monitor.ChangeDetected += (_, eventArgs) =>
                {
                    Interlocked.Increment(ref eventCount);
                    completion.TrySetResult(eventArgs);
                };
                monitor.Start(
                    path,
                    file.Length,
                    new DateTimeOffset(file.LastWriteTimeUtc));

                await File.WriteAllBytesAsync(path, [0x01, 0x02]);
                monitor.NotifyFileSystemChange();
                await File.WriteAllBytesAsync(path, [0x01, 0x02, 0x03]);
                monitor.NotifyFileSystemChange();

                SaveFileChangedEventArgs detected = await completion.Task.WaitAsync(
                    TimeSpan.FromSeconds(2));
                await Task.Delay(125);

                Assert.Equal(Path.GetFullPath(path), detected.FilePath);
                Assert.Equal(3, detected.FileSize);
                Assert.Equal(1, Volatile.Read(ref eventCount));
            }
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Polling_DetectsChangeWithoutFileSystemWatcher()
    {
        string path = CreateTemporaryPath();

        try
        {
            await File.WriteAllBytesAsync(path, [0x01]);
            var file = new FileInfo(path);
            using var monitor = new SaveFileMonitor(
                TimeSpan.FromMilliseconds(10),
                TimeSpan.FromMilliseconds(25),
                enableFileSystemWatcher: false,
                enablePolling: true);
            var completion = new TaskCompletionSource<SaveFileChangedEventArgs>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            monitor.ChangeDetected += (_, eventArgs) =>
                completion.TrySetResult(eventArgs);
            monitor.Start(
                path,
                file.Length,
                new DateTimeOffset(file.LastWriteTimeUtc));

            await File.WriteAllBytesAsync(path, [0x10, 0x20, 0x30, 0x40]);

            SaveFileChangedEventArgs detected = await completion.Task.WaitAsync(
                TimeSpan.FromSeconds(2));
            Assert.Equal(4, detected.FileSize);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Stop_CancelsPendingDebouncedNotification()
    {
        string path = CreateTemporaryPath();

        try
        {
            await File.WriteAllBytesAsync(path, [0x01]);
            var file = new FileInfo(path);
            using var monitor = new SaveFileMonitor(
                TimeSpan.FromMilliseconds(100),
                TimeSpan.FromSeconds(30),
                enableFileSystemWatcher: false,
                enablePolling: false);
            int eventCount = 0;
            monitor.ChangeDetected += (_, _) => Interlocked.Increment(ref eventCount);
            monitor.Start(
                path,
                file.Length,
                new DateTimeOffset(file.LastWriteTimeUtc));
            await File.WriteAllBytesAsync(path, [0x01, 0x02]);
            monitor.NotifyFileSystemChange();

            monitor.Stop();
            await Task.Delay(200);

            Assert.False(monitor.IsRunning);
            Assert.Equal(0, Volatile.Read(ref eventCount));
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static string CreateTemporaryPath() =>
        Path.Combine(Path.GetTempPath(), $"ERBossTrackerJP-monitor-{Guid.NewGuid():N}.sl2");
}
