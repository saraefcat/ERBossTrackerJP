using System.IO;

namespace ERBossTrackerJP.Services.SaveFiles;

public sealed class SaveFileSnapshotReader : ISaveFileSnapshotReader
{
    public const int DefaultMaximumAttempts = 3;
    public const int DefaultMaximumFileSize = 128 * 1024 * 1024;
    public const int FileStreamBufferSize = 64 * 1024;

    public static readonly TimeSpan DefaultRetryDelay = TimeSpan.FromMilliseconds(100);

    private readonly int _maximumAttempts;
    private readonly TimeSpan _retryDelay;
    private readonly int _maximumFileSize;
    private readonly TimeProvider _timeProvider;

    public SaveFileSnapshotReader(
        int maximumAttempts = DefaultMaximumAttempts,
        TimeSpan? retryDelay = null,
        int maximumFileSize = DefaultMaximumFileSize,
        TimeProvider? timeProvider = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumAttempts, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumFileSize, 1);

        TimeSpan effectiveRetryDelay = retryDelay ?? DefaultRetryDelay;

        if (effectiveRetryDelay < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(retryDelay),
                effectiveRetryDelay,
                "Retry delay cannot be negative.");
        }

        _maximumAttempts = maximumAttempts;
        _retryDelay = effectiveRetryDelay;
        _maximumFileSize = maximumFileSize;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<SaveFileSnapshot> ReadAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        string fullPath = ResolveFullPath(filePath);
        Exception? lastIoException = null;
        bool lastAttemptObservedChange = false;

        for (int attempt = 1; attempt <= _maximumAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lastIoException = null;
            lastAttemptObservedChange = false;

            try
            {
                SaveFileSnapshot? snapshot = await ReadOnceAsync(
                    fullPath,
                    attempt,
                    cancellationToken);

                if (snapshot is not null)
                {
                    return snapshot;
                }

                lastAttemptObservedChange = true;
            }
            catch (FileNotFoundException exception)
            {
                throw CreateException(
                    SaveFileReadErrorCode.FileNotFound,
                    fullPath,
                    "The save file was not found.",
                    attempt,
                    exception);
            }
            catch (DirectoryNotFoundException exception)
            {
                throw CreateException(
                    SaveFileReadErrorCode.FileNotFound,
                    fullPath,
                    "The save file directory was not found.",
                    attempt,
                    exception);
            }
            catch (UnauthorizedAccessException exception)
            {
                throw CreateException(
                    SaveFileReadErrorCode.AccessDenied,
                    fullPath,
                    "Access to the save file was denied.",
                    attempt,
                    exception);
            }
            catch (IOException exception)
            {
                lastIoException = exception;
            }

            if (attempt == _maximumAttempts)
            {
                break;
            }

            await Task.Delay(_retryDelay, _timeProvider, cancellationToken);
        }

        if (lastAttemptObservedChange)
        {
            throw CreateException(
                SaveFileReadErrorCode.ChangedDuringRead,
                fullPath,
                "The save file changed during every read attempt.",
                _maximumAttempts);
        }

        throw CreateException(
            SaveFileReadErrorCode.TemporarilyUnavailable,
            fullPath,
            "The save file could not be read after the retry limit was reached.",
            _maximumAttempts,
            lastIoException);
    }

    private async Task<SaveFileSnapshot?> ReadOnceAsync(
        string fullPath,
        int attempt,
        CancellationToken cancellationToken)
    {
        var options = new FileStreamOptions
        {
            Mode = FileMode.Open,
            Access = FileAccess.Read,
            Share = FileShare.ReadWrite | FileShare.Delete,
            Options = FileOptions.Asynchronous | FileOptions.SequentialScan,
            BufferSize = FileStreamBufferSize,
        };

        await using var stream = new FileStream(fullPath, options);
        long lengthBeforeRead = stream.Length;

        if (lengthBeforeRead > _maximumFileSize)
        {
            throw CreateException(
                SaveFileReadErrorCode.FileTooLarge,
                fullPath,
                $"The save file contains {lengthBeforeRead} bytes; the configured maximum is {_maximumFileSize} bytes.",
                attempt);
        }

        DateTime lastWriteBeforeRead = File.GetLastWriteTimeUtc(stream.SafeFileHandle);
        byte[] bytes = GC.AllocateUninitializedArray<byte>((int)lengthBeforeRead);
        await stream.ReadExactlyAsync(bytes, cancellationToken);
        long lengthAfterRead = stream.Length;
        DateTime lastWriteAfterRead = File.GetLastWriteTimeUtc(stream.SafeFileHandle);

        if (lengthBeforeRead != lengthAfterRead ||
            lastWriteBeforeRead != lastWriteAfterRead)
        {
            return null;
        }

        return new SaveFileSnapshot(
            fullPath,
            bytes,
            new DateTimeOffset(lastWriteAfterRead));
    }

    private static string ResolveFullPath(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw CreateException(
                SaveFileReadErrorCode.InvalidPath,
                filePath ?? string.Empty,
                "The save file path is empty.",
                0);
        }

        try
        {
            return Path.GetFullPath(filePath);
        }
        catch (Exception exception) when (
            exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            throw CreateException(
                SaveFileReadErrorCode.InvalidPath,
                filePath,
                "The save file path is invalid.",
                0,
                exception);
        }
    }

    private static SaveFileReadException CreateException(
        SaveFileReadErrorCode errorCode,
        string filePath,
        string message,
        int attemptCount,
        Exception? innerException = null) =>
        new(errorCode, filePath, message, attemptCount, innerException);
}
