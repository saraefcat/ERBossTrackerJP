using System.Security.Cryptography;
using ERBossTrackerJP.Save.Exceptions;

namespace ERBossTrackerJP.Save.EventFlags;

public static class EmbeddedEventFlagBlockMap
{
    public const int ExpectedMappingCount = 11_920;
    public const string SourceSha256 =
        "092C3B73B7049D04087DA425544396BC93ED9B1F80EE3BB3C2E4734C8900B728";

    private const string ResourceName = "ERBossTrackerJP.Save.Data.eventflag_bst.txt";

    private static readonly Lazy<IReadOnlyDictionary<uint, uint>> LazyMappings =
        new(LoadCore, LazyThreadSafetyMode.ExecutionAndPublication);

    public static IReadOnlyDictionary<uint, uint> Load() => LazyMappings.Value;

    private static IReadOnlyDictionary<uint, uint> LoadCore()
    {
        using Stream resource = typeof(EmbeddedEventFlagBlockMap).Assembly
            .GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{ResourceName}' was not found.");
        using var buffer = new MemoryStream();
        resource.CopyTo(buffer);
        byte[] bytes = buffer.ToArray();
        string actualHash = Convert.ToHexString(SHA256.HashData(bytes));

        if (!string.Equals(actualHash, SourceSha256, StringComparison.Ordinal))
        {
            throw new SaveParseException(
                SaveParseErrorCode.InvalidEventFlagBlockMap,
                $"The embedded event flag block map hash is {actualHash}; expected {SourceSha256}.");
        }

        using var data = new MemoryStream(bytes, writable: false);
        IReadOnlyDictionary<uint, uint> mappings = new EventFlagBlockMapReader().Read(data);

        if (mappings.Count != ExpectedMappingCount)
        {
            throw new SaveParseException(
                SaveParseErrorCode.InvalidEventFlagBlockMap,
                $"The embedded event flag block map contains {mappings.Count} entries; expected {ExpectedMappingCount}.");
        }

        return mappings;
    }
}
