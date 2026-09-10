namespace ERBossTrackerJP.Core.Bosses;

public enum BossDataErrorCode
{
    EmptyDocument,
    DocumentTooLarge,
    InvalidEncoding,
    InvalidJson,
    UnsupportedSchemaVersion,
    InvalidBossCount,
    MissingRequiredField,
    InvalidIdentifier,
    InvalidFlagId,
    InvalidContent,
    InvalidSortOrder,
    DuplicateId,
    DuplicateFlagId,
    DuplicateSortOrder,
    InconsistentRegion,
}
