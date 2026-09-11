using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;

namespace ERBossTrackerJP.Services.Diagnostics;

internal sealed class RollingFileTraceListener : TraceListener
{
    private static readonly Encoding LogEncoding = new UTF8Encoding(
        encoderShouldEmitUTF8Identifier: false);

    private readonly object _syncRoot = new();
    private readonly string _filePath;
    private readonly long _maximumFileSizeBytes;
    private readonly int _archiveFileCount;
    private StreamWriter? _writer;
    private bool _disabled;
    private bool _disposed;

    public RollingFileTraceListener(
        string filePath,
        long maximumFileSizeBytes,
        int archiveFileCount)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumFileSizeBytes, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(archiveFileCount, 1);

        _filePath = Path.GetFullPath(filePath);
        _maximumFileSizeBytes = maximumFileSizeBytes;
        _archiveFileCount = archiveFileCount;
        OpenWriter();
    }

    public override void Write(string? message) => WriteEntry(message, appendNewLine: false);

    public override void WriteLine(string? message) =>
        WriteEntry(message, appendNewLine: true);

    public override void Flush()
    {
        lock (_syncRoot)
        {
            if (_disposed || _disabled)
            {
                return;
            }

            try
            {
                _writer?.Flush();
            }
            catch (Exception exception) when (
                exception is IOException or ObjectDisposedException)
            {
                Disable();
            }
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (!disposing)
        {
            base.Dispose(disposing);
            return;
        }

        lock (_syncRoot)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            DisposeWriter();
        }

        base.Dispose(disposing);
    }

    private void WriteEntry(string? message, bool appendNewLine)
    {
        lock (_syncRoot)
        {
            if (_disposed || _disabled)
            {
                return;
            }

            string timestamp = DateTimeOffset.Now.ToString(
                "yyyy-MM-dd HH:mm:ss.fff zzz",
                CultureInfo.InvariantCulture);
            string entry = $"{timestamp} {message ?? string.Empty}";

            if (appendNewLine)
            {
                entry += Environment.NewLine;
            }

            try
            {
                RotateIfNeeded(LogEncoding.GetByteCount(entry));
                _writer!.Write(entry);
                _writer.Flush();
            }
            catch (Exception exception) when (
                exception is IOException or UnauthorizedAccessException or
                ObjectDisposedException)
            {
                Disable();
            }
        }
    }

    private void RotateIfNeeded(int incomingByteCount)
    {
        long currentLength = _writer?.BaseStream.Length ?? 0;

        if (currentLength == 0 ||
            currentLength + incomingByteCount <= _maximumFileSizeBytes)
        {
            return;
        }

        DisposeWriter();

        for (int index = _archiveFileCount; index >= 1; index--)
        {
            string sourcePath = index == 1
                ? _filePath
                : GetArchivePath(index - 1);
            string destinationPath = GetArchivePath(index);

            if (!File.Exists(sourcePath))
            {
                continue;
            }

            File.Delete(destinationPath);
            File.Move(sourcePath, destinationPath);
        }

        OpenWriter();
    }

    private void OpenWriter()
    {
        string? directoryPath = Path.GetDirectoryName(_filePath);

        if (string.IsNullOrWhiteSpace(directoryPath))
        {
            throw new ArgumentException(
                "The diagnostic log path must include a directory.",
                nameof(_filePath));
        }

        Directory.CreateDirectory(directoryPath);
        var stream = new FileStream(
            _filePath,
            FileMode.Append,
            FileAccess.Write,
            FileShare.ReadWrite | FileShare.Delete);
        _writer = new StreamWriter(stream, LogEncoding)
        {
            AutoFlush = true,
        };
    }

    private string GetArchivePath(int index) => $"{_filePath}.{index}";

    private void Disable()
    {
        _disabled = true;
        DisposeWriter();
    }

    private void DisposeWriter()
    {
        try
        {
            _writer?.Dispose();
        }
        catch (IOException)
        {
            // Diagnostic logging must never stop the application from exiting.
        }
        finally
        {
            _writer = null;
        }
    }
}
