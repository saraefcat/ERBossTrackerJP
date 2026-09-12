using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using ERBossTrackerJP.Core.Models;
using ERBossTrackerJP.Core.Outputs;
using ERBossTrackerJP.Services.Settings;

namespace ERBossTrackerJP.Services.Outputs;

public sealed class ObsTextFileOutput : IObsTextFileOutput
{
    public const string ProgressFileName = "progress.txt";
    public const string DeathsFileName = "deaths.txt";
    public const string DefeatedFileName = "defeated.txt";
    public const string RemainingFileName = "remaining.txt";
    public const string TotalFileName = "total.txt";
    public const string PercentageFileName = "percentage.txt";
    public const string LatestBossFileName = "latest_boss.txt";
    public const string SnapshotFileName = "snapshot.json";

    private static readonly Encoding Utf8WithoutBom = new UTF8Encoding(false);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    private readonly object _configurationLock = new();
    private readonly SemaphoreSlim _publishLock = new(1, 1);
    private string _outputDirectory;
    private string _progressFormat;
    private IReadOnlyList<string> _latestBossIds = [];

    public ObsTextFileOutput(
        string? outputDirectory = null,
        string? progressFormat = null)
    {
        DefaultOutputDirectory = GetDefaultOutputDirectory();
        _outputDirectory = DefaultOutputDirectory;
        _progressFormat = DefaultProgressFormat;

        if (outputDirectory is not null && !TrySetOutputDirectory(outputDirectory))
        {
            throw new ArgumentException(
                "OBS output directory must be a fully qualified path.",
                nameof(outputDirectory));
        }

        if (progressFormat is not null && !TrySetProgressFormat(progressFormat))
        {
            throw new ArgumentException(
                "OBS progress format is invalid.",
                nameof(progressFormat));
        }
    }

    public string DefaultOutputDirectory { get; }

    public string DefaultProgressFormat => ObsProgressTextFormatter.DefaultFormat;

    public string OutputDirectory
    {
        get
        {
            lock (_configurationLock)
            {
                return _outputDirectory;
            }
        }
    }

    public string ProgressFormat
    {
        get
        {
            lock (_configurationLock)
            {
                return _progressFormat;
            }
        }
    }

    public bool TrySetOutputDirectory(string outputDirectory)
    {
        if (string.IsNullOrWhiteSpace(outputDirectory) ||
            outputDirectory.Length > short.MaxValue ||
            !Path.IsPathFullyQualified(outputDirectory))
        {
            return false;
        }

        try
        {
            string fullPath = Path.GetFullPath(outputDirectory.Trim());

            lock (_configurationLock)
            {
                _outputDirectory = fullPath;
            }

            return true;
        }
        catch (Exception exception) when (
            exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }
    }

    public bool TrySetProgressFormat(string progressFormat)
    {
        if (!ObsProgressTextFormatter.TryValidate(
                progressFormat,
                out _))
        {
            return false;
        }

        lock (_configurationLock)
        {
            _progressFormat = progressFormat;
        }

        return true;
    }

    public async ValueTask PublishAsync(
        TrackerOutputUpdate update,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(update);
        (string outputDirectory, string progressFormat) = GetConfiguration();
        await _publishLock.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            TrackerSnapshot snapshot = update.Snapshot;
            TrackerSnapshot? previousSnapshot = IsSameCharacter(
                snapshot,
                update.PreviousSnapshot)
                    ? update.PreviousSnapshot
                    : null;
            IReadOnlyList<string> nextLatestBossIds = GetNextLatestBossIds(
                snapshot,
                previousSnapshot);
            IReadOnlyList<BossProgress> latestBosses = snapshot.Bosses
                .Where(progress => nextLatestBossIds.Contains(
                    progress.Boss.Id,
                    StringComparer.Ordinal))
                .ToArray();
            string percentage = snapshot.ProgressPercentage.ToString(
                "F1",
                CultureInfo.InvariantCulture) + "%";
            IReadOnlyDictionary<string, string> files = CreateOutputFiles(
                update,
                latestBosses,
                percentage,
                progressFormat);

            // Keep the detected transition even when one of the output files is
            // temporarily unavailable. A later "re-output current values" call
            // compares the same snapshot to itself and therefore relies on this
            // state to restore latest_boss.txt.
            _latestBossIds = nextLatestBossIds.ToArray();
            await WriteFilesAtomicallyAsync(
                outputDirectory,
                files,
                cancellationToken).ConfigureAwait(false);
            System.Diagnostics.Trace.WriteLine(
                $"[ObsTextFileOutput] Published: {outputDirectory}; " +
                $"Defeated={snapshot.Defeated}; Total={snapshot.Total}; " +
                $"Deaths={snapshot.CumulativeDeathCount}; " +
                $"LatestBosses={latestBosses.Count}");
        }
        finally
        {
            _publishLock.Release();
        }
    }

    private IReadOnlyList<string> GetNextLatestBossIds(
        TrackerSnapshot snapshot,
        TrackerSnapshot? previousSnapshot)
    {
        if (previousSnapshot is null)
        {
            return [];
        }

        HashSet<string> previouslyDefeated = previousSnapshot.Bosses
            .Where(progress => progress.IsDefeated)
            .Select(progress => progress.Boss.Id)
            .ToHashSet(StringComparer.Ordinal);
        string[] newlyDefeated = snapshot.Bosses
            .Where(progress =>
                progress.IsDefeated &&
                !previouslyDefeated.Contains(progress.Boss.Id))
            .Select(progress => progress.Boss.Id)
            .ToArray();

        return newlyDefeated.Length > 0
            ? newlyDefeated
            : _latestBossIds;
    }

    private static IReadOnlyDictionary<string, string> CreateOutputFiles(
        TrackerOutputUpdate update,
        IReadOnlyList<BossProgress> latestBosses,
        string percentage,
        string progressFormat)
    {
        TrackerSnapshot snapshot = update.Snapshot;
        string[] latestBossNames = latestBosses
            .Select(progress => GetBossName(progress.Boss, update.DisplayLanguage))
            .ToArray();
        var document = new ObsSnapshotDocument(
            SchemaVersion: 2,
            UpdatedAt: snapshot.UpdatedAt,
            DisplayLanguage: update.DisplayLanguage == DisplayLanguage.English
                ? "en"
                : "ja",
            Character: new ObsCharacterDocument(
                snapshot.Character.SlotNumber,
                snapshot.Character.Name,
                snapshot.Character.Level),
            Defeated: snapshot.Defeated,
            Remaining: snapshot.Remaining,
            Total: snapshot.Total,
            ProgressPercentage: snapshot.ProgressPercentage,
            SaveDeathCount: snapshot.SaveDeathCount,
            DeathCountOffset: snapshot.DeathCountOffset,
            CumulativeDeathCount: snapshot.CumulativeDeathCount,
            LatestBosses: latestBosses
                .Select((progress, index) => new ObsLatestBossDocument(
                    progress.Boss.Id,
                    latestBossNames[index]))
                .ToArray(),
            Bosses: snapshot.Bosses
                .Select(progress => new ObsBossDocument(
                    progress.Boss.Id,
                    progress.Boss.FlagId,
                    GetBossName(progress.Boss, update.DisplayLanguage),
                    GetRegionName(progress.Boss, update.DisplayLanguage),
                    progress.IsDefeated))
                .ToArray());

        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [ProgressFileName] = ObsProgressTextFormatter.Format(
                progressFormat,
                snapshot.Defeated,
                snapshot.Remaining,
                snapshot.Total,
                snapshot.ProgressPercentage),
            [DeathsFileName] = snapshot.CumulativeDeathCount.ToString(
                "N0",
                CultureInfo.InvariantCulture),
            [DefeatedFileName] = snapshot.Defeated.ToString(CultureInfo.InvariantCulture),
            [RemainingFileName] = snapshot.Remaining.ToString(CultureInfo.InvariantCulture),
            [TotalFileName] = snapshot.Total.ToString(CultureInfo.InvariantCulture),
            [PercentageFileName] = percentage,
            [LatestBossFileName] = string.Join(Environment.NewLine, latestBossNames),
            [SnapshotFileName] = JsonSerializer.Serialize(document, JsonOptions),
        };
    }

    private (string OutputDirectory, string ProgressFormat) GetConfiguration()
    {
        lock (_configurationLock)
        {
            return (_outputDirectory, _progressFormat);
        }
    }

    private static async Task WriteFilesAtomicallyAsync(
        string outputDirectory,
        IReadOnlyDictionary<string, string> files,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(outputDirectory);
        var stagedFiles = new List<(string TemporaryPath, string TargetPath)>();

        try
        {
            foreach ((string fileName, string contents) in files)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string targetPath = Path.Combine(outputDirectory, fileName);
                string temporaryPath = Path.Combine(
                    outputDirectory,
                    $".{fileName}.{Guid.NewGuid():N}.tmp");
                await File.WriteAllTextAsync(
                    temporaryPath,
                    contents,
                    Utf8WithoutBom,
                    cancellationToken).ConfigureAwait(false);
                stagedFiles.Add((temporaryPath, targetPath));
            }

            foreach ((string temporaryPath, string targetPath) in stagedFiles)
            {
                await MoveWithRetryAsync(
                    temporaryPath,
                    targetPath,
                    cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            foreach ((string temporaryPath, _) in stagedFiles)
            {
                try
                {
                    File.Delete(temporaryPath);
                }
                catch (Exception exception) when (
                    exception is IOException or UnauthorizedAccessException)
                {
                    System.Diagnostics.Trace.WriteLine(
                        $"[ObsTextFileOutput] Temporary file cleanup failed: {exception}");
                }
            }
        }
    }

    private static async Task MoveWithRetryAsync(
        string temporaryPath,
        string targetPath,
        CancellationToken cancellationToken)
    {
        const int maximumAttempts = 3;

        for (int attempt = 1; ; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                File.Move(temporaryPath, targetPath, overwrite: true);
                return;
            }
            catch (Exception exception) when (
                attempt < maximumAttempts &&
                exception is IOException or UnauthorizedAccessException)
            {
                await Task.Delay(
                    TimeSpan.FromMilliseconds(50 * attempt),
                    cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static bool IsSameCharacter(
        TrackerSnapshot snapshot,
        TrackerSnapshot? previousSnapshot) =>
        previousSnapshot?.Character.SlotIndex == snapshot.Character.SlotIndex;

    private static string GetBossName(
        BossDefinition boss,
        DisplayLanguage language) => language == DisplayLanguage.English
            ? boss.NameEn
            : boss.NameJa;

    private static string GetRegionName(
        BossDefinition boss,
        DisplayLanguage language) => language == DisplayLanguage.English
            ? boss.RegionEn
            : boss.RegionJa;

    private static string GetDefaultOutputDirectory()
    {
        string localApplicationData = Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData);
        string root = string.IsNullOrWhiteSpace(localApplicationData)
            ? AppContext.BaseDirectory
            : localApplicationData;
        return Path.GetFullPath(Path.Combine(
            root,
            JsonUserSettingsService.ApplicationDirectoryName,
            "OBS"));
    }

    private sealed record ObsSnapshotDocument(
        int SchemaVersion,
        DateTimeOffset UpdatedAt,
        string DisplayLanguage,
        ObsCharacterDocument Character,
        int Defeated,
        int Remaining,
        int Total,
        double ProgressPercentage,
        uint SaveDeathCount,
        uint DeathCountOffset,
        ulong CumulativeDeathCount,
        IReadOnlyList<ObsLatestBossDocument> LatestBosses,
        IReadOnlyList<ObsBossDocument> Bosses);

    private sealed record ObsLatestBossDocument(
        string Id,
        string Name);

    private sealed record ObsCharacterDocument(
        int SlotNumber,
        string Name,
        uint Level);

    private sealed record ObsBossDocument(
        string Id,
        uint FlagId,
        string Name,
        string Region,
        bool IsDefeated);
}
