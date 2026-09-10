namespace ERBossTrackerJP.Save.Exceptions;

public sealed class SaveParseException : Exception
{
    public SaveParseException(
        SaveParseErrorCode errorCode,
        string message,
        long? offset = null,
        Exception? innerException = null,
        int? lineNumber = null)
        : base(message, innerException)
    {
        ErrorCode = errorCode;
        Offset = offset;
        LineNumber = lineNumber;
    }

    public SaveParseErrorCode ErrorCode { get; }

    public long? Offset { get; }

    public int? LineNumber { get; }
}
