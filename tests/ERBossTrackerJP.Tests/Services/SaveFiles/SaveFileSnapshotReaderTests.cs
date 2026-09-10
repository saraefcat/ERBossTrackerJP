using ERBossTrackerJP.Services.SaveFiles;

namespace ERBossTrackerJP.Tests.Services.SaveFiles;

public sealed class SaveFileSnapshotReaderTests
{
    [Fact]
    public async Task ReadAsync_CreatesMemorySnapshotAndReleasesFile()
    {
        string path = CreateTemporaryPath();

        try
        {
            byte[] original = [1, 2, 3, 4];
            await File.WriteAllBytesAsync(path, original);
            var reader = new SaveFileSnapshotReader(maximumAttempts: 1);

            SaveFileSnapshot snapshot = await reader.ReadAsync(path);
            await File.WriteAllBytesAsync(path, [9, 8]);

            Assert.Equal(Path.GetFullPath(path), snapshot.SourcePath);
            Assert.Equal(original, snapshot.Bytes.ToArray());
            Assert.NotEqual(default, snapshot.LastWriteTimeUtc);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ReadAsync_UsesReadWriteDeleteSharing()
    {
        string path = CreateTemporaryPath();

        try
        {
            await File.WriteAllBytesAsync(path, [1, 2, 3]);
            await using var gameStream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.ReadWrite,
                FileShare.ReadWrite | FileShare.Delete);
            var reader = new SaveFileSnapshotReader(maximumAttempts: 1);

            SaveFileSnapshot snapshot = await reader.ReadAsync(path);

            Assert.Equal([1, 2, 3], snapshot.Bytes.ToArray());
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ReadAsync_ReportsMissingFileWithoutRetrying()
    {
        string path = CreateTemporaryPath();
        var reader = new SaveFileSnapshotReader(
            maximumAttempts: 3,
            retryDelay: TimeSpan.Zero);

        SaveFileReadException exception = await Assert.ThrowsAsync<SaveFileReadException>(() =>
            reader.ReadAsync(path));

        Assert.Equal(SaveFileReadErrorCode.FileNotFound, exception.ErrorCode);
        Assert.Equal(1, exception.AttemptCount);
        Assert.Equal(Path.GetFullPath(path), exception.FilePath);
    }

    [Fact]
    public async Task ReadAsync_ReportsFileLargerThanConfiguredLimit()
    {
        string path = CreateTemporaryPath();

        try
        {
            await File.WriteAllBytesAsync(path, [1, 2, 3, 4, 5]);
            var reader = new SaveFileSnapshotReader(
                maximumAttempts: 1,
                maximumFileSize: 4);

            SaveFileReadException exception = await Assert.ThrowsAsync<SaveFileReadException>(() =>
                reader.ReadAsync(path));

            Assert.Equal(SaveFileReadErrorCode.FileTooLarge, exception.ErrorCode);
            Assert.Equal(1, exception.AttemptCount);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ReadAsync_RetriesTemporarySharingFailure()
    {
        string path = CreateTemporaryPath();

        try
        {
            await File.WriteAllBytesAsync(path, [1, 2, 3]);
            await using var exclusiveStream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.ReadWrite,
                FileShare.None);
            var reader = new SaveFileSnapshotReader(
                maximumAttempts: 2,
                retryDelay: TimeSpan.Zero);

            SaveFileReadException exception = await Assert.ThrowsAsync<SaveFileReadException>(() =>
                reader.ReadAsync(path));

            Assert.Equal(SaveFileReadErrorCode.TemporarilyUnavailable, exception.ErrorCode);
            Assert.Equal(2, exception.AttemptCount);
            Assert.IsType<IOException>(exception.InnerException);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ReadAsync_PreservesCancellation()
    {
        string path = CreateTemporaryPath();

        try
        {
            await File.WriteAllBytesAsync(path, [1]);
            var reader = new SaveFileSnapshotReader();
            using var cancellationSource = new CancellationTokenSource();
            await cancellationSource.CancelAsync();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                reader.ReadAsync(path, cancellationSource.Token));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ReadAsync_RejectsEmptyPath(string? path)
    {
        var reader = new SaveFileSnapshotReader();

        SaveFileReadException exception = await Assert.ThrowsAsync<SaveFileReadException>(() =>
            reader.ReadAsync(path!));

        Assert.Equal(SaveFileReadErrorCode.InvalidPath, exception.ErrorCode);
        Assert.Equal(0, exception.AttemptCount);
    }

    private static string CreateTemporaryPath() =>
        Path.Combine(Path.GetTempPath(), $"ERBossTrackerJP-{Guid.NewGuid():N}.sl2");
}
