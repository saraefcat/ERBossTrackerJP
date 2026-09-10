namespace ERBossTrackerJP.Services.SaveFiles;

public enum SaveFileReadErrorCode
{
    InvalidPath,
    FileNotFound,
    AccessDenied,
    FileTooLarge,
    TemporarilyUnavailable,
    ChangedDuringRead,
}
