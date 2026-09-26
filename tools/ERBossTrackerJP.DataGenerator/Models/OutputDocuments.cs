using System.Text.Json.Serialization;

namespace ERBossTrackerJP.DataGenerator.Models;

public sealed record GeneratedBossDocument(
    [property: JsonPropertyName("schemaVersion")] int SchemaVersion,
    [property: JsonPropertyName("bosses")] IReadOnlyList<GeneratedBossDefinition> Bosses);

public sealed record GeneratedBossDefinition(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("flagId")] uint FlagId,
    [property: JsonPropertyName("nameEn")] string NameEn,
    [property: JsonPropertyName("nameJa")] string NameJa,
    [property: JsonPropertyName("regionId")] string RegionId,
    [property: JsonPropertyName("regionEn")] string RegionEn,
    [property: JsonPropertyName("regionJa")] string RegionJa,
    [property: JsonPropertyName("locationEn")] string LocationEn,
    [property: JsonPropertyName("locationJa")] string LocationJa,
    [property: JsonPropertyName("content")] string Content,
    [property: JsonPropertyName("sortOrder")] int SortOrder);

public sealed record ComparisonReport(
    [property: JsonPropertyName("schemaVersion")] int SchemaVersion,
    [property: JsonPropertyName("source")] ComparisonSource Source,
    [property: JsonPropertyName("summary")] ComparisonSummary Summary,
    [property: JsonPropertyName("entries")] IReadOnlyList<ComparisonEntry> Entries,
    [property: JsonPropertyName("warnings")] IReadOnlyList<string> Warnings);

public sealed record ComparisonSource(
    [property: JsonPropertyName("game")] string Game,
    [property: JsonPropertyName("appVersion")] string AppVersion,
    [property: JsonPropertyName("regulationVersion")] string RegulationVersion,
    [property: JsonPropertyName("toolVersion")] string ToolVersion);

public sealed record ComparisonSummary(
    [property: JsonPropertyName("bossCount")] int BossCount,
    [property: JsonPropertyName("mappingCount")] int MappingCount,
    [property: JsonPropertyName("npcResolutionCounts")] IReadOnlyDictionary<string, int> NpcResolutionCounts,
    [property: JsonPropertyName("placeResolutionCounts")] IReadOnlyDictionary<string, int> PlaceResolutionCounts,
    [property: JsonPropertyName("changedBossCount")] int ChangedBossCount,
    [property: JsonPropertyName("nameJaChangeCount")] int NameJaChangeCount,
    [property: JsonPropertyName("nameEnChangeCount")] int NameEnChangeCount,
    [property: JsonPropertyName("locationJaChangeCount")] int LocationJaChangeCount,
    [property: JsonPropertyName("locationEnChangeCount")] int LocationEnChangeCount,
    [property: JsonPropertyName("expectedDiffCount")] int ExpectedDiffCount,
    [property: JsonPropertyName("unexpectedDiffCount")] int UnexpectedDiffCount,
    [property: JsonPropertyName("unresolvedCount")] int UnresolvedCount,
    [property: JsonPropertyName("fallbackCount")] int FallbackCount,
    [property: JsonPropertyName("warningCount")] int WarningCount,
    [property: JsonPropertyName("validationPassed")] bool ValidationPassed);

public sealed record ComparisonEntry(
    [property: JsonPropertyName("flagId")] uint FlagId,
    [property: JsonPropertyName("sortOrder")] int SortOrder,
    [property: JsonPropertyName("oldNameJa")] string OldNameJa,
    [property: JsonPropertyName("newNameJa")] string NewNameJa,
    [property: JsonPropertyName("oldNameEn")] string OldNameEn,
    [property: JsonPropertyName("newNameEn")] string NewNameEn,
    [property: JsonPropertyName("npcNameResolution")] string NpcNameResolution,
    [property: JsonPropertyName("npcNameCandidates")] IReadOnlyList<ResolutionCandidateSet> NpcNameCandidates,
    [property: JsonPropertyName("oldLocationJa")] string OldLocationJa,
    [property: JsonPropertyName("newLocationJa")] string NewLocationJa,
    [property: JsonPropertyName("oldLocationEn")] string OldLocationEn,
    [property: JsonPropertyName("newLocationEn")] string NewLocationEn,
    [property: JsonPropertyName("placeNameResolution")] string PlaceNameResolution,
    [property: JsonPropertyName("placeNameCandidates")] IReadOnlyList<long> PlaceNameCandidates,
    [property: JsonPropertyName("changedFields")] IReadOnlyList<string> ChangedFields,
    [property: JsonPropertyName("expectedDiffs")] IReadOnlyList<string> ExpectedDiffs,
    [property: JsonPropertyName("unexpectedDiffs")] IReadOnlyList<string> UnexpectedDiffs);

public sealed record ResolutionCandidateSet(
    [property: JsonPropertyName("resolution")] string Resolution,
    [property: JsonPropertyName("textIds")] IReadOnlyList<long> TextIds);
