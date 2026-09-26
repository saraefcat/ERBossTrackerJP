namespace ERBossTrackerJP.DataGenerator.Generation;

public static class ProductionOutputGuard
{
    private static readonly string[] ProductionPathSegments =
    [
        "src",
        "ERBossTrackerJP.Core",
        "Data",
        "bosses.json",
    ];

    public static void EnsureSafe(string sourcePath, string outputPath, string reportPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(reportPath);

        string resolvedSourcePath = Path.GetFullPath(sourcePath);
        ValidateDestination(resolvedSourcePath, Path.GetFullPath(outputPath), "generated boss data");
        ValidateDestination(resolvedSourcePath, Path.GetFullPath(reportPath), "comparison report");
    }

    private static void ValidateDestination(string sourcePath, string destinationPath, string label)
    {
        if (string.Equals(sourcePath, destinationPath, StringComparison.OrdinalIgnoreCase) ||
            IsProductionBossesPath(destinationPath))
        {
            throw new LocalizationGenerationException(
                LocalizationGenerationErrorCode.InvalidArgument,
                $"PR1 {label} must not overwrite the production source bosses.json.");
        }
    }

    private static bool IsProductionBossesPath(string path)
    {
        string[] segments = path.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries);

        if (segments.Length < ProductionPathSegments.Length)
        {
            return false;
        }

        int offset = segments.Length - ProductionPathSegments.Length;

        for (int index = 0; index < ProductionPathSegments.Length; index++)
        {
            if (!string.Equals(
                    segments[offset + index],
                    ProductionPathSegments[index],
                    StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }
}
