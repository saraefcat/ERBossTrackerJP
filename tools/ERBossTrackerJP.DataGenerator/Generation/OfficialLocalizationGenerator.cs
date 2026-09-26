using ERBossTrackerJP.Core.Bosses;
using ERBossTrackerJP.Core.Models;
using ERBossTrackerJP.DataGenerator.IO;
using ERBossTrackerJP.DataGenerator.Models;

namespace ERBossTrackerJP.DataGenerator.Generation;

public sealed class OfficialLocalizationGenerator
{
    public const int ProductionBossCount = 207;
    public const int CurrentMappingSchemaVersion = 1;
    public const int CurrentOfficialTextSchemaVersion = 1;
    public const string ExpectedGame = "EldenRing";
    public const string ExpectedAppVersion = "1.17";
    public const string ExpectedRegulationVersion = "1.17";
    public const string JapaneseSeparator = "＆";
    public const string EnglishSeparator = " & ";

    private const int MaximumMappingBytes = 4 * 1024 * 1024;
    private const int MaximumOfficialTextBytes = 16 * 1024 * 1024;
    private const int MaximumManifestBytes = 1024 * 1024;

    private static readonly HashSet<string> ExpectedLocalizedFields = new(StringComparer.Ordinal)
    {
        "nameJa",
        "nameEn",
        "locationJa",
        "locationEn",
    };

    private readonly BossDefinitionLoader _bossLoader = new();

    public GenerationResult Generate(GenerationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.ExpectedBossCount <= 0 || request.ExpectedBossCount > BossDefinitionLoader.MaximumBossCount)
        {
            throw new LocalizationGenerationException(
                LocalizationGenerationErrorCode.InvalidArgument,
                $"Expected boss count must be between 1 and {BossDefinitionLoader.MaximumBossCount}.");
        }

        IReadOnlyList<BossDefinition> sourceBosses = ReadBosses(request);
        LocalizationMappingDocument mapping = StrictJsonFile.Read<LocalizationMappingDocument>(
            request.MappingPath,
            MaximumMappingBytes);
        OfficialTextDocument npcDocument = StrictJsonFile.Read<OfficialTextDocument>(
            request.NpcNamesPath,
            MaximumOfficialTextBytes);
        OfficialTextDocument placeDocument = StrictJsonFile.Read<OfficialTextDocument>(
            request.PlaceNamesPath,
            MaximumOfficialTextBytes);
        LocalizationManifest manifest = StrictJsonFile.Read<LocalizationManifest>(
            request.ManifestPath,
            MaximumManifestBytes);

        ValidatedManifest validatedManifest = ValidateManifest(manifest, npcDocument, placeDocument);
        ValidateMappingHeader(mapping, validatedManifest);

        IReadOnlyDictionary<long, OfficialDisplayText> npcTexts = CreateCatalog(
            npcDocument,
            "NpcName",
            validatedManifest.NpcTable);
        IReadOnlyDictionary<long, OfficialDisplayText> placeTexts = CreateCatalog(
            placeDocument,
            "PlaceName",
            validatedManifest.PlaceTable);
        IReadOnlyDictionary<uint, LocalizationMappingEntry> mappings = ValidateMappingCoverage(
            mapping,
            sourceBosses,
            request.ExpectedBossCount);

        var generatedBosses = new List<GeneratedBossDefinition>(sourceBosses.Count);
        var comparisonEntries = new List<ComparisonEntry>(sourceBosses.Count);
        var npcResolutionCounts = CreateResolutionCounts();
        var placeResolutionCounts = CreateResolutionCounts(includeComposite: false);

        foreach (BossDefinition source in sourceBosses)
        {
            LocalizationMappingEntry entry = mappings[source.FlagId];
            LocalizationRule npcRule = entry.NpcName!;
            LocalizationRule placeRule = entry.PlaceName!;
            ResolvedText npc = ResolveRule(npcRule, npcTexts, source.FlagId, "npcName", allowComposite: true);
            ResolvedText place = ResolveRule(placeRule, placeTexts, source.FlagId, "placeName", allowComposite: false);

            npcResolutionCounts[npcRule.Resolution!]++;
            placeResolutionCounts[placeRule.Resolution!]++;

            var generated = new GeneratedBossDefinition(
                source.Id,
                source.FlagId,
                npc.En,
                npc.Ja,
                source.RegionId,
                source.RegionEn,
                source.RegionJa,
                place.En,
                place.Ja,
                ToJsonContent(source.Content),
                source.SortOrder);
            generatedBosses.Add(generated);
            comparisonEntries.Add(CreateComparisonEntry(source, generated, npcRule, placeRule));
        }

        if (generatedBosses.Count != request.ExpectedBossCount)
        {
            throw new LocalizationGenerationException(
                LocalizationGenerationErrorCode.InvalidBossCount,
                $"Generated {generatedBosses.Count} bosses; expected {request.ExpectedBossCount}.");
        }

        int unexpectedDiffCount = comparisonEntries.Sum(static entry => entry.UnexpectedDiffs.Count);

        if (unexpectedDiffCount != 0)
        {
            throw new LocalizationGenerationException(
                LocalizationGenerationErrorCode.UnexpectedDifference,
                $"Comparison found {unexpectedDiffCount} unexpected differences.");
        }

        var warnings = Array.Empty<string>();
        var summary = new ComparisonSummary(
            generatedBosses.Count,
            mappings.Count,
            npcResolutionCounts,
            placeResolutionCounts,
            comparisonEntries.Count(static entry => entry.ChangedFields.Count > 0),
            comparisonEntries.Count(static entry => entry.ChangedFields.Contains("nameJa", StringComparer.Ordinal)),
            comparisonEntries.Count(static entry => entry.ChangedFields.Contains("nameEn", StringComparer.Ordinal)),
            comparisonEntries.Count(static entry => entry.ChangedFields.Contains("locationJa", StringComparer.Ordinal)),
            comparisonEntries.Count(static entry => entry.ChangedFields.Contains("locationEn", StringComparer.Ordinal)),
            comparisonEntries.Sum(static entry => entry.ExpectedDiffs.Count),
            unexpectedDiffCount,
            UnresolvedCount: 0,
            FallbackCount: 0,
            WarningCount: warnings.Length,
            ValidationPassed: true);
        var reportSource = new ComparisonSource(
            validatedManifest.Game,
            validatedManifest.AppVersion,
            validatedManifest.RegulationVersion,
            validatedManifest.ToolVersion);

        return new GenerationResult(
            new GeneratedBossDocument(BossDefinitionLoader.CurrentSchemaVersion, generatedBosses),
            new ComparisonReport(1, reportSource, summary, comparisonEntries, warnings));
    }

    private IReadOnlyList<BossDefinition> ReadBosses(GenerationRequest request)
    {
        byte[] bytes = File.ReadAllBytes(request.SourceBossesPath);

        try
        {
            return _bossLoader.Read(bytes, request.ExpectedBossCount);
        }
        catch (BossDataException exception)
        {
            throw new LocalizationGenerationException(
                LocalizationGenerationErrorCode.InvalidBossCount,
                $"Source boss metadata is invalid: {exception.Message}",
                innerException: exception);
        }
    }

    private static ValidatedManifest ValidateManifest(
        LocalizationManifest manifest,
        OfficialTextDocument npcDocument,
        OfficialTextDocument placeDocument)
    {
        if (manifest.SchemaVersion != CurrentOfficialTextSchemaVersion)
        {
            throw Error(
                LocalizationGenerationErrorCode.UnsupportedSchemaVersion,
                $"Manifest schemaVersion must be {CurrentOfficialTextSchemaVersion}.");
        }

        RequireEqual(manifest.Game, ExpectedGame, "manifest game");
        RequireEqual(manifest.AppVersion, ExpectedAppVersion, "manifest appVersion");
        RequireEqual(manifest.RegulationVersion, ExpectedRegulationVersion, "manifest regulationVersion");

        if (string.IsNullOrWhiteSpace(manifest.ToolVersion))
        {
            throw Error(LocalizationGenerationErrorCode.InvalidManifest, "Manifest toolVersion is required.");
        }

        string?[] languages = manifest.Languages ??
            throw Error(LocalizationGenerationErrorCode.InvalidManifest, "Manifest languages are required.");

        if (languages.Any(string.IsNullOrWhiteSpace) ||
            languages.Distinct(StringComparer.Ordinal).Count() != languages.Length ||
            !languages.Contains("jpnjp", StringComparer.Ordinal) ||
            !languages.Contains("engus", StringComparer.Ordinal))
        {
            throw Error(
                LocalizationGenerationErrorCode.InvalidManifest,
                "Manifest languages must uniquely include 'jpnjp' and 'engus'.");
        }

        ManifestTable?[] tables = manifest.Tables ??
            throw Error(LocalizationGenerationErrorCode.InvalidManifest, "Manifest tables are required.");
        ManifestTable npcTable = ReadManifestTable(tables, "NpcName", "npc-names.json");
        ManifestTable placeTable = ReadManifestTable(tables, "PlaceName", "place-names.json");

        if (tables.Length != 2)
        {
            throw Error(
                LocalizationGenerationErrorCode.InvalidManifest,
                $"Manifest must contain exactly NpcName and PlaceName tables; found {tables.Length}.");
        }

        ValidateOfficialDocumentMetadata(npcDocument, manifest, "NpcName");
        ValidateOfficialDocumentMetadata(placeDocument, manifest, "PlaceName");

        return new ValidatedManifest(
            manifest.Game!,
            manifest.AppVersion!,
            manifest.RegulationVersion!,
            manifest.ToolVersion!,
            npcTable,
            placeTable);
    }

    private static ManifestTable ReadManifestTable(
        IEnumerable<ManifestTable?> tables,
        string tableName,
        string fileName)
    {
        ManifestTable[] matches = tables
            .Where(table => string.Equals(table?.Table, tableName, StringComparison.Ordinal))
            .Cast<ManifestTable>()
            .ToArray();

        if (matches.Length != 1)
        {
            throw Error(
                LocalizationGenerationErrorCode.InvalidManifest,
                $"Manifest must contain exactly one {tableName} table.");
        }

        ManifestTable table = matches[0];
        RequireEqual(table.File, fileName, $"manifest {tableName} file");

        if (table.EntryCount is null or < 0 ||
            table.MissingJaCount is null or < 0 ||
            table.MissingEnCount is null or < 0 ||
            table.DuplicateJaOverrideCount is null or < 0 ||
            table.DuplicateEnOverrideCount is null or < 0)
        {
            throw Error(
                LocalizationGenerationErrorCode.InvalidManifest,
                $"Manifest counts for {tableName} must be non-negative integers.");
        }

        return table;
    }

    private static void ValidateOfficialDocumentMetadata(
        OfficialTextDocument document,
        LocalizationManifest manifest,
        string tableName)
    {
        if (document.SchemaVersion != CurrentOfficialTextSchemaVersion)
        {
            throw Error(
                LocalizationGenerationErrorCode.UnsupportedSchemaVersion,
                $"{tableName} schemaVersion must be {CurrentOfficialTextSchemaVersion}.");
        }

        RequireEqual(document.Game, manifest.Game!, $"{tableName} game");
        RequireEqual(document.AppVersion, manifest.AppVersion!, $"{tableName} appVersion");
        RequireEqual(document.RegulationVersion, manifest.RegulationVersion!, $"{tableName} regulationVersion");
        RequireEqual(document.Table, tableName, $"{tableName} table");
    }

    private static IReadOnlyDictionary<long, OfficialDisplayText> CreateCatalog(
        OfficialTextDocument document,
        string tableName,
        ManifestTable manifestTable)
    {
        OfficialTextEntry?[] entries = document.Entries ??
            throw Error(
                LocalizationGenerationErrorCode.InvalidOfficialText,
                $"{tableName} entries are required.");

        if (entries.Length != manifestTable.EntryCount)
        {
            throw Error(
                LocalizationGenerationErrorCode.InvalidManifest,
                $"{tableName} contains {entries.Length} entries; manifest declares {manifestTable.EntryCount}.");
        }

        int missingJaCount = entries.Count(static entry => string.IsNullOrWhiteSpace(entry?.Ja));
        int missingEnCount = entries.Count(static entry => string.IsNullOrWhiteSpace(entry?.En));

        if (missingJaCount != manifestTable.MissingJaCount || missingEnCount != manifestTable.MissingEnCount)
        {
            throw Error(
                LocalizationGenerationErrorCode.InvalidManifest,
                $"{tableName} missing-text counts do not match the manifest.");
        }

        var catalog = new Dictionary<long, OfficialDisplayText>();

        for (int index = 0; index < entries.Length; index++)
        {
            OfficialTextEntry entry = entries[index] ??
                throw Error(
                    LocalizationGenerationErrorCode.InvalidOfficialText,
                    $"{tableName} entry {index} is null.");

            if (entry.TextId is null)
            {
                throw Error(
                    LocalizationGenerationErrorCode.InvalidOfficialText,
                    $"{tableName} entry {index} has no textId.");
            }

            RequireEqual(entry.Table, tableName, $"{tableName} entry {entry.TextId} table");

            if ((!string.IsNullOrWhiteSpace(entry.Ja) && string.IsNullOrWhiteSpace(entry.JaSourcePath)) ||
                (!string.IsNullOrWhiteSpace(entry.En) && string.IsNullOrWhiteSpace(entry.EnSourcePath)))
            {
                throw Error(
                    LocalizationGenerationErrorCode.InvalidOfficialText,
                    $"{tableName} entry {entry.TextId} must include a source path for each non-empty localized value.");
            }

            if (!catalog.TryAdd(entry.TextId.Value, new OfficialDisplayText(entry.Ja, entry.En)))
            {
                throw Error(
                    LocalizationGenerationErrorCode.InvalidOfficialText,
                    $"{tableName} textId {entry.TextId} is duplicated.");
            }
        }

        return catalog;
    }

    private static void ValidateMappingHeader(
        LocalizationMappingDocument mapping,
        ValidatedManifest manifest)
    {
        if (mapping.SchemaVersion != CurrentMappingSchemaVersion)
        {
            throw Error(
                LocalizationGenerationErrorCode.UnsupportedSchemaVersion,
                $"Mapping schemaVersion must be {CurrentMappingSchemaVersion}.");
        }

        MappingSource source = mapping.Source ??
            throw Error(LocalizationGenerationErrorCode.InvalidJson, "Mapping source is required.");
        RequireEqual(source.Game, manifest.Game, "mapping source game");
        RequireEqual(source.AppVersion, manifest.AppVersion, "mapping source appVersion");
        RequireEqual(source.RegulationVersion, manifest.RegulationVersion, "mapping source regulationVersion");

        LanguageSeparators separators = mapping.LanguageSeparators ??
            throw Error(LocalizationGenerationErrorCode.InvalidJson, "Mapping languageSeparators are required.");
        RequireEqual(separators.Ja, JapaneseSeparator, "mapping Japanese separator");
        RequireEqual(separators.En, EnglishSeparator, "mapping English separator");
    }

    private static IReadOnlyDictionary<uint, LocalizationMappingEntry> ValidateMappingCoverage(
        LocalizationMappingDocument mapping,
        IReadOnlyList<BossDefinition> sourceBosses,
        int expectedBossCount)
    {
        LocalizationMappingEntry?[] entries = mapping.Entries ??
            throw Error(LocalizationGenerationErrorCode.InvalidMappingCount, "Mapping entries are required.");

        if (entries.Length != expectedBossCount)
        {
            throw Error(
                LocalizationGenerationErrorCode.InvalidMappingCount,
                $"Mapping contains {entries.Length} entries; expected {expectedBossCount}.");
        }

        var mappings = new Dictionary<uint, LocalizationMappingEntry>();

        for (int index = 0; index < entries.Length; index++)
        {
            LocalizationMappingEntry entry = entries[index] ??
                throw Error(
                    LocalizationGenerationErrorCode.InvalidJson,
                    $"Mapping entry {index} is null.");

            if (entry.FlagId is null or 0)
            {
                throw Error(
                    LocalizationGenerationErrorCode.InvalidJson,
                    $"Mapping entry {index} has no positive flagId.");
            }

            if (entry.NpcName is null || entry.PlaceName is null)
            {
                throw Error(
                    LocalizationGenerationErrorCode.InvalidJson,
                    $"Mapping for flagId {entry.FlagId} must include npcName and placeName.",
                    entry.FlagId);
            }

            if (!mappings.TryAdd(entry.FlagId.Value, entry))
            {
                throw Error(
                    LocalizationGenerationErrorCode.DuplicateFlagId,
                    $"Mapping flagId {entry.FlagId} is duplicated.",
                    entry.FlagId);
            }
        }

        var sourceFlags = sourceBosses.Select(static boss => boss.FlagId).ToHashSet();
        uint[] missing = sourceFlags.Except(mappings.Keys).Order().ToArray();
        uint[] extra = mappings.Keys.Except(sourceFlags).Order().ToArray();

        if (missing.Length > 0 || extra.Length > 0)
        {
            var problems = new List<string>(capacity: 2);

            if (missing.Length > 0)
            {
                problems.Add($"missing flagIds: {string.Join(", ", missing)}");
            }

            if (extra.Length > 0)
            {
                problems.Add($"unknown flagIds: {string.Join(", ", extra)}");
            }

            throw Error(
                missing.Length > 0
                    ? LocalizationGenerationErrorCode.MissingMapping
                    : LocalizationGenerationErrorCode.ExtraMapping,
                $"Mapping coverage does not match source bosses ({string.Join("; ", problems)}).");
        }

        return mappings;
    }

    private static ResolvedText ResolveRule(
        LocalizationRule rule,
        IReadOnlyDictionary<long, OfficialDisplayText> catalog,
        uint flagId,
        string propertyName,
        bool allowComposite)
    {
        string resolution = rule.Resolution ?? string.Empty;

        return resolution switch
        {
            "exact" => ResolveSingleRule(rule, catalog, flagId, propertyName, exact: true),
            "same-display-ambiguous" => ResolveSingleRule(rule, catalog, flagId, propertyName, exact: false),
            "composite-reviewed" when allowComposite => ResolveComposite(rule, catalog, flagId, propertyName),
            "composite-reviewed" => throw Error(
                LocalizationGenerationErrorCode.InvalidResolution,
                $"{propertyName} for flagId {flagId} cannot be composite-reviewed.",
                flagId),
            _ => throw Error(
                LocalizationGenerationErrorCode.InvalidResolution,
                $"{propertyName} for flagId {flagId} has unsupported resolution '{resolution}'.",
                flagId),
        };
    }

    private static ResolvedText ResolveSingleRule(
        LocalizationRule rule,
        IReadOnlyDictionary<long, OfficialDisplayText> catalog,
        uint flagId,
        string propertyName,
        bool exact)
    {
        long[] textIds = rule.TextIds ?? [];
        int requiredMinimum = exact ? 1 : 2;
        bool invalidCount = exact ? textIds.Length != 1 : textIds.Length < requiredMinimum;

        if (invalidCount)
        {
            string expectation = exact ? "exactly one" : "at least two";
            throw Error(
                LocalizationGenerationErrorCode.InvalidTextIdCount,
                $"{propertyName} for flagId {flagId} must contain {expectation} textId for resolution '{rule.Resolution}'.",
                flagId);
        }

        if (textIds.Distinct().Count() != textIds.Length)
        {
            throw Error(
                LocalizationGenerationErrorCode.InvalidTextIdCount,
                $"{propertyName} for flagId {flagId} contains duplicate textIds.",
                flagId);
        }

        if (!string.IsNullOrEmpty(rule.Composition) || rule.Components is { Length: > 0 })
        {
            throw Error(
                LocalizationGenerationErrorCode.InvalidResolution,
                $"{propertyName} for flagId {flagId} cannot include composite fields for resolution '{rule.Resolution}'.",
                flagId);
        }

        var candidates = new OfficialDisplayText[textIds.Length];

        for (int index = 0; index < textIds.Length; index++)
        {
            long textId = textIds[index];

            if (!catalog.TryGetValue(textId, out OfficialDisplayText? text) || text is null)
            {
                throw Error(
                    LocalizationGenerationErrorCode.MissingTextId,
                    $"{propertyName} for flagId {flagId} references missing textId {textId}.",
                    flagId);
            }

            if (string.IsNullOrWhiteSpace(text.Ja) || string.IsNullOrWhiteSpace(text.En))
            {
                throw Error(
                    LocalizationGenerationErrorCode.EmptyOfficialText,
                    $"{propertyName} for flagId {flagId}, textId {textId}, has an empty JA or EN value.",
                    flagId);
            }

            candidates[index] = text;
        }

        OfficialDisplayText first = candidates[0];

        if (!exact && candidates.Skip(1).Any(candidate =>
                !string.Equals(candidate.Ja, first.Ja, StringComparison.Ordinal) ||
                !string.Equals(candidate.En, first.En, StringComparison.Ordinal)))
        {
            throw Error(
                LocalizationGenerationErrorCode.AmbiguousDisplayMismatch,
                $"{propertyName} for flagId {flagId} has same-display candidates with different JA or EN values.",
                flagId);
        }

        return new ResolvedText(first.Ja!, first.En!);
    }

    private static ResolvedText ResolveComposite(
        LocalizationRule rule,
        IReadOnlyDictionary<long, OfficialDisplayText> catalog,
        uint flagId,
        string propertyName)
    {
        if (!string.Equals(rule.Composition, "join", StringComparison.Ordinal))
        {
            throw Error(
                LocalizationGenerationErrorCode.InvalidComposite,
                $"{propertyName} for flagId {flagId} must use composition 'join'.",
                flagId);
        }

        if (rule.TextIds is { Length: > 0 })
        {
            throw Error(
                LocalizationGenerationErrorCode.InvalidComposite,
                $"{propertyName} for flagId {flagId} must not use flat textIds for a composite.",
                flagId);
        }

        LocalizationRule?[] components = rule.Components ?? [];

        if (components.Length < 2)
        {
            throw Error(
                LocalizationGenerationErrorCode.InvalidComposite,
                $"{propertyName} for flagId {flagId} must contain at least two components.",
                flagId);
        }

        var resolved = new ResolvedText[components.Length];

        for (int index = 0; index < components.Length; index++)
        {
            LocalizationRule component = components[index] ??
                throw Error(
                    LocalizationGenerationErrorCode.InvalidComposite,
                    $"{propertyName} for flagId {flagId} contains a null component at index {index}.",
                    flagId);

            if (component.Resolution is not ("exact" or "same-display-ambiguous"))
            {
                throw Error(
                    LocalizationGenerationErrorCode.InvalidComposite,
                    $"{propertyName} for flagId {flagId}, component {index}, must be exact or same-display-ambiguous.",
                    flagId);
            }

            resolved[index] = ResolveSingleRule(
                component,
                catalog,
                flagId,
                $"{propertyName}.components[{index}]",
                exact: component.Resolution == "exact");
        }

        return new ResolvedText(
            string.Join(JapaneseSeparator, resolved.Select(static text => text.Ja)),
            string.Join(EnglishSeparator, resolved.Select(static text => text.En)));
    }

    private static ComparisonEntry CreateComparisonEntry(
        BossDefinition source,
        GeneratedBossDefinition generated,
        LocalizationRule npcRule,
        LocalizationRule placeRule)
    {
        var changed = new List<string>();
        Compare(changed, "id", source.Id, generated.Id);
        Compare(changed, "flagId", source.FlagId, generated.FlagId);
        Compare(changed, "nameEn", source.NameEn, generated.NameEn);
        Compare(changed, "nameJa", source.NameJa, generated.NameJa);
        Compare(changed, "regionId", source.RegionId, generated.RegionId);
        Compare(changed, "regionEn", source.RegionEn, generated.RegionEn);
        Compare(changed, "regionJa", source.RegionJa, generated.RegionJa);
        Compare(changed, "locationEn", source.LocationEn, generated.LocationEn);
        Compare(changed, "locationJa", source.LocationJa, generated.LocationJa);
        Compare(changed, "content", ToJsonContent(source.Content), generated.Content);
        Compare(changed, "sortOrder", source.SortOrder, generated.SortOrder);

        string[] expected = changed.Where(ExpectedLocalizedFields.Contains).ToArray();
        string[] unexpected = changed.Where(field => !ExpectedLocalizedFields.Contains(field)).ToArray();
        IReadOnlyList<ResolutionCandidateSet> npcCandidates = npcRule.Resolution == "composite-reviewed"
            ? npcRule.Components!
                .Select(component => new ResolutionCandidateSet(component!.Resolution!, component.TextIds!))
                .ToArray()
            : [new ResolutionCandidateSet(npcRule.Resolution!, npcRule.TextIds!)];

        return new ComparisonEntry(
            source.FlagId,
            source.SortOrder,
            source.NameJa,
            generated.NameJa,
            source.NameEn,
            generated.NameEn,
            npcRule.Resolution!,
            npcCandidates,
            source.LocationJa,
            generated.LocationJa,
            source.LocationEn,
            generated.LocationEn,
            placeRule.Resolution!,
            placeRule.TextIds!,
            changed,
            expected,
            unexpected);
    }

    private static void Compare<T>(ICollection<string> changed, string field, T oldValue, T newValue)
    {
        if (!EqualityComparer<T>.Default.Equals(oldValue, newValue))
        {
            changed.Add(field);
        }
    }

    private static Dictionary<string, int> CreateResolutionCounts(bool includeComposite = true)
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["exact"] = 0,
            ["same-display-ambiguous"] = 0,
        };

        if (includeComposite)
        {
            counts["composite-reviewed"] = 0;
        }

        return counts;
    }

    private static string ToJsonContent(GameContent content) => content switch
    {
        GameContent.BaseGame => "baseGame",
        GameContent.ShadowOfTheErdtree => "shadowOfTheErdtree",
        _ => throw new ArgumentOutOfRangeException(nameof(content), content, "Unsupported game content."),
    };

    private static void RequireEqual(string? actual, string expected, string label)
    {
        if (!string.Equals(actual, expected, StringComparison.Ordinal))
        {
            throw Error(
                LocalizationGenerationErrorCode.InvalidManifest,
                $"{label} must be '{expected}', found '{actual ?? "<null>"}'.");
        }
    }

    private static LocalizationGenerationException Error(
        LocalizationGenerationErrorCode code,
        string message,
        uint? flagId = null) => new(code, message, flagId);

    private sealed record OfficialDisplayText(string? Ja, string? En);

    private sealed record ResolvedText(string Ja, string En);

    private sealed record ValidatedManifest(
        string Game,
        string AppVersion,
        string RegulationVersion,
        string ToolVersion,
        ManifestTable NpcTable,
        ManifestTable PlaceTable);
}
