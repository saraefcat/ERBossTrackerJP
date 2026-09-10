using ERBossTrackerJP.Core.Models;

namespace ERBossTrackerJP.Core.Bosses;

public static class EmbeddedBossDefinitions
{
    private const string ResourceName = "ERBossTrackerJP.Core.Data.bosses.json";

    public static IReadOnlyList<BossDefinition> Load()
    {
        using Stream stream = typeof(EmbeddedBossDefinitions).Assembly
            .GetManifestResourceStream(ResourceName) ??
            throw new InvalidOperationException(
                $"Embedded boss data resource '{ResourceName}' was not found.");
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);

        return new BossDefinitionLoader().Read(
            buffer.ToArray(),
            BossDefinitionLoader.ProductionBossCount);
    }
}
