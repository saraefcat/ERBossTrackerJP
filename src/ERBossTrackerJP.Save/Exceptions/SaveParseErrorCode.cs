namespace ERBossTrackerJP.Save.Exceptions;

public enum SaveParseErrorCode
{
    InvalidBnd4Magic,
    Bnd4HeaderOutOfBounds,
    InvalidBnd4Header,
    Bnd4EntryOutOfBounds,
    CharacterProfileOutOfBounds,
    InvalidCharacterName,
    UnsupportedSaveVersion,
    EmptyCharacterSlot,
    EventFlagSectionNotFound,
    MissingEventFlagBlock,
    EventFlagOutOfBounds,
}
