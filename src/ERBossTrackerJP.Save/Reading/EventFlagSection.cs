namespace ERBossTrackerJP.Save.Reading;

/// <param name="Bytes">The event flag bytes for a character slot.</param>
/// <param name="Offset">
/// The event flag offset relative to the slot data after its 16-byte checksum.
/// </param>
public readonly record struct EventFlagSection(ReadOnlyMemory<byte> Bytes, int Offset);
