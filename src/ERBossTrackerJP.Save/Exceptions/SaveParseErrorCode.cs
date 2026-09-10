namespace ERBossTrackerJP.Save.Exceptions;

public enum SaveParseErrorCode
{
    InvalidBnd4Magic,
    Bnd4HeaderOutOfBounds,
    Bnd4EntryOutOfBounds,
    UnsupportedSaveVersion,
    EmptyCharacterSlot,
    EventFlagSectionNotFound,
    MissingEventFlagBlock,
    EventFlagOutOfBounds,
}
