using System.Text.Json;
using ERBossTrackerJP.Core.Models;
using ERBossTrackerJP.Core.Outputs;
using ERBossTrackerJP.Services.Outputs;

namespace ERBossTrackerJP.Tests.Services.Outputs;

public sealed class ObsTextFileOutputTests
{
    [Fact]
    public async Task PublishAsync_FirstSnapshotWritesUtf8FilesWithoutLatestBoss()
    {
        string outputDirectory = CreateTestDirectory();

        try
        {
            var output = new ObsTextFileOutput(outputDirectory);
            TrackerSnapshot snapshot = CreateSnapshot(secondBossDefeated: false);

            await output.PublishAsync(new TrackerOutputUpdate(
                snapshot,
                previousSnapshot: null,
                DisplayLanguage.Japanese));

            Assert.Equal(
                "1 / 2 (50.0%)",
                File.ReadAllText(Path.Combine(
                    outputDirectory,
                    ObsTextFileOutput.ProgressFileName)));
            Assert.Equal(
                "1",
                File.ReadAllText(Path.Combine(
                    outputDirectory,
                    ObsTextFileOutput.DefeatedFileName)));
            Assert.Equal(
                "1",
                File.ReadAllText(Path.Combine(
                    outputDirectory,
                    ObsTextFileOutput.RemainingFileName)));
            Assert.Equal(
                "2",
                File.ReadAllText(Path.Combine(
                    outputDirectory,
                    ObsTextFileOutput.TotalFileName)));
            Assert.Equal(
                "50.0%",
                File.ReadAllText(Path.Combine(
                    outputDirectory,
                    ObsTextFileOutput.PercentageFileName)));
            Assert.Empty(File.ReadAllText(Path.Combine(
                outputDirectory,
                ObsTextFileOutput.LatestBossFileName)));
            byte[] progressBytes = File.ReadAllBytes(Path.Combine(
                outputDirectory,
                ObsTextFileOutput.ProgressFileName));
            Assert.False(progressBytes.AsSpan().StartsWith(
                new byte[] { 0xEF, 0xBB, 0xBF }));

            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(
                Path.Combine(outputDirectory, ObsTextFileOutput.SnapshotFileName)));
            Assert.Equal(1, document.RootElement.GetProperty("schemaVersion").GetInt32());
            Assert.Equal("ja", document.RootElement.GetProperty("displayLanguage").GetString());
            Assert.Equal(2, document.RootElement.GetProperty("bosses").GetArrayLength());
            Assert.Equal(
                "Tarnished",
                document.RootElement
                    .GetProperty("character")
                    .GetProperty("name")
                    .GetString());
            Assert.Empty(Directory.EnumerateFiles(outputDirectory, "*.tmp"));
        }
        finally
        {
            DeleteTestDirectory(outputDirectory);
        }
    }

    [Fact]
    public async Task PublishAsync_DetectsNewDefeatAndRetainsItAcrossLanguageChange()
    {
        string outputDirectory = CreateTestDirectory();

        try
        {
            var output = new ObsTextFileOutput(outputDirectory);
            TrackerSnapshot previous = CreateSnapshot(secondBossDefeated: false);
            TrackerSnapshot current = CreateSnapshot(secondBossDefeated: true);

            await output.PublishAsync(new TrackerOutputUpdate(
                previous,
                previousSnapshot: null,
                DisplayLanguage.Japanese));
            await output.PublishAsync(new TrackerOutputUpdate(
                current,
                previous,
                DisplayLanguage.Japanese));

            Assert.Equal(
                "飛竜アギール",
                File.ReadAllText(Path.Combine(
                    outputDirectory,
                    ObsTextFileOutput.LatestBossFileName)));
            Assert.Equal(
                "2 / 2 (100.0%)",
                File.ReadAllText(Path.Combine(
                    outputDirectory,
                    ObsTextFileOutput.ProgressFileName)));

            await output.PublishAsync(new TrackerOutputUpdate(
                current,
                current,
                DisplayLanguage.English));

            Assert.Equal(
                "Flying Dragon Agheel",
                File.ReadAllText(Path.Combine(
                    outputDirectory,
                    ObsTextFileOutput.LatestBossFileName)));
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(
                Path.Combine(outputDirectory, ObsTextFileOutput.SnapshotFileName)));
            Assert.Equal(
                "Flying Dragon Agheel",
                document.RootElement
                    .GetProperty("latestBosses")[0]
                    .GetProperty("name")
                    .GetString());

            TrackerSnapshot otherCharacter = CreateSnapshot(
                secondBossDefeated: true,
                slotIndex: 1);
            await output.PublishAsync(new TrackerOutputUpdate(
                otherCharacter,
                current,
                DisplayLanguage.Japanese));
            Assert.Empty(File.ReadAllText(Path.Combine(
                outputDirectory,
                ObsTextFileOutput.LatestBossFileName)));
            Assert.Empty(Directory.EnumerateFiles(outputDirectory, "*.tmp"));
        }
        finally
        {
            DeleteTestDirectory(outputDirectory);
        }
    }

    [Fact]
    public async Task PublishAsync_MultipleNewDefeatsAreWrittenAsSeparateLines()
    {
        string outputDirectory = CreateTestDirectory();

        try
        {
            var output = new ObsTextFileOutput(outputDirectory);
            TrackerSnapshot previous = CreateSnapshot(
                secondBossDefeated: false,
                firstBossDefeated: false);
            TrackerSnapshot current = CreateSnapshot(secondBossDefeated: true);

            await output.PublishAsync(new TrackerOutputUpdate(
                previous,
                previousSnapshot: null,
                DisplayLanguage.Japanese));
            await output.PublishAsync(new TrackerOutputUpdate(
                current,
                previous,
                DisplayLanguage.Japanese));

            Assert.Equal(
                $"ツリーガード{Environment.NewLine}飛竜アギール",
                File.ReadAllText(Path.Combine(
                    outputDirectory,
                    ObsTextFileOutput.LatestBossFileName)));
        }
        finally
        {
            DeleteTestDirectory(outputDirectory);
        }
    }

    [Fact]
    public async Task PublishAsync_FailedBatchRetainsLatestBossForRetry()
    {
        string outputDirectory = CreateTestDirectory();

        try
        {
            var output = new ObsTextFileOutput(outputDirectory);
            TrackerSnapshot previous = CreateSnapshot(secondBossDefeated: false);
            TrackerSnapshot current = CreateSnapshot(secondBossDefeated: true);
            await output.PublishAsync(new TrackerOutputUpdate(
                previous,
                previousSnapshot: null,
                DisplayLanguage.Japanese));
            string snapshotPath = Path.Combine(
                outputDirectory,
                ObsTextFileOutput.SnapshotFileName);

            await using (var lockedSnapshot = new FileStream(
                             snapshotPath,
                             FileMode.Open,
                             FileAccess.Read,
                             FileShare.Read))
            {
                await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                    output.PublishAsync(new TrackerOutputUpdate(
                        current,
                        previous,
                        DisplayLanguage.Japanese)).AsTask());
            }

            await output.PublishAsync(new TrackerOutputUpdate(
                current,
                current,
                DisplayLanguage.Japanese));

            Assert.Equal(
                "飛竜アギール",
                File.ReadAllText(Path.Combine(
                    outputDirectory,
                    ObsTextFileOutput.LatestBossFileName)));
        }
        finally
        {
            DeleteTestDirectory(outputDirectory);
        }
    }

    [Fact]
    public async Task PublishAsync_CancellationStopsLockedFileRetry()
    {
        string outputDirectory = CreateTestDirectory();

        try
        {
            var output = new ObsTextFileOutput(outputDirectory);
            TrackerSnapshot previous = CreateSnapshot(secondBossDefeated: false);
            TrackerSnapshot current = CreateSnapshot(secondBossDefeated: true);
            await output.PublishAsync(new TrackerOutputUpdate(
                previous,
                previousSnapshot: null,
                DisplayLanguage.Japanese));
            string progressPath = Path.Combine(
                outputDirectory,
                ObsTextFileOutput.ProgressFileName);
            await using var lockedProgress = new FileStream(
                progressPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read);
            using var cancellationSource = new CancellationTokenSource(
                TimeSpan.FromMilliseconds(25));

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                output.PublishAsync(
                    new TrackerOutputUpdate(
                        current,
                        previous,
                        DisplayLanguage.Japanese),
                    cancellationSource.Token).AsTask());
        }
        finally
        {
            DeleteTestDirectory(outputDirectory);
        }
    }

    [Fact]
    public void TrySetOutputDirectory_RejectsRelativePathWithoutChangingTarget()
    {
        string outputDirectory = CreateTestDirectory();

        try
        {
            var output = new ObsTextFileOutput(outputDirectory);

            bool changed = output.TrySetOutputDirectory("relative-output");

            Assert.False(changed);
            Assert.Equal(outputDirectory, output.OutputDirectory);
        }
        finally
        {
            DeleteTestDirectory(outputDirectory);
        }
    }

    [Fact]
    public async Task PublishAsync_UsesConfiguredProgressFormat()
    {
        string outputDirectory = CreateTestDirectory();

        try
        {
            var output = new ObsTextFileOutput(outputDirectory);
            Assert.True(output.TrySetProgressFormat(
                "撃破 {defeated}/{total}体・残り{remaining}体・{percentage}%"));

            await output.PublishAsync(new TrackerOutputUpdate(
                CreateSnapshot(secondBossDefeated: false),
                previousSnapshot: null,
                DisplayLanguage.Japanese));

            Assert.Equal(
                "撃破 1/2体・残り1体・50.0%",
                File.ReadAllText(Path.Combine(
                    outputDirectory,
                    ObsTextFileOutput.ProgressFileName)));
        }
        finally
        {
            DeleteTestDirectory(outputDirectory);
        }
    }

    [Fact]
    public void TrySetProgressFormat_InvalidValueKeepsCurrentFormat()
    {
        var output = new ObsTextFileOutput();
        string original = output.ProgressFormat;

        bool changed = output.TrySetProgressFormat("{unknown}");

        Assert.False(changed);
        Assert.Equal(original, output.ProgressFormat);
    }

    private static TrackerSnapshot CreateSnapshot(
        bool secondBossDefeated,
        bool firstBossDefeated = true,
        int slotIndex = 0)
    {
        var treeSentinel = new BossDefinition(
            "tree-sentinel",
            1000,
            "Tree Sentinel",
            "ツリーガード",
            "limgrave",
            "Limgrave",
            "リムグレイブ",
            "Church of Elleh",
            "エレの教会",
            GameContent.BaseGame,
            0);
        var agheel = new BossDefinition(
            "agheel",
            1001,
            "Flying Dragon Agheel",
            "飛竜アギール",
            "limgrave",
            "Limgrave",
            "リムグレイブ",
            "Agheel Lake",
            "アギール湖",
            GameContent.BaseGame,
            1);

        return new TrackerSnapshot(
            new DateTimeOffset(2026, 9, 11, 9, 0, 0, TimeSpan.Zero),
            new CharacterSlot(slotIndex, "Tarnished", 150),
            [
                new BossProgress(treeSentinel, firstBossDefeated),
                new BossProgress(agheel, secondBossDefeated),
            ],
            [
                new RegionProgress(
                    "limgrave",
                    "Limgrave",
                    "リムグレイブ",
                    (firstBossDefeated ? 1 : 0) + (secondBossDefeated ? 1 : 0),
                    2),
            ]);
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
