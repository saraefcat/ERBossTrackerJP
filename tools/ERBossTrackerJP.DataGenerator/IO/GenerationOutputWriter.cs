using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using ERBossTrackerJP.Core.Bosses;
using ERBossTrackerJP.DataGenerator.Generation;

namespace ERBossTrackerJP.DataGenerator.IO;

public static class GenerationOutputWriter
{
    private static readonly UTF8Encoding Utf8WithoutBom = new(false);

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static void Write(GenerationResult result, string outputPath, string reportPath)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(reportPath);

        string resolvedOutputPath = Path.GetFullPath(outputPath);
        string resolvedReportPath = Path.GetFullPath(reportPath);

        if (string.Equals(resolvedOutputPath, resolvedReportPath, StringComparison.OrdinalIgnoreCase))
        {
            throw new LocalizationGenerationException(
                LocalizationGenerationErrorCode.InvalidArgument,
                "Generated boss data and comparison report must use different paths.");
        }

        string outputJson = SerializeWithLf(result.BossDocument);
        string reportJson = SerializeWithLf(result.ComparisonReport);
        ValidateRuntimeSchema(outputJson, result.BossDocument.Bosses.Count);

        WriteAtomic(resolvedOutputPath, outputJson);
        WriteAtomic(resolvedReportPath, reportJson);
    }

    private static string SerializeWithLf<T>(T value) =>
        JsonSerializer.Serialize(value, Options).ReplaceLineEndings("\n") + "\n";

    private static void ValidateRuntimeSchema(string outputJson, int expectedBossCount)
    {
        try
        {
            _ = new BossDefinitionLoader().Read(
                Utf8WithoutBom.GetBytes(outputJson),
                expectedBossCount);
        }
        catch (BossDataException exception)
        {
            throw new LocalizationGenerationException(
                LocalizationGenerationErrorCode.InvalidGeneratedOutput,
                $"Generated boss data does not conform to runtime schema v1: {exception.Message}",
                innerException: exception);
        }
    }

    private static void WriteAtomic(string path, string content)
    {
        string? directory = Path.GetDirectoryName(path);

        if (string.IsNullOrEmpty(directory))
        {
            throw new LocalizationGenerationException(
                LocalizationGenerationErrorCode.InvalidArgument,
                $"Output path has no directory: {path}");
        }

        Directory.CreateDirectory(directory);
        string temporaryPath = Path.Combine(
            directory,
            $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");

        try
        {
            File.WriteAllText(temporaryPath, content, Utf8WithoutBom);
            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }
}
