using System.Diagnostics;
using ERBossTrackerJP.Services.Diagnostics;

namespace ERBossTrackerJP.Tests.Services.Diagnostics;

public sealed class DiagnosticLogServiceTests
{
    [Fact]
    public void TryStart_WritesTraceMessagesToUtf8Log()
    {
        string testDirectory = CreateTestDirectory();
        string logPath = Path.Combine(testDirectory, "diagnostic.log");
        const string marker = "diagnostic-marker-日本語";

        try
        {
            var service = new DiagnosticLogService(logPath);
            Assert.True(service.TryStart());

            Trace.WriteLine(marker);
            Trace.Flush();
            service.Dispose();

            byte[] bytes = File.ReadAllBytes(logPath);
            string log = File.ReadAllText(logPath);
            Assert.False(bytes.AsSpan().StartsWith(
                new byte[] { 0xEF, 0xBB, 0xBF }));
            Assert.Matches(
                @"^\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{3} [+-]\d{2}:\d{2}",
                log);
            Assert.Contains(marker, log, StringComparison.Ordinal);
            Assert.Contains("Logging started", log, StringComparison.Ordinal);
            Assert.Contains("Logging stopped", log, StringComparison.Ordinal);
        }
        finally
        {
            DeleteTestDirectory(testDirectory);
        }
    }

    [Fact]
    public void WriteLine_RotatesBySizeAndLimitsArchiveCount()
    {
        string testDirectory = CreateTestDirectory();
        string logPath = Path.Combine(testDirectory, "rolling.log");

        try
        {
            using (var listener = new RollingFileTraceListener(
                       logPath,
                       maximumFileSizeBytes: 180,
                       archiveFileCount: 2))
            {
                for (int index = 0; index < 12; index++)
                {
                    listener.WriteLine($"entry-{index:D2}-{new string('x', 80)}");
                }
            }

            Assert.True(File.Exists(logPath));
            Assert.True(File.Exists($"{logPath}.1"));
            Assert.True(File.Exists($"{logPath}.2"));
            Assert.False(File.Exists($"{logPath}.3"));

            string currentLog = File.ReadAllText(logPath);
            string retainedLogs = currentLog +
                                  File.ReadAllText($"{logPath}.1") +
                                  File.ReadAllText($"{logPath}.2");
            Assert.Contains("entry-11", currentLog, StringComparison.Ordinal);
            Assert.DoesNotContain("entry-00", retainedLogs, StringComparison.Ordinal);
        }
        finally
        {
            DeleteTestDirectory(testDirectory);
        }
    }

    [Fact]
    public void TryStart_UnwritableShapeReturnsFalseWithoutThrowing()
    {
        string testDirectory = CreateTestDirectory();
        string blockingFile = Path.Combine(testDirectory, "not-a-directory");
        File.WriteAllText(blockingFile, "block");
        string logPath = Path.Combine(blockingFile, "diagnostic.log");

        try
        {
            using var service = new DiagnosticLogService(logPath);

            bool started = service.TryStart();

            Assert.False(started);
            Assert.False(service.IsEnabled);
        }
        finally
        {
            DeleteTestDirectory(testDirectory);
        }
    }

    private static string CreateTestDirectory()
    {
        string directoryPath = Path.Combine(
            Path.GetTempPath(),
            "ERBossTrackerJP.Tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directoryPath);
        return directoryPath;
    }

    private static void DeleteTestDirectory(string directoryPath)
    {
        string fullPath = Path.GetFullPath(directoryPath);
        string testRoot = Path.GetFullPath(Path.Combine(
            Path.GetTempPath(),
            "ERBossTrackerJP.Tests"));

        if (!fullPath.StartsWith(
                testRoot + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Refusing to delete a non-test directory.");
        }

        if (Directory.Exists(fullPath))
        {
            Directory.Delete(fullPath, recursive: true);
        }
    }
}
