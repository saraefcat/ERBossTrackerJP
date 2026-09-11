using ERBossTrackerJP.Core.Models;
using ERBossTrackerJP.Services.Settings;

namespace ERBossTrackerJP.Tests.Services.Settings;

public sealed class JsonUserSettingsServiceTests
{
    [Fact]
    public void TrySaveAndLoad_RoundTripsSupportedSettings()
    {
        string testDirectory = CreateTestDirectory();
        string settingsPath = Path.Combine(testDirectory, "settings.json");

        try
        {
            var service = new JsonUserSettingsService(settingsPath);
            var expected = new UserSettings(
                "D:\\EldenRing\\76561198000000000\\ER0000.sl2",
                4,
                DisplayLanguage.English,
                IsAutoMonitoringEnabled: false);

            bool saved = service.TrySave(expected);
            UserSettings actual = service.Load();
            string json = File.ReadAllText(settingsPath);

            Assert.True(saved);
            Assert.Equal(expected, actual);
            Assert.Contains("\"schemaVersion\": 1", json, StringComparison.Ordinal);
            Assert.Contains("\"displayLanguage\": \"en\"", json, StringComparison.Ordinal);
            Assert.DoesNotContain("eventFlag", json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("characterName", json, StringComparison.OrdinalIgnoreCase);
            Assert.Empty(Directory.EnumerateFiles(testDirectory, "*.tmp"));
        }
        finally
        {
            DeleteTestDirectory(testDirectory);
        }
    }

    [Fact]
    public void Load_CorruptJsonReturnsDefaults()
    {
        string testDirectory = CreateTestDirectory();
        string settingsPath = Path.Combine(testDirectory, "settings.json");

        try
        {
            File.WriteAllText(settingsPath, "{ this is not json");
            var service = new JsonUserSettingsService(settingsPath);

            UserSettings actual = service.Load();

            Assert.Equal(UserSettings.Default, actual);
        }
        finally
        {
            DeleteTestDirectory(testDirectory);
        }
    }

    [Fact]
    public void Load_UnsupportedSchemaReturnsDefaults()
    {
        string testDirectory = CreateTestDirectory();
        string settingsPath = Path.Combine(testDirectory, "settings.json");

        try
        {
            File.WriteAllText(
                settingsPath,
                """
                {
                  "schemaVersion": 99,
                  "saveFilePath": "D:\\EldenRing\\ER0000.sl2",
                  "characterSlotIndex": 2,
                  "displayLanguage": "en",
                  "isAutoMonitoringEnabled": false
                }
                """);
            var service = new JsonUserSettingsService(settingsPath);

            UserSettings actual = service.Load();

            Assert.Equal(UserSettings.Default, actual);
        }
        finally
        {
            DeleteTestDirectory(testDirectory);
        }
    }

    [Fact]
    public void Load_InvalidFieldsAreSanitizedIndependently()
    {
        string testDirectory = CreateTestDirectory();
        string settingsPath = Path.Combine(testDirectory, "settings.json");

        try
        {
            File.WriteAllText(
                settingsPath,
                """
                {
                  "schemaVersion": 1,
                  "saveFilePath": "   ",
                  "characterSlotIndex": 10,
                  "displayLanguage": "unknown",
                  "isAutoMonitoringEnabled": false
                }
                """);
            var service = new JsonUserSettingsService(settingsPath);

            UserSettings actual = service.Load();

            Assert.Null(actual.SaveFilePath);
            Assert.Null(actual.CharacterSlotIndex);
            Assert.Equal(DisplayLanguage.Japanese, actual.DisplayLanguage);
            Assert.False(actual.IsAutoMonitoringEnabled);
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
