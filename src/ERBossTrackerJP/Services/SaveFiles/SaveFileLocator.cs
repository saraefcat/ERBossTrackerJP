using System.IO;

namespace ERBossTrackerJP.Services.SaveFiles;

public sealed class SaveFileLocator : ISaveFileLocator
{
    public const string GameDirectoryName = "EldenRing";
    public const string SaveFileName = "ER0000.sl2";
    public const int SteamIdLength = 17;

    private static readonly EnumerationOptions DirectoryEnumerationOptions = new()
    {
        IgnoreInaccessible = true,
        RecurseSubdirectories = false,
        ReturnSpecialDirectories = false,
        AttributesToSkip = 0,
    };

    public SaveFileLocator(string? applicationDataPath = null)
    {
        string appDataPath = applicationDataPath ??
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        if (!string.IsNullOrWhiteSpace(appDataPath))
        {
            DefaultSearchRoot = Path.Combine(
                Path.GetFullPath(appDataPath),
                GameDirectoryName);
        }
    }

    public string? DefaultSearchRoot { get; }

    public IReadOnlyList<SaveFileCandidate> FindDefaultCandidates()
    {
        if (DefaultSearchRoot is null || !Directory.Exists(DefaultSearchRoot))
        {
            return Array.Empty<SaveFileCandidate>();
        }

        return FindCandidatesInSearchRoot(DefaultSearchRoot);
    }

    public IReadOnlyList<SaveFileCandidate> FindCandidatesInFolder(string folderPath)
    {
        string fullPath = ResolveFolderPath(folderPath);

        if (!Directory.Exists(fullPath))
        {
            throw new DirectoryNotFoundException(
                $"The selected save folder was not found: {fullPath}");
        }

        string nestedGameDirectory = Path.Combine(fullPath, GameDirectoryName);

        if (!Path.GetFileName(fullPath).Equals(
                GameDirectoryName,
                StringComparison.OrdinalIgnoreCase) &&
            Directory.Exists(nestedGameDirectory))
        {
            var candidates = new Dictionary<string, SaveFileCandidate>(
                StringComparer.OrdinalIgnoreCase);
            TryAddCandidate(fullPath, candidates);
            AddCandidatesFromSearchRoot(nestedGameDirectory, candidates);
            return SortCandidates(candidates.Values);
        }

        return FindCandidatesInSearchRoot(fullPath);
    }

    private static IReadOnlyList<SaveFileCandidate> FindCandidatesInSearchRoot(
        string searchRoot)
    {
        var candidates = new Dictionary<string, SaveFileCandidate>(
            StringComparer.OrdinalIgnoreCase);
        AddCandidatesFromSearchRoot(searchRoot, candidates);
        return SortCandidates(candidates.Values);
    }

    private static void AddCandidatesFromSearchRoot(
        string searchRoot,
        IDictionary<string, SaveFileCandidate> candidates)
    {
        TryAddCandidate(searchRoot, candidates);

        foreach (string directory in Directory.EnumerateDirectories(
                     searchRoot,
                     "*",
                     DirectoryEnumerationOptions))
        {
            TryAddCandidate(directory, candidates);
        }
    }

    private static void TryAddCandidate(
        string directory,
        IDictionary<string, SaveFileCandidate> candidates)
    {
        try
        {
            string filePath = Path.Combine(directory, SaveFileName);
            var file = new FileInfo(filePath);
            file.Refresh();

            if (!file.Exists || file.DirectoryName is null)
            {
                return;
            }

            string saveDirectoryPath = Path.GetFullPath(file.DirectoryName);
            string directoryName = Path.GetFileName(
                Path.TrimEndingDirectorySeparator(saveDirectoryPath));
            string? steamId = IsSteamId(directoryName) ? directoryName : null;
            string fullFilePath = Path.GetFullPath(file.FullName);

            candidates[fullFilePath] = new SaveFileCandidate(
                fullFilePath,
                saveDirectoryPath,
                steamId,
                file.Length,
                new DateTimeOffset(file.LastWriteTimeUtc));
        }
        catch (FileNotFoundException)
        {
            // A save can be replaced between directory enumeration and metadata access.
        }
        catch (DirectoryNotFoundException)
        {
            // A profile directory can disappear while candidates are being collected.
        }
    }

    private static IReadOnlyList<SaveFileCandidate> SortCandidates(
        IEnumerable<SaveFileCandidate> candidates)
    {
        SaveFileCandidate[] sorted = candidates
            .OrderByDescending(candidate => candidate.LastWriteTimeUtc)
            .ThenBy(candidate => candidate.FilePath, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return Array.AsReadOnly(sorted);
    }

    private static bool IsSteamId(string value) =>
        value.Length == SteamIdLength && value.All(char.IsAsciiDigit);

    private static string ResolveFolderPath(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath))
        {
            throw new ArgumentException(
                "The selected save folder path is empty.",
                nameof(folderPath));
        }

        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(folderPath));
    }
}
