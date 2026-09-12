namespace ERBossTrackerJP.Save.Reading;

/// <param name="Bytes">The event flag bytes for a character slot.</param>
/// <param name="Offset">
/// The event flag offset relative to the slot data after its 16-byte checksum.
/// </param>
/// <param name="TotalDeathCount">
/// The character's total death count stored immediately before the event flags.
/// </param>
public readonly record struct EventFlagSection(
    ReadOnlyMemory<byte> Bytes,
    int Offset,
    uint TotalDeathCount = 0);
