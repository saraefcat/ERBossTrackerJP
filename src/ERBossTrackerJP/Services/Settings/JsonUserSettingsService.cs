using System.Diagnostics;
using System.IO;
using System.Text.Json;
using ERBossTrackerJP.Core.Models;
using ERBossTrackerJP.Services.Theming;

namespace ERBossTrackerJP.Services.Settings;

public sealed class JsonUserSettingsService : IUserSettingsService
{
    public const int CurrentSchemaVersion = 1;
    public const int MaximumSettingsFileSize = 64 * 1024;
    public const string ApplicationDirectoryName = "ERBossTrackerJP";
    public const string SettingsFileName = "settings.json";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    private readonly string? _settingsPath;

    public JsonUserSettingsService(string? settingsPath = null)
    {
        if (settingsPath is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(settingsPath);
            _settingsPath = Path.GetFullPath(settingsPath);
            return;
        }

        string localApplicationData = Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData);

        if (!string.IsNullOrWhiteSpace(localApplicationData))
        {
            _settingsPath = Path.Combine(
                Path.GetFullPath(localApplicationData),
                ApplicationDirectoryName,
                SettingsFileName);
        }
    }

    public UserSettings Load()
    {
        if (_settingsPath is null)
        {
            return UserSettings.Default;
        }

        try
        {
            var file = new FileInfo(_settingsPath);
            file.Refresh();

            if (!file.Exists)
            {
                return UserSettings.Default;
            }

            if (file.Length > MaximumSettingsFileSize)
            {
                Trace.WriteLine(
                    $"[JsonUserSettingsService] Settings file is too large: {_settingsPath}");
                return UserSettings.Default;
            }

            string json = File.ReadAllText(_settingsPath);
            SettingsDocument? document = JsonSerializer.Deserialize<SettingsDocument>(
                json,
                SerializerOptions);
            return Validate(document);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or JsonException)
        {
            Trace.WriteLine(
                $"[JsonUserSettingsService] Settings load failed: {exception}");
            return UserSettings.Default;
        }
    }

    public bool TrySave(UserSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (_settingsPath is null)
        {
            return false;
        }

        string? directoryPath = Path.GetDirectoryName(_settingsPath);

        if (string.IsNullOrWhiteSpace(directoryPath))
        {
            return false;
        }

        string temporaryPath = Path.Combine(
            directoryPath,
            $".{SettingsFileName}.{Guid.NewGuid():N}.tmp");

        try
        {
            Directory.CreateDirectory(directoryPath);
            var document = new SettingsDocument(
                CurrentSchemaVersion,
                NormalizePath(settings.SaveFilePath),
                ValidateSlotIndex(settings.CharacterSlotIndex),
                ToLanguageCode(settings.DisplayLanguage),
                settings.IsAutoMonitoringEnabled,
                ToThemeCode(settings.Theme));
            string json = JsonSerializer.Serialize(document, SerializerOptions);
            File.WriteAllText(temporaryPath, json);
            File.Move(temporaryPath, _settingsPath, overwrite: true);
            return true;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            Trace.WriteLine(
                $"[JsonUserSettingsService] Settings save failed: {exception}");
            TryDeleteTemporaryFile(temporaryPath);
            return false;
        }
    }

    private static UserSettings Validate(SettingsDocument? document)
    {
        if (document is null || document.SchemaVersion != CurrentSchemaVersion)
        {
            return UserSettings.Default;
        }

        DisplayLanguage language = document.DisplayLanguage switch
        {
            "ja" => DisplayLanguage.Japanese,
            "en" => DisplayLanguage.English,
            _ => DisplayLanguage.Japanese,
        };
        ApplicationTheme theme = document.Theme switch
        {
            "light" => ApplicationTheme.Light,
            _ => ApplicationTheme.Dark,
        };
        return new UserSettings(
            NormalizePath(document.SaveFilePath),
            ValidateSlotIndex(document.CharacterSlotIndex),
            language,
            document.IsAutoMonitoringEnabled ?? true,
            theme);
    }

    private static string? NormalizePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || path.Length > short.MaxValue)
        {
            return null;
        }

        return path.Trim();
    }

    private static int? ValidateSlotIndex(int? slotIndex) =>
        slotIndex is >= 0 and < CharacterSlot.MaximumSlotCount
            ? slotIndex
            : null;

    private static string ToLanguageCode(DisplayLanguage language) => language switch
    {
        DisplayLanguage.Japanese => "ja",
        DisplayLanguage.English => "en",
        _ => "ja",
    };

    private static string ToThemeCode(ApplicationTheme theme) => theme switch
    {
        ApplicationTheme.Light => "light",
        _ => "dark",
    };

    private static void TryDeleteTemporaryFile(string temporaryPath)
    {
        try
        {
            File.Delete(temporaryPath);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
            Trace.WriteLine(
                $"[JsonUserSettingsService] Temporary file cleanup failed: {exception}");
        }
    }

    private sealed record SettingsDocument(
        int? SchemaVersion,
        string? SaveFilePath,
        int? CharacterSlotIndex,
        string? DisplayLanguage,
        bool? IsAutoMonitoringEnabled,
        string? Theme);
}
