using ERBossTrackerJP.Core.Models;

namespace ERBossTrackerJP.Services.SaveFiles;

public sealed class LoadedSaveFile
{
    private readonly ReadOnlyMemory<byte> _bytes;

    internal LoadedSaveFile(
        SaveFileSnapshot snapshot,
        IEnumerable<CharacterSlot> characterSlots)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(characterSlots);

        CharacterSlot[] copiedSlots = characterSlots.ToArray();

        SourcePath = snapshot.SourcePath;
        LastWriteTimeUtc = snapshot.LastWriteTimeUtc;
        FileSize = snapshot.Bytes.Length;
        CharacterSlots = Array.AsReadOnly(copiedSlots);
        _bytes = snapshot.Bytes;
    }

    public string SourcePath { get; }

    public DateTimeOffset LastWriteTimeUtc { get; }

    public int FileSize { get; }

    public IReadOnlyList<CharacterSlot> CharacterSlots { get; }

    internal ReadOnlyMemory<byte> Bytes => _bytes;
}
