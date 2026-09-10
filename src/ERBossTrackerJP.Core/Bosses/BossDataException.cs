namespace ERBossTrackerJP.Core.Bosses;

public sealed class BossDataException : Exception
{
    public BossDataException(
        BossDataErrorCode errorCode,
        string message,
        int? bossIndex = null,
        string? propertyName = null,
        Exception? innerException = null)
        : base(message, innerException)
    {
        ErrorCode = errorCode;
        BossIndex = bossIndex;
        PropertyName = propertyName;
    }

    public BossDataErrorCode ErrorCode { get; }

    public int? BossIndex { get; }

    public string? PropertyName { get; }
}
