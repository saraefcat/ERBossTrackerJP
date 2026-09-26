using ERBossTrackerJP.DataGenerator.Generation;
using ERBossTrackerJP.DataGenerator.IO;
using ERBossTrackerJP.DataGenerator.Models;

return Run(args);

static int Run(string[] args)
{
    if (args.Contains("--help", StringComparer.Ordinal) || args.Contains("-h", StringComparer.Ordinal))
    {
        PrintUsage();
        return 0;
    }

    try
    {
        IReadOnlyDictionary<string, string> options = ParseOptions(args);
        string sourcePath = ReadRequired(options, "--source");
        string mappingPath = ReadRequired(options, "--mapping");
        string officialTextDirectory = Path.GetFullPath(ReadRequired(options, "--official-text-directory"));
        string outputPath = Path.GetFullPath(ReadRequired(options, "--output"));
        string reportPath = Path.GetFullPath(ReadRequired(options, "--report"));
        ProductionOutputGuard.EnsureSafe(sourcePath, outputPath, reportPath);

        var request = new GenerationRequest(
            SourceBossesPath: sourcePath,
            MappingPath: mappingPath,
            NpcNamesPath: Path.Combine(officialTextDirectory, "npc-names.json"),
            PlaceNamesPath: Path.Combine(officialTextDirectory, "place-names.json"),
            ManifestPath: Path.Combine(officialTextDirectory, "localization.manifest.json"));
        var generator = new OfficialLocalizationGenerator();
        GenerationResult result = generator.Generate(request);
        GenerationOutputWriter.Write(result, outputPath, reportPath);

        ComparisonSummary summary = result.ComparisonReport.Summary;
        Console.WriteLine($"生成完了: {Path.GetFullPath(outputPath)}");
        Console.WriteLine($"比較レポート: {Path.GetFullPath(reportPath)}");
        Console.WriteLine($"ボス: {summary.BossCount}, mapping: {summary.MappingCount}");
        Console.WriteLine(
            $"NpcName: exact={summary.NpcResolutionCounts["exact"]}, " +
            $"same-display-ambiguous={summary.NpcResolutionCounts["same-display-ambiguous"]}, " +
            $"composite-reviewed={summary.NpcResolutionCounts["composite-reviewed"]}");
        Console.WriteLine(
            $"PlaceName: exact={summary.PlaceResolutionCounts["exact"]}, " +
            $"same-display-ambiguous={summary.PlaceResolutionCounts["same-display-ambiguous"]}");
        Console.WriteLine(
            $"expected diff={summary.ExpectedDiffCount}, unexpected diff={summary.UnexpectedDiffCount}, " +
            $"unresolved={summary.UnresolvedCount}, fallback={summary.FallbackCount}, warning={summary.WarningCount}");
        return 0;
    }
    catch (LocalizationGenerationException exception)
    {
        Console.Error.WriteLine($"生成失敗 [{exception.ErrorCode}]: {exception.Message}");
        return 1;
    }
    catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
    {
        Console.Error.WriteLine($"入出力エラー: {exception.Message}");
        return 1;
    }
}

static IReadOnlyDictionary<string, string> ParseOptions(string[] args)
{
    var supported = new HashSet<string>(StringComparer.Ordinal)
    {
        "--source",
        "--mapping",
        "--official-text-directory",
        "--output",
        "--report",
    };
    var options = new Dictionary<string, string>(StringComparer.Ordinal);

    for (int index = 0; index < args.Length; index += 2)
    {
        string option = args[index];

        if (!supported.Contains(option))
        {
            throw new LocalizationGenerationException(
                LocalizationGenerationErrorCode.InvalidArgument,
                $"未対応の引数です: {option}");
        }

        if (index + 1 >= args.Length || args[index + 1].StartsWith("--", StringComparison.Ordinal))
        {
            throw new LocalizationGenerationException(
                LocalizationGenerationErrorCode.InvalidArgument,
                $"引数の値がありません: {option}");
        }

        if (!options.TryAdd(option, args[index + 1]))
        {
            throw new LocalizationGenerationException(
                LocalizationGenerationErrorCode.InvalidArgument,
                $"引数が重複しています: {option}");
        }
    }

    return options;
}

static string ReadRequired(IReadOnlyDictionary<string, string> options, string name)
{
    if (!options.TryGetValue(name, out string? value) || string.IsNullOrWhiteSpace(value))
    {
        throw new LocalizationGenerationException(
            LocalizationGenerationErrorCode.InvalidArgument,
            $"必須引数がありません: {name}");
    }

    return value;
}

static void PrintUsage()
{
    Console.WriteLine(
        """
        ERBossTrackerJP 公式ローカライズ DataGenerator

        使用方法:
          dotnet run --project tools/ERBossTrackerJP.DataGenerator -- \
            --source <現行bosses.json> \
            --mapping <boss-localization-map.json> \
            --official-text-directory <公式テキストJSONのフォルダー> \
            --output <生成bosses.json> \
            --report <comparison-report.json>

        PR1では --output と --report にproductionのbosses.jsonと異なるパスを指定してください。
        """);
}
