namespace ERBossTrackerJP.DataGenerator.Generation;

public enum LocalizationGenerationErrorCode
{
    InvalidArgument,
    FileTooLarge,
    InvalidEncoding,
    InvalidJson,
    UnsupportedSchemaVersion,
    InvalidManifest,
    InvalidOfficialText,
    InvalidBossCount,
    InvalidMappingCount,
    DuplicateFlagId,
    MissingMapping,
    ExtraMapping,
    InvalidResolution,
    InvalidTextIdCount,
    MissingTextId,
    EmptyOfficialText,
    AmbiguousDisplayMismatch,
    InvalidComposite,
    InvalidGeneratedOutput,
    UnexpectedDifference,
}

public sealed class LocalizationGenerationException : Exception
{
    public LocalizationGenerationException(
        LocalizationGenerationErrorCode errorCode,
        string message,
        uint? flagId = null,
        Exception? innerException = null)
        : base(message, innerException)
    {
        ErrorCode = errorCode;
        FlagId = flagId;
    }

    public LocalizationGenerationErrorCode ErrorCode { get; }

    public uint? FlagId { get; }
}
