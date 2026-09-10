namespace ERBossTrackerJP.Services.SaveFiles;

public sealed class SaveFileReadException : Exception
{
    public SaveFileReadException(
        SaveFileReadErrorCode errorCode,
        string filePath,
        string message,
        int attemptCount,
        Exception? innerException = null)
        : base(message, innerException)
    {
        ErrorCode = errorCode;
        FilePath = filePath;
        AttemptCount = attemptCount;
    }

    public SaveFileReadErrorCode ErrorCode { get; }

    public string FilePath { get; }

    public int AttemptCount { get; }
}
