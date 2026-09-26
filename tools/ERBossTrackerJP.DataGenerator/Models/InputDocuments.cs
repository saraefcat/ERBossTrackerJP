using System.Text.Json.Serialization;

namespace ERBossTrackerJP.DataGenerator.Models;

public sealed class LocalizationMappingDocument
{
    [JsonPropertyName("schemaVersion")]
    public int? SchemaVersion { get; init; }

    [JsonPropertyName("source")]
    public MappingSource? Source { get; init; }

    [JsonPropertyName("languageSeparators")]
    public LanguageSeparators? LanguageSeparators { get; init; }

    [JsonPropertyName("entries")]
    public LocalizationMappingEntry?[]? Entries { get; init; }
}

public sealed class MappingSource
{
    [JsonPropertyName("game")]
    public string? Game { get; init; }

    [JsonPropertyName("appVersion")]
    public string? AppVersion { get; init; }

    [JsonPropertyName("regulationVersion")]
    public string? RegulationVersion { get; init; }
}

public sealed class LanguageSeparators
{
    [JsonPropertyName("ja")]
    public string? Ja { get; init; }

    [JsonPropertyName("en")]
    public string? En { get; init; }
}

public sealed class LocalizationMappingEntry
{
    [JsonPropertyName("flagId")]
    public uint? FlagId { get; init; }

    [JsonPropertyName("npcName")]
    public LocalizationRule? NpcName { get; init; }

    [JsonPropertyName("placeName")]
    public LocalizationRule? PlaceName { get; init; }
}

public sealed class LocalizationRule
{
    [JsonPropertyName("resolution")]
    public string? Resolution { get; init; }

    [JsonPropertyName("textIds")]
    public long[]? TextIds { get; init; }

    [JsonPropertyName("composition")]
    public string? Composition { get; init; }

    [JsonPropertyName("components")]
    public LocalizationRule?[]? Components { get; init; }
}

public sealed class OfficialTextDocument
{
    [JsonPropertyName("schemaVersion")]
    public int? SchemaVersion { get; init; }

    [JsonPropertyName("game")]
    public string? Game { get; init; }

    [JsonPropertyName("appVersion")]
    public string? AppVersion { get; init; }

    [JsonPropertyName("regulationVersion")]
    public string? RegulationVersion { get; init; }

    [JsonPropertyName("table")]
    public string? Table { get; init; }

    [JsonPropertyName("entries")]
    public OfficialTextEntry?[]? Entries { get; init; }
}

public sealed class OfficialTextEntry
{
    [JsonPropertyName("textId")]
    public long? TextId { get; init; }

    [JsonPropertyName("ja")]
    public string? Ja { get; init; }

    [JsonPropertyName("en")]
    public string? En { get; init; }

    [JsonPropertyName("table")]
    public string? Table { get; init; }

    [JsonPropertyName("jaSourcePath")]
    public string? JaSourcePath { get; init; }

    [JsonPropertyName("enSourcePath")]
    public string? EnSourcePath { get; init; }
}

public sealed class LocalizationManifest
{
    [JsonPropertyName("schemaVersion")]
    public int? SchemaVersion { get; init; }

    [JsonPropertyName("toolVersion")]
    public string? ToolVersion { get; init; }

    [JsonPropertyName("game")]
    public string? Game { get; init; }

    [JsonPropertyName("appVersion")]
    public string? AppVersion { get; init; }

    [JsonPropertyName("regulationVersion")]
    public string? RegulationVersion { get; init; }

    [JsonPropertyName("languages")]
    public string?[]? Languages { get; init; }

    [JsonPropertyName("tables")]
    public ManifestTable?[]? Tables { get; init; }
}

public sealed class ManifestTable
{
    [JsonPropertyName("table")]
    public string? Table { get; init; }

    [JsonPropertyName("file")]
    public string? File { get; init; }

    [JsonPropertyName("entryCount")]
    public int? EntryCount { get; init; }

    [JsonPropertyName("missingJaCount")]
    public int? MissingJaCount { get; init; }

    [JsonPropertyName("missingEnCount")]
    public int? MissingEnCount { get; init; }

    [JsonPropertyName("duplicateJaOverrideCount")]
    public int? DuplicateJaOverrideCount { get; init; }

    [JsonPropertyName("duplicateEnOverrideCount")]
    public int? DuplicateEnOverrideCount { get; init; }
}
