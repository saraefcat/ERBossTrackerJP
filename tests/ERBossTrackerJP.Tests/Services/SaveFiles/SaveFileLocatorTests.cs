using ERBossTrackerJP.Services.SaveFiles;

namespace ERBossTrackerJP.Tests.Services.SaveFiles;

public sealed class SaveFileLocatorTests
{
    [Fact]
    public void FindDefaultCandidates_ReturnsSteamProfilesNewestFirst()
    {
        string appDataPath = CreateTemporaryDirectory();

        try
        {
            string olderPath = CreateSave(
                Path.Combine(appDataPath, "EldenRing", "76561198000000001"),
                [1]);
            string newerPath = CreateSave(
                Path.Combine(appDataPath, "EldenRing", "76561198000000002"),
                [2, 3]);
            File.SetLastWriteTimeUtc(olderPath, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
            File.SetLastWriteTimeUtc(newerPath, new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc));
            var locator = new SaveFileLocator(appDataPath);

            IReadOnlyList<SaveFileCandidate> candidates = locator.FindDefaultCandidates();

            Assert.Equal(Path.Combine(appDataPath, "EldenRing"), locator.DefaultSearchRoot);
            Assert.Collection(
                candidates,
                candidate =>
                {
                    Assert.Equal("76561198000000002", candidate.SteamId);
                    Assert.Equal(Path.GetFullPath(newerPath), candidate.FilePath);
                    Assert.Equal(2, candidate.FileSize);
                },
                candidate =>
                {
                    Assert.Equal("76561198000000001", candidate.SteamId);
                    Assert.Equal(Path.GetFullPath(olderPath), candidate.FilePath);
                    Assert.Equal(1, candidate.FileSize);
                });
        }
        finally
        {
            DeleteTemporaryDirectory(appDataPath);
        }
    }

    [Fact]
    public void FindDefaultCandidates_ReturnsEmptyWhenDefaultFolderDoesNotExist()
    {
        string appDataPath = Path.Combine(
            Path.GetTempPath(),
            $"ERBossTrackerJP-missing-{Guid.NewGuid():N}");
        var locator = new SaveFileLocator(appDataPath);

        IReadOnlyList<SaveFileCandidate> candidates = locator.FindDefaultCandidates();

        Assert.Empty(candidates);
    }

    [Fact]
    public void FindCandidatesInFolder_AcceptsFolderContainingSaveDirectly()
    {
        string selectedFolder = CreateTemporaryDirectory();

        try
        {
            string savePath = CreateSave(selectedFolder, [4, 5, 6]);
            var locator = new SaveFileLocator();

            SaveFileCandidate candidate = Assert.Single(
                locator.FindCandidatesInFolder(selectedFolder));

            Assert.Equal(Path.GetFullPath(savePath), candidate.FilePath);
            Assert.Equal(Path.GetFullPath(selectedFolder), candidate.SaveDirectoryPath);
            Assert.Null(candidate.SteamId);
        }
        finally
        {
            DeleteTemporaryDirectory(selectedFolder);
        }
    }

    [Fact]
    public void FindCandidatesInFolder_AcceptsEldenRingFolderWithSteamProfiles()
    {
        string selectedFolder = CreateTemporaryDirectory();

        try
        {
            CreateSave(Path.Combine(selectedFolder, "76561198000000003"), [7]);
            CreateSave(Path.Combine(selectedFolder, "76561198000000004"), [8]);
            var locator = new SaveFileLocator();

            IReadOnlyList<SaveFileCandidate> candidates =
                locator.FindCandidatesInFolder(selectedFolder);

            Assert.Equal(2, candidates.Count);
            Assert.All(candidates, candidate => Assert.NotNull(candidate.SteamId));
        }
        finally
        {
            DeleteTemporaryDirectory(selectedFolder);
        }
    }

    [Fact]
    public void FindCandidatesInFolder_AcceptsParentOfEldenRingFolder()
    {
        string selectedFolder = CreateTemporaryDirectory();

        try
        {
            string savePath = CreateSave(
                Path.Combine(selectedFolder, "EldenRing", "76561198000000005"),
                [9]);
            var locator = new SaveFileLocator();

            SaveFileCandidate candidate = Assert.Single(
                locator.FindCandidatesInFolder(selectedFolder));

            Assert.Equal(Path.GetFullPath(savePath), candidate.FilePath);
            Assert.Equal("76561198000000005", candidate.SteamId);
        }
        finally
        {
            DeleteTemporaryDirectory(selectedFolder);
        }
    }

    [Fact]
    public void FindCandidatesInFolder_IgnoresUnsupportedSaveExtensions()
    {
        string selectedFolder = CreateTemporaryDirectory();

        try
        {
            File.WriteAllBytes(Path.Combine(selectedFolder, "ER0000.co2"), [1]);
            File.WriteAllBytes(Path.Combine(selectedFolder, "ER0000.sl2.copy"), [2]);
            var locator = new SaveFileLocator();

            IReadOnlyList<SaveFileCandidate> candidates =
                locator.FindCandidatesInFolder(selectedFolder);

            Assert.Empty(candidates);
        }
        finally
        {
            DeleteTemporaryDirectory(selectedFolder);
        }
    }

    [Fact]
    public void FindCandidatesInFolder_ReportsMissingSelectedFolder()
    {
        string selectedFolder = Path.Combine(
            Path.GetTempPath(),
            $"ERBossTrackerJP-missing-{Guid.NewGuid():N}");
        var locator = new SaveFileLocator();

        Assert.Throws<DirectoryNotFoundException>(() =>
            locator.FindCandidatesInFolder(selectedFolder));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void FindCandidatesInFolder_RejectsEmptyPath(string? folderPath)
    {
        var locator = new SaveFileLocator();

        Assert.Throws<ArgumentException>(() =>
            locator.FindCandidatesInFolder(folderPath!));
    }

    private static string CreateTemporaryDirectory()
    {
        string path = Path.Combine(
            Path.GetTempPath(),
            $"ERBossTrackerJP-{Guid.NewGuid():N}");
        return Directory.CreateDirectory(path).FullName;
    }

    private static string CreateSave(string directoryPath, byte[] data)
    {
        Directory.CreateDirectory(directoryPath);
        string path = Path.Combine(directoryPath, SaveFileLocator.SaveFileName);
        File.WriteAllBytes(path, data);
        return path;
    }

    private static void DeleteTemporaryDirectory(string path)
    {
        string fullPath = Path.GetFullPath(path);
        string temporaryRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath()));

        Assert.StartsWith(
            temporaryRoot + Path.DirectorySeparatorChar,
            fullPath,
            StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith(
            "ERBossTrackerJP-",
            Path.GetFileName(fullPath),
            StringComparison.Ordinal);
        Directory.Delete(fullPath, recursive: true);
    }
}
