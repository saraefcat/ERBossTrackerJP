namespace ERBossTrackerJP.Save.Bnd4;

public sealed class Bnd4Container
{
    private readonly ReadOnlyMemory<byte> _source;

    internal Bnd4Container(
        ReadOnlyMemory<byte> source,
        int dataStartOffset,
        Bnd4Entry[] entries)
    {
        _source = source;
        DataStartOffset = dataStartOffset;
        Entries = Array.AsReadOnly(entries);
    }

    public int DataStartOffset { get; }

    public IReadOnlyList<Bnd4Entry> Entries { get; }

    public ReadOnlyMemory<byte> GetEntryData(int entryIndex)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(entryIndex);

        if (entryIndex >= Entries.Count)
        {
            throw new ArgumentOutOfRangeException(
                nameof(entryIndex),
                entryIndex,
                $"Entry index must be less than {Entries.Count}.");
        }

        Bnd4Entry entry = Entries[entryIndex];
        return _source.Slice(entry.DataOffset, entry.DataSize);
    }
}
