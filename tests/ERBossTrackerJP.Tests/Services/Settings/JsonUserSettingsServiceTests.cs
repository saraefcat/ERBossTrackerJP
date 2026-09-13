using ERBossTrackerJP.Core.Models;
using ERBossTrackerJP.Services.Settings;
using ERBossTrackerJP.Services.Theming;

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
                IsAutoMonitoringEnabled: false,
                ApplicationTheme.Light,
                IsObsOutputEnabled: true,
                ObsOutputDirectory: "D:\\OBS",
                ObsProgressFormat: "撃破 {defeated}/{total}（{percentage}%）",
                WindowPlacement: new WindowPlacementSetting(
                    -1_200,
                    80,
                    1_280,
                    800,
                    IsMaximized: true));

            bool saved = service.TrySave(expected);
            UserSettings actual = service.Load();
            string json = File.ReadAllText(settingsPath);

            Assert.True(saved);
            Assert.Equal(expected, actual);
            Assert.Contains("\"schemaVersion\": 2", json, StringComparison.Ordinal);
            Assert.Contains("\"displayLanguage\": \"en\"", json, StringComparison.Ordinal);
            Assert.Contains("\"theme\": \"light\"", json, StringComparison.Ordinal);
            Assert.Contains("\"isObsOutputEnabled\": true", json, StringComparison.Ordinal);
            Assert.Contains("\"obsOutputDirectory\": \"D:\\\\OBS\"", json, StringComparison.Ordinal);
            Assert.Contains(
                "\"obsProgressFormat\":",
                json,
                StringComparison.Ordinal);
            Assert.Contains("\"windowPlacement\":", json, StringComparison.Ordinal);
            Assert.Contains("\"isMaximized\": true", json, StringComparison.Ordinal);
            Assert.DoesNotContain("eventFlag", json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("characterName", json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("completionFilter", json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("regionFilter", json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("searchText", json, StringComparison.OrdinalIgnoreCase);
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
    public void TrySaveAndLoad_RoundTripsCharacterSpecificDeathCountBaselines()
    {
        string testDirectory = CreateTestDirectory();
        string settingsPath = Path.Combine(testDirectory, "settings.json");

        try
        {
            var service = new JsonUserSettingsService(settingsPath);
            var settings = new UserSettings(
                SaveFilePath: "D:\\EldenRing\\ER0000.sl2",
                CharacterSlotIndex: 4,
                DeathCountBaselines:
                [
                    new DeathCountBaselineSetting(
                        "D:\\EldenRing\\ER0000.sl2",
                        4,
                        1_234,
                        IsEnabled: true),
                    new DeathCountBaselineSetting(
                        "D:\\EldenRing\\ER0000.sl2",
                        7,
                        99,
                        IsEnabled: false),
                    new DeathCountBaselineSetting(
                        "D:\\EldenRing\\ER0000.sl2",
                        8,
                        0,
                        IsEnabled: true),
                ]);

            Assert.True(service.TrySave(settings));
            UserSettings actual = service.Load();

            Assert.Collection(
                actual.DeathCountBaselines!,
                baseline => Assert.Equal(
                    new DeathCountBaselineSetting(
                        "D:\\EldenRing\\ER0000.sl2",
                        4,
                        1_234,
                        IsEnabled: true),
                    baseline),
                baseline => Assert.Equal(
                    new DeathCountBaselineSetting(
                        "D:\\EldenRing\\ER0000.sl2",
                        7,
                        99,
                        IsEnabled: false),
                    baseline),
                baseline => Assert.Equal(
                    new DeathCountBaselineSetting(
                        "D:\\EldenRing\\ER0000.sl2",
                        8,
                        0,
                        IsEnabled: true),
                    baseline));
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
                  "isAutoMonitoringEnabled": false,
                  "theme": "unknown",
                  "isObsOutputEnabled": true,
                  "obsOutputDirectory": "   ",
                  "obsProgressFormat": "{unknown}"
                }
                """);
            var service = new JsonUserSettingsService(settingsPath);

            UserSettings actual = service.Load();

            Assert.Null(actual.SaveFilePath);
            Assert.Null(actual.CharacterSlotIndex);
            Assert.Equal(DisplayLanguage.Japanese, actual.DisplayLanguage);
            Assert.False(actual.IsAutoMonitoringEnabled);
            Assert.Equal(ApplicationTheme.Dark, actual.Theme);
            Assert.True(actual.IsObsOutputEnabled);
            Assert.Null(actual.ObsOutputDirectory);
            Assert.Null(actual.ObsProgressFormat);
            Assert.Null(actual.DeathCountBaselines);
        }
        finally
        {
            DeleteTestDirectory(testDirectory);
        }
    }

    [Fact]
    public void Load_InvalidWindowPlacementIsIgnored()
    {
        string testDirectory = CreateTestDirectory();
        string settingsPath = Path.Combine(testDirectory, "settings.json");

        try
        {
            File.WriteAllText(
                settingsPath,
                """
                {
                  "schemaVersion": 2,
                  "displayLanguage": "en",
                  "windowPlacement": {
                    "left": 100,
                    "top": 100,
                    "width": 1280,
                    "height": 0,
                    "isMaximized": true
                  }
                }
                """);
            var service = new JsonUserSettingsService(settingsPath);

            UserSettings actual = service.Load();

            Assert.Equal(DisplayLanguage.English, actual.DisplayLanguage);
            Assert.Null(actual.WindowPlacement);
        }
        finally
        {
            DeleteTestDirectory(testDirectory);
        }
    }

    [Fact]
    public void Load_LegacyDocumentIgnoresAdditiveOffsetsAndPreservesOtherSettings()
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
                  "saveFilePath": "D:\\EldenRing\\ER0000.sl2",
                  "characterSlotIndex": 2,
                  "displayLanguage": "en",
                  "isAutoMonitoringEnabled": false,
                  "windowPlacement": {
                    "left": 100,
                    "top": 100,
                    "width": 1280,
                    "height": 800,
                    "isMaximized": true
                  },
                  "deathCountOffsets": [
                    {
                      "saveFilePath": "D:\\EldenRing\\ER0000.sl2",
                      "characterSlotIndex": 2,
                      "offset": 1234
                    }
                  ]
                }
                """);
            var service = new JsonUserSettingsService(settingsPath);

            UserSettings actual = service.Load();

            Assert.Equal("D:\\EldenRing\\ER0000.sl2", actual.SaveFilePath);
            Assert.Equal(2, actual.CharacterSlotIndex);
            Assert.Equal(DisplayLanguage.English, actual.DisplayLanguage);
            Assert.False(actual.IsAutoMonitoringEnabled);
            Assert.Equal(ApplicationTheme.Dark, actual.Theme);
            Assert.False(actual.IsObsOutputEnabled);
            Assert.Null(actual.ObsOutputDirectory);
            Assert.Null(actual.ObsProgressFormat);
            Assert.Null(actual.DeathCountBaselines);
            Assert.Null(actual.WindowPlacement);
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
