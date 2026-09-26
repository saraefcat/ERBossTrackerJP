using System.Text.Json;
using ERBossTrackerJP.Core.Bosses;
using ERBossTrackerJP.DataGenerator.Generation;
using ERBossTrackerJP.DataGenerator.IO;
using ERBossTrackerJP.DataGenerator.Models;

namespace ERBossTrackerJP.Tests.DataGenerator;

public sealed class OfficialLocalizationGeneratorTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    [Fact]
    public void ProductionOutputGuard_RejectsProductionPathForEitherDestination()
    {
        string root = Path.Combine(Path.GetTempPath(), $"ERBossTrackerJP.Guard-{Guid.NewGuid():N}");
        string sourceCopy = Path.Combine(root, "input", "bosses.json");
        string production = Path.Combine(root, "src", "ERBossTrackerJP.Core", "Data", "bosses.json");
        string safeOutput = Path.Combine(root, "artifacts", "bosses.generated.json");
        string safeReport = Path.Combine(root, "artifacts", "comparison-report.json");

        Assert.Equal(
            LocalizationGenerationErrorCode.InvalidArgument,
            Assert.Throws<LocalizationGenerationException>(() =>
                ProductionOutputGuard.EnsureSafe(sourceCopy, production, safeReport)).ErrorCode);
        Assert.Equal(
            LocalizationGenerationErrorCode.InvalidArgument,
            Assert.Throws<LocalizationGenerationException>(() =>
                ProductionOutputGuard.EnsureSafe(sourceCopy, safeOutput, production)).ErrorCode);
        Assert.Equal(
            LocalizationGenerationErrorCode.InvalidArgument,
            Assert.Throws<LocalizationGenerationException>(() =>
                ProductionOutputGuard.EnsureSafe(sourceCopy, sourceCopy, safeReport)).ErrorCode);

        ProductionOutputGuard.EnsureSafe(sourceCopy, safeOutput, safeReport);
    }

    [Fact]
    public void GenerationOutputWriter_InvalidRuntimeSchema_LeavesNoOutputFiles()
    {
        GenerationResult valid = GenerateSingle(
            1,
            Exact(10),
            Exact(20),
            [Official(10, "公式名", "Official Name", "NpcName")],
            [Official(20, "公式場所", "Official Place", "PlaceName")]);
        GeneratedBossDefinition invalidBoss = valid.BossDocument.Bosses[0] with { Id = "INVALID ID" };
        GenerationResult invalid = valid with
        {
            BossDocument = new GeneratedBossDocument(1, [invalidBoss]),
        };
        using var files = new GeneratorFiles();

        LocalizationGenerationException exception = Assert.Throws<LocalizationGenerationException>(() =>
            GenerationOutputWriter.Write(invalid, files.OutputPath, files.ReportPath));

        Assert.Equal(LocalizationGenerationErrorCode.InvalidGeneratedOutput, exception.ErrorCode);
        Assert.False(File.Exists(files.OutputPath));
        Assert.False(File.Exists(files.ReportPath));
    }

    [Fact]
    public void Generate_ReviewedMapping_CoversAll207Bosses()
    {
        string repositoryRoot = FindRepositoryRoot();
        string sourcePath = Path.Combine(repositoryRoot, "src", "ERBossTrackerJP.Core", "Data", "bosses.json");
        string mappingPath = Path.Combine(
            repositoryRoot,
            "tools",
            "ERBossTrackerJP.DataGenerator",
            "Data",
            "boss-localization-map.json");
        LocalizationMappingDocument mapping = ReadMapping(mappingPath);

        using var files = new GeneratorFiles();
        files.WriteOfficialInputs(
            CreateEntries(CollectTextIds(mapping.Entries!, static entry => entry!.NpcName!), "NpcName", "公式ボス", "Official Boss"),
            CreateEntries(CollectTextIds(mapping.Entries!, static entry => entry!.PlaceName!), "PlaceName", "公式の場所", "Official Place"));

        GenerationResult result = new OfficialLocalizationGenerator().Generate(new GenerationRequest(
            sourcePath,
            mappingPath,
            files.NpcPath,
            files.PlacePath,
            files.ManifestPath));

        Assert.Equal(207, result.BossDocument.Bosses.Count);
        Assert.Equal(207, result.ComparisonReport.Summary.MappingCount);
        Assert.Equal(82, result.ComparisonReport.Summary.NpcResolutionCounts["exact"]);
        Assert.Equal(99, result.ComparisonReport.Summary.NpcResolutionCounts["same-display-ambiguous"]);
        Assert.Equal(26, result.ComparisonReport.Summary.NpcResolutionCounts["composite-reviewed"]);
        Assert.Equal(47, result.ComparisonReport.Summary.PlaceResolutionCounts["exact"]);
        Assert.Equal(160, result.ComparisonReport.Summary.PlaceResolutionCounts["same-display-ambiguous"]);
        Assert.Equal(0, result.ComparisonReport.Summary.UnexpectedDiffCount);

        GenerationOutputWriter.Write(result, files.OutputPath, files.ReportPath);
        Assert.Equal(
            207,
            new BossDefinitionLoader().Read(File.ReadAllBytes(files.OutputPath), expectedBossCount: 207).Count);
    }

    [Fact]
    public void Generate_ExactRule_ResolvesTextAndPreservesMetadata()
    {
        LocalizationRule npcRule = Exact(10);
        LocalizationRule placeRule = Exact(20);
        GenerationResult result = GenerateSingle(
            1,
            npcRule,
            placeRule,
            [Official(10, "公式名", "Official Name", "NpcName")],
            [Official(20, "公式場所", "Official Place", "PlaceName")]);

        GeneratedBossDefinition boss = Assert.Single(result.BossDocument.Bosses);
        Assert.Equal("test.boss", boss.Id);
        Assert.Equal((uint)1, boss.FlagId);
        Assert.Equal("region", boss.RegionId);
        Assert.Equal("Region", boss.RegionEn);
        Assert.Equal("地域", boss.RegionJa);
        Assert.Equal("baseGame", boss.Content);
        Assert.Equal(0, boss.SortOrder);
        Assert.Equal("公式名", boss.NameJa);
        Assert.Equal("Official Name", boss.NameEn);
        Assert.Equal("公式場所", boss.LocationJa);
        Assert.Equal("Official Place", boss.LocationEn);
    }

    [Fact]
    public void Generate_ExactRuleWithMultipleTextIds_Fails()
    {
        LocalizationGenerationException exception = Assert.Throws<LocalizationGenerationException>(() =>
            GenerateSingle(
                1,
                new LocalizationRule { Resolution = "exact", TextIds = [10, 11] },
                Exact(20),
                [Official(10, "名", "Name", "NpcName"), Official(11, "名", "Name", "NpcName")],
                [Official(20, "場所", "Place", "PlaceName")]));

        Assert.Equal(LocalizationGenerationErrorCode.InvalidTextIdCount, exception.ErrorCode);
    }

    [Fact]
    public void Generate_ExactRuleWithEmptyOfficialText_Fails()
    {
        LocalizationGenerationException exception = Assert.Throws<LocalizationGenerationException>(() =>
            GenerateSingle(
                1,
                Exact(10),
                Exact(20),
                [Official(10, "名", string.Empty, "NpcName")],
                [Official(20, "場所", "Place", "PlaceName")]));

        Assert.Equal(LocalizationGenerationErrorCode.EmptyOfficialText, exception.ErrorCode);
    }

    [Fact]
    public void Generate_SameDisplayAmbiguousRule_RequiresIdenticalJaAndEn()
    {
        LocalizationRule rule = Ambiguous(10, 11);
        GenerationResult result = GenerateSingle(
            1,
            rule,
            Exact(20),
            [Official(10, "同名", "Same Name", "NpcName"), Official(11, "同名", "Same Name", "NpcName")],
            [Official(20, "場所", "Place", "PlaceName")]);
        Assert.Equal("同名", Assert.Single(result.BossDocument.Bosses).NameJa);

        LocalizationGenerationException exception = Assert.Throws<LocalizationGenerationException>(() =>
            GenerateSingle(
                1,
                rule,
                Exact(20),
                [Official(10, "同名", "Same Name", "NpcName"), Official(11, "別名", "Same Name", "NpcName")],
                [Official(20, "場所", "Place", "PlaceName")]));

        Assert.Equal(LocalizationGenerationErrorCode.AmbiguousDisplayMismatch, exception.ErrorCode);

        LocalizationGenerationException englishMismatch = Assert.Throws<LocalizationGenerationException>(() =>
            GenerateSingle(
                1,
                rule,
                Exact(20),
                [Official(10, "同名", "Same Name", "NpcName"), Official(11, "同名", "Different Name", "NpcName")],
                [Official(20, "場所", "Place", "PlaceName")]));

        Assert.Equal(LocalizationGenerationErrorCode.AmbiguousDisplayMismatch, englishMismatch.ErrorCode);
    }

    [Fact]
    public void Generate_CompositeReviewed_PreservesComponentOrderAndSeparators()
    {
        var composite = new LocalizationRule
        {
            Resolution = "composite-reviewed",
            Composition = "join",
            Components = [Exact(10), Exact(11)],
        };

        GenerationResult result = GenerateSingle(
            1,
            composite,
            Exact(20),
            [Official(10, "甲", "Alpha", "NpcName"), Official(11, "乙", "Beta", "NpcName")],
            [Official(20, "場所", "Place", "PlaceName")]);

        GeneratedBossDefinition boss = Assert.Single(result.BossDocument.Bosses);
        Assert.Equal("甲＆乙", boss.NameJa);
        Assert.Equal("Alpha & Beta", boss.NameEn);
    }

    [Fact]
    public void Generate_MissingMappedTextId_Fails()
    {
        LocalizationGenerationException exception = Assert.Throws<LocalizationGenerationException>(() =>
            GenerateSingle(
                1,
                Exact(999),
                Exact(20),
                [Official(10, "名", "Name", "NpcName")],
                [Official(20, "場所", "Place", "PlaceName")]));

        Assert.Equal(LocalizationGenerationErrorCode.MissingTextId, exception.ErrorCode);
    }

    [Fact]
    public void Generate_MappingCoverageMismatch_ReportsMissingAndExtraFlagIds()
    {
        using var files = new GeneratorFiles();
        files.WriteSource(1);
        files.WriteMapping(2, Exact(10), Exact(20));
        files.WriteOfficialInputs(
            [Official(10, "名", "Name", "NpcName")],
            [Official(20, "場所", "Place", "PlaceName")]);

        LocalizationGenerationException exception = Assert.Throws<LocalizationGenerationException>(() =>
            new OfficialLocalizationGenerator().Generate(new GenerationRequest(
                files.SourcePath,
                files.MappingPath,
                files.NpcPath,
                files.PlacePath,
                files.ManifestPath,
                ExpectedBossCount: 1)));

        Assert.Equal(LocalizationGenerationErrorCode.MissingMapping, exception.ErrorCode);
        Assert.Contains("missing flagIds: 1", exception.Message, StringComparison.Ordinal);
        Assert.Contains("unknown flagIds: 2", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(2046410800u, true, "孤牢の騎士", "Knight of the Solitary Gaol")]
    [InlineData(31040800u, true, "マレニアの貴腐騎士", "Cleanrot Knight")]
    [InlineData(19000800u, false, "壊れかけのマリカ", "Fractured Marika")]
    [InlineData(1043330800u, false, "小黄金樹", "Minor Erdtree")]
    public void Generate_ReviewedSpecialMappings_ResolveExpectedOfficialDisplay(
        uint flagId,
        bool verifyNpc,
        string expectedJa,
        string expectedEn)
    {
        LocalizationMappingEntry reviewed = FindReviewedEntry(flagId);
        LocalizationRule npcRule = reviewed.NpcName!;
        LocalizationRule placeRule = reviewed.PlaceName!;
        string npcJa = verifyNpc ? expectedJa : "公式ボス";
        string npcEn = verifyNpc ? expectedEn : "Official Boss";
        string placeJa = verifyNpc ? "公式場所" : expectedJa;
        string placeEn = verifyNpc ? "Official Place" : expectedEn;

        GenerationResult result = GenerateSingle(
            flagId,
            npcRule,
            placeRule,
            CreateEntries(CollectTextIds(npcRule), "NpcName", npcJa, npcEn),
            CreateEntries(CollectTextIds(placeRule), "PlaceName", placeJa, placeEn));

        GeneratedBossDefinition boss = Assert.Single(result.BossDocument.Bosses);
        Assert.Equal(expectedJa, verifyNpc ? boss.NameJa : boss.LocationJa);
        Assert.Equal(expectedEn, verifyNpc ? boss.NameEn : boss.LocationEn);
    }

    private static GenerationResult GenerateSingle(
        uint flagId,
        LocalizationRule npcRule,
        LocalizationRule placeRule,
        IReadOnlyList<OfficialTextEntry> npcEntries,
        IReadOnlyList<OfficialTextEntry> placeEntries)
    {
        using var files = new GeneratorFiles();
        files.WriteSource(flagId);
        files.WriteMapping(flagId, npcRule, placeRule);
        files.WriteOfficialInputs(npcEntries, placeEntries);

        return new OfficialLocalizationGenerator().Generate(new GenerationRequest(
            files.SourcePath,
            files.MappingPath,
            files.NpcPath,
            files.PlacePath,
            files.ManifestPath,
            ExpectedBossCount: 1));
    }

    private static LocalizationMappingEntry FindReviewedEntry(uint flagId)
    {
        string path = Path.Combine(
            FindRepositoryRoot(),
            "tools",
            "ERBossTrackerJP.DataGenerator",
            "Data",
            "boss-localization-map.json");
        return ReadMapping(path).Entries!.Single(entry => entry!.FlagId == flagId)!;
    }

    private static LocalizationMappingDocument ReadMapping(string path) =>
        JsonSerializer.Deserialize<LocalizationMappingDocument>(File.ReadAllText(path), JsonOptions)
        ?? throw new InvalidOperationException("Reviewed mapping could not be read.");

    private static HashSet<long> CollectTextIds(
        IEnumerable<LocalizationMappingEntry?> entries,
        Func<LocalizationMappingEntry?, LocalizationRule> selector)
    {
        var ids = new HashSet<long>();
        foreach (LocalizationMappingEntry? entry in entries)
        {
            CollectTextIds(selector(entry), ids);
        }

        return ids;
    }

    private static HashSet<long> CollectTextIds(LocalizationRule rule)
    {
        var ids = new HashSet<long>();
        CollectTextIds(rule, ids);
        return ids;
    }

    private static void CollectTextIds(LocalizationRule rule, ISet<long> ids)
    {
        foreach (long id in rule.TextIds ?? [])
        {
            ids.Add(id);
        }

        foreach (LocalizationRule? component in rule.Components ?? [])
        {
            CollectTextIds(component!, ids);
        }
    }

    private static OfficialTextEntry[] CreateEntries(
        IEnumerable<long> ids,
        string table,
        string ja,
        string en) => ids.Order().Select(id => Official(id, ja, en, table)).ToArray();

    private static OfficialTextEntry Official(long id, string ja, string en, string table) => new()
    {
        TextId = id,
        Ja = ja,
        En = en,
        Table = table,
        JaSourcePath = $"jpnjp/{table}.fmg",
        EnSourcePath = $"engus/{table}.fmg",
    };

    private static LocalizationRule Exact(long id) => new()
    {
        Resolution = "exact",
        TextIds = [id],
    };

    private static LocalizationRule Ambiguous(params long[] ids) => new()
    {
        Resolution = "same-display-ambiguous",
        TextIds = ids,
    };

    private static string FindRepositoryRoot()
    {
        for (DirectoryInfo? directory = new(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "ERBossTrackerJP.sln")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("ERBossTrackerJP repository root was not found.");
    }

    private sealed class GeneratorFiles : IDisposable
    {
        public GeneratorFiles()
        {
            DirectoryPath = Path.Combine(Path.GetTempPath(), $"ERBossTrackerJP.DataGenerator.Tests-{Guid.NewGuid():N}");
            Directory.CreateDirectory(DirectoryPath);
        }

        public string DirectoryPath { get; }

        public string SourcePath => Path.Combine(DirectoryPath, "bosses.json");

        public string MappingPath => Path.Combine(DirectoryPath, "mapping.json");

        public string NpcPath => Path.Combine(DirectoryPath, "npc-names.json");

        public string PlacePath => Path.Combine(DirectoryPath, "place-names.json");

        public string ManifestPath => Path.Combine(DirectoryPath, "localization.manifest.json");

        public string OutputPath => Path.Combine(DirectoryPath, "bosses.generated.json");

        public string ReportPath => Path.Combine(DirectoryPath, "comparison-report.json");

        public void WriteSource(uint flagId)
        {
            var source = new
            {
                schemaVersion = 1,
                bosses = new[]
                {
                    new
                    {
                        id = "test.boss",
                        flagId,
                        nameEn = "Old Name",
                        nameJa = "旧名",
                        regionId = "region",
                        regionEn = "Region",
                        regionJa = "地域",
                        locationEn = "Old Place",
                        locationJa = "旧場所",
                        content = "baseGame",
                        sortOrder = 0,
                    },
                },
            };
            WriteJson(SourcePath, source);
        }

        public void WriteMapping(uint flagId, LocalizationRule npcRule, LocalizationRule placeRule)
        {
            var mapping = new LocalizationMappingDocument
            {
                SchemaVersion = 1,
                Source = new MappingSource
                {
                    Game = "EldenRing",
                    AppVersion = "1.17",
                    RegulationVersion = "1.17",
                },
                LanguageSeparators = new LanguageSeparators { Ja = "＆", En = " & " },
                Entries =
                [
                    new LocalizationMappingEntry
                    {
                        FlagId = flagId,
                        NpcName = npcRule,
                        PlaceName = placeRule,
                    },
                ],
            };
            WriteJson(MappingPath, mapping);
        }

        public void WriteOfficialInputs(
            IReadOnlyList<OfficialTextEntry> npcEntries,
            IReadOnlyList<OfficialTextEntry> placeEntries)
        {
            WriteJson(NpcPath, OfficialDocument("NpcName", npcEntries));
            WriteJson(PlacePath, OfficialDocument("PlaceName", placeEntries));
            WriteJson(ManifestPath, new LocalizationManifest
            {
                SchemaVersion = 1,
                ToolVersion = "test",
                Game = "EldenRing",
                AppVersion = "1.17",
                RegulationVersion = "1.17",
                Languages = ["jpnjp", "engus"],
                Tables =
                [
                    Manifest("NpcName", "npc-names.json", npcEntries),
                    Manifest("PlaceName", "place-names.json", placeEntries),
                ],
            });
        }

        public void Dispose()
        {
            if (Directory.Exists(DirectoryPath))
            {
                Directory.Delete(DirectoryPath, recursive: true);
            }
        }

        private static OfficialTextDocument OfficialDocument(
            string table,
            IReadOnlyList<OfficialTextEntry> entries) => new()
            {
                SchemaVersion = 1,
                Game = "EldenRing",
                AppVersion = "1.17",
                RegulationVersion = "1.17",
                Table = table,
                Entries = entries.Cast<OfficialTextEntry?>().ToArray(),
            };

        private static ManifestTable Manifest(
            string table,
            string file,
            IReadOnlyList<OfficialTextEntry> entries) => new()
            {
                Table = table,
                File = file,
                EntryCount = entries.Count,
                MissingJaCount = entries.Count(entry => string.IsNullOrWhiteSpace(entry.Ja)),
                MissingEnCount = entries.Count(entry => string.IsNullOrWhiteSpace(entry.En)),
                DuplicateJaOverrideCount = 0,
                DuplicateEnOverrideCount = 0,
            };

        private static void WriteJson<T>(string path, T value) =>
            File.WriteAllText(path, JsonSerializer.Serialize(value, JsonOptions));
    }
}
