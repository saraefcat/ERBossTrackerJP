namespace ERBossTrackerJP.Save.Reading;

public readonly record struct EventFlagSection(ReadOnlyMemory<byte> Bytes, int Offset);
