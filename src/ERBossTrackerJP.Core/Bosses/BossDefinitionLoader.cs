using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ERBossTrackerJP.Core.Models;

namespace ERBossTrackerJP.Core.Bosses;

public sealed class BossDefinitionLoader
{
    public const int CurrentSchemaVersion = 1;
    public const int ProductionBossCount = 207;
    public const int MaximumBossCount = 1_000;
    public const int MaximumDocumentSize = 4 * 1024 * 1024;

    private static readonly Encoding StrictUtf8 =
        new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = false,
        AllowTrailingCommas = false,
        ReadCommentHandling = JsonCommentHandling.Disallow,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        MaxDepth = 16,
    };

    public IReadOnlyList<BossDefinition> Read(
        ReadOnlyMemory<byte> data,
        int? expectedBossCount = null)
    {
        if (expectedBossCount is <= 0 or > MaximumBossCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(expectedBossCount),
                expectedBossCount,
                $"Expected boss count must be between 1 and {MaximumBossCount}.");
        }

        if (data.IsEmpty)
        {
            throw new BossDataException(
                BossDataErrorCode.EmptyDocument,
                "The boss definition document is empty.");
        }

        if (data.Length > MaximumDocumentSize)
        {
            throw new BossDataException(
                BossDataErrorCode.DocumentTooLarge,
                $"The boss definition document contains {data.Length} bytes; the maximum is {MaximumDocumentSize}.");
        }

        ReadOnlySpan<byte> json = RemoveUtf8ByteOrderMark(data.Span);

        if (json.IsEmpty)
        {
            throw new BossDataException(
                BossDataErrorCode.EmptyDocument,
                "The boss definition document is empty.");
        }

        ValidateUtf8(json);
        BossDocumentDto document = Deserialize(json);

        if (document.SchemaVersion is null)
        {
            throw MissingRootProperty("schemaVersion");
        }

        if (document.SchemaVersion != CurrentSchemaVersion)
        {
            throw new BossDataException(
                BossDataErrorCode.UnsupportedSchemaVersion,
                $"Boss data schema version {document.SchemaVersion} is not supported; expected {CurrentSchemaVersion}.",
                propertyName: "schemaVersion");
        }

        BossDto?[] bosses = document.Bosses ??
            throw MissingRootProperty("bosses");

        if (bosses.Length == 0 || bosses.Length > MaximumBossCount ||
            expectedBossCount.HasValue && bosses.Length != expectedBossCount.Value)
        {
            string expectedText = expectedBossCount.HasValue
                ? $"exactly {expectedBossCount.Value}"
                : $"between 1 and {MaximumBossCount}";
            throw new BossDataException(
                BossDataErrorCode.InvalidBossCount,
                $"The boss definition document contains {bosses.Length} bosses; expected {expectedText}.",
                propertyName: "bosses");
        }

        var definitions = new BossDefinition[bosses.Length];
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var flagIds = new HashSet<uint>();
        var sortOrders = new HashSet<int>();
        var regions = new Dictionary<string, RegionDescriptor>(StringComparer.Ordinal);

        for (int index = 0; index < bosses.Length; index++)
        {
            BossDto boss = bosses[index] ??
                throw new BossDataException(
                    BossDataErrorCode.MissingRequiredField,
                    $"Boss entry {index} is null.",
                    index);
            definitions[index] = ValidateBoss(
                boss,
                index,
                ids,
                flagIds,
                sortOrders,
                regions);
        }

        Array.Sort(definitions, static (left, right) => left.SortOrder.CompareTo(right.SortOrder));
        return Array.AsReadOnly(definitions);
    }

    private static BossDefinition ValidateBoss(
        BossDto boss,
        int index,
        ISet<string> ids,
        ISet<uint> flagIds,
        ISet<int> sortOrders,
        IDictionary<string, RegionDescriptor> regions)
    {
        string id = ReadIdentifier(boss.Id, index, "id");
        string nameEn = ReadText(boss.NameEn, index, "nameEn");
        string nameJa = ReadText(boss.NameJa, index, "nameJa");
        string regionId = ReadIdentifier(boss.RegionId, index, "regionId");
        string regionEn = ReadText(boss.RegionEn, index, "regionEn");
        string regionJa = ReadText(boss.RegionJa, index, "regionJa");
        string locationEn = ReadText(boss.LocationEn, index, "locationEn");
        string locationJa = ReadText(boss.LocationJa, index, "locationJa");

        if (boss.FlagId is null or 0)
        {
            throw InvalidBoss(
                BossDataErrorCode.InvalidFlagId,
                index,
                "flagId",
                "Flag ID must be a positive unsigned integer.");
        }

        GameContent content = boss.Content switch
        {
            "baseGame" => GameContent.BaseGame,
            "shadowOfTheErdtree" => GameContent.ShadowOfTheErdtree,
            _ => throw InvalidBoss(
                BossDataErrorCode.InvalidContent,
                index,
                "content",
                "Content must be 'baseGame' or 'shadowOfTheErdtree'."),
        };

        if (boss.SortOrder is null or < 0)
        {
            throw InvalidBoss(
                BossDataErrorCode.InvalidSortOrder,
                index,
                "sortOrder",
                "Sort order must be a non-negative integer.");
        }

        if (!ids.Add(id))
        {
            throw InvalidBoss(
                BossDataErrorCode.DuplicateId,
                index,
                "id",
                $"Boss ID '{id}' is duplicated.");
        }

        if (!flagIds.Add(boss.FlagId.Value))
        {
            throw InvalidBoss(
                BossDataErrorCode.DuplicateFlagId,
                index,
                "flagId",
                $"Flag ID {boss.FlagId.Value} is duplicated.");
        }

        if (!sortOrders.Add(boss.SortOrder.Value))
        {
            throw InvalidBoss(
                BossDataErrorCode.DuplicateSortOrder,
                index,
                "sortOrder",
                $"Sort order {boss.SortOrder.Value} is duplicated.");
        }

        var region = new RegionDescriptor(regionEn, regionJa, content);

        if (regions.TryGetValue(regionId, out RegionDescriptor existingRegion) &&
            existingRegion != region)
        {
            throw InvalidBoss(
                BossDataErrorCode.InconsistentRegion,
                index,
                "regionId",
                $"Region '{regionId}' has inconsistent names or content.");
        }

        regions[regionId] = region;

        return new BossDefinition(
            id,
            boss.FlagId.Value,
            nameEn,
            nameJa,
            regionId,
            regionEn,
            regionJa,
            locationEn,
            locationJa,
            content,
            boss.SortOrder.Value);
    }

    private static string ReadIdentifier(string? value, int index, string propertyName)
    {
        string identifier = ReadText(value, index, propertyName);

        if (!IsStableIdentifier(identifier))
        {
            throw InvalidBoss(
                BossDataErrorCode.InvalidIdentifier,
                index,
                propertyName,
                "Identifier must contain lowercase ASCII letters or digits separated by '.', '_' or '-'.");
        }

        return identifier;
    }

    private static string ReadText(string? value, int index, string propertyName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw InvalidBoss(
                BossDataErrorCode.MissingRequiredField,
                index,
                propertyName,
                "A non-blank value is required.");
        }

        if (!string.Equals(value, value.Trim(), StringComparison.Ordinal))
        {
            throw InvalidBoss(
                BossDataErrorCode.MissingRequiredField,
                index,
                propertyName,
                "Leading or trailing whitespace is not allowed.");
        }

        return value;
    }

    private static bool IsStableIdentifier(string value)
    {
        bool previousWasSeparator = true;

        foreach (char character in value)
        {
            bool isLetterOrDigit = character is >= 'a' and <= 'z' or >= '0' and <= '9';

            if (isLetterOrDigit)
            {
                previousWasSeparator = false;
                continue;
            }

            bool isSeparator = character is '.' or '_' or '-';

            if (!isSeparator || previousWasSeparator)
            {
                return false;
            }

            previousWasSeparator = true;
        }

        return !previousWasSeparator;
    }

    private static BossDocumentDto Deserialize(ReadOnlySpan<byte> json)
    {
        try
        {
            return JsonSerializer.Deserialize<BossDocumentDto>(json, SerializerOptions) ??
                throw new BossDataException(
                    BossDataErrorCode.InvalidJson,
                    "The boss definition document root is null.");
        }
        catch (JsonException exception)
        {
            throw new BossDataException(
                BossDataErrorCode.InvalidJson,
                "The boss definition document is not valid JSON or contains unknown properties.",
                innerException: exception);
        }
    }

    private static void ValidateUtf8(ReadOnlySpan<byte> json)
    {
        try
        {
            _ = StrictUtf8.GetCharCount(json);
        }
        catch (DecoderFallbackException exception)
        {
            throw new BossDataException(
                BossDataErrorCode.InvalidEncoding,
                "The boss definition document is not valid UTF-8.",
                innerException: exception);
        }
    }

    private static ReadOnlySpan<byte> RemoveUtf8ByteOrderMark(ReadOnlySpan<byte> data) =>
        data.StartsWith(Encoding.UTF8.Preamble) ? data[Encoding.UTF8.Preamble.Length..] : data;

    private static BossDataException MissingRootProperty(string propertyName) =>
        new(
            BossDataErrorCode.MissingRequiredField,
            $"The root property '{propertyName}' is required.",
            propertyName: propertyName);

    private static BossDataException InvalidBoss(
        BossDataErrorCode errorCode,
        int bossIndex,
        string propertyName,
        string message) =>
        new(
            errorCode,
            $"Invalid boss entry {bossIndex}, property '{propertyName}': {message}",
            bossIndex,
            propertyName);

    private sealed class BossDocumentDto
    {
        [JsonPropertyName("schemaVersion")]
        public int? SchemaVersion { get; init; }

        [JsonPropertyName("bosses")]
        public BossDto?[]? Bosses { get; init; }
    }

    private sealed class BossDto
    {
        [JsonPropertyName("id")]
        public string? Id { get; init; }

        [JsonPropertyName("flagId")]
        public uint? FlagId { get; init; }

        [JsonPropertyName("nameEn")]
        public string? NameEn { get; init; }

        [JsonPropertyName("nameJa")]
        public string? NameJa { get; init; }

        [JsonPropertyName("regionId")]
        public string? RegionId { get; init; }

        [JsonPropertyName("regionEn")]
        public string? RegionEn { get; init; }

        [JsonPropertyName("regionJa")]
        public string? RegionJa { get; init; }

        [JsonPropertyName("locationEn")]
        public string? LocationEn { get; init; }

        [JsonPropertyName("locationJa")]
        public string? LocationJa { get; init; }

        [JsonPropertyName("content")]
        public string? Content { get; init; }

        [JsonPropertyName("sortOrder")]
        public int? SortOrder { get; init; }
    }

    private readonly record struct RegionDescriptor(
        string NameEn,
        string NameJa,
        GameContent Content);
}
