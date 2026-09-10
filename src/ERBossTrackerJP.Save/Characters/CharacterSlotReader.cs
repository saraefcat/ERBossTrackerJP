using System.Buffers.Binary;
using System.Text;
using ERBossTrackerJP.Core.Models;
using ERBossTrackerJP.Save.Bnd4;
using ERBossTrackerJP.Save.Exceptions;

namespace ERBossTrackerJP.Save.Characters;

public sealed class CharacterSlotReader
{
    public const int CharacterSlotCount = 10;
    public const int ProfileSummaryEntryIndex = 10;
    public const int ProfileSummaryOffset = 0x1964;
    public const int ProfileEntrySize = 0x24C;
    public const int CharacterNameByteLength = 32;
    public const int LevelOffset = 0x22;

    private const int ActiveProfileByteCount = CharacterSlotCount;
    private const int RequiredProfileSummaryLength =
        ProfileSummaryOffset + ActiveProfileByteCount + CharacterSlotCount * ProfileEntrySize;

    private static readonly Encoding StrictUtf16LittleEndian =
        new UnicodeEncoding(bigEndian: false, byteOrderMark: false, throwOnInvalidBytes: true);

    public IReadOnlyList<CharacterSlot> Read(Bnd4Container container)
    {
        ArgumentNullException.ThrowIfNull(container);

        if (container.Entries.Count <= ProfileSummaryEntryIndex)
        {
            throw new SaveParseException(
                SaveParseErrorCode.CharacterProfileOutOfBounds,
                $"The BND4 container has {container.Entries.Count} entries; entry {ProfileSummaryEntryIndex} is required.");
        }

        Bnd4Entry profileSummaryEntry = container.Entries[ProfileSummaryEntryIndex];
        ReadOnlySpan<byte> data = container.GetEntryData(ProfileSummaryEntryIndex).Span;

        if (data.Length < RequiredProfileSummaryLength)
        {
            throw new SaveParseException(
                SaveParseErrorCode.CharacterProfileOutOfBounds,
                $"USER_DATA_010 contains {data.Length} bytes; ProfileSummary requires at least {RequiredProfileSummaryLength} bytes.",
                profileSummaryEntry.DataOffset + ProfileSummaryOffset);
        }

        var slots = new List<CharacterSlot>(CharacterSlotCount);

        for (int slotIndex = 0; slotIndex < CharacterSlotCount; slotIndex++)
        {
            if (data[ProfileSummaryOffset + slotIndex] == 0)
            {
                continue;
            }

            int profileOffset =
                ProfileSummaryOffset + ActiveProfileByteCount + slotIndex * ProfileEntrySize;
            string name = ReadCharacterName(
                data.Slice(profileOffset, CharacterNameByteLength),
                profileSummaryEntry.DataOffset + profileOffset);
            uint level = BinaryPrimitives.ReadUInt32LittleEndian(
                data.Slice(profileOffset + LevelOffset, sizeof(uint)));

            slots.Add(new CharacterSlot(slotIndex, name, level));
        }

        return slots.AsReadOnly();
    }

    private static string ReadCharacterName(ReadOnlySpan<byte> bytes, int absoluteOffset)
    {
        int byteLength = 0;

        while (byteLength < bytes.Length &&
               BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(byteLength, sizeof(ushort))) != 0)
        {
            byteLength += sizeof(ushort);
        }

        if (byteLength == 0)
        {
            throw new SaveParseException(
                SaveParseErrorCode.InvalidCharacterName,
                "An active character profile has an empty name.",
                absoluteOffset);
        }

        try
        {
            string name = StrictUtf16LittleEndian.GetString(bytes[..byteLength]);

            if (string.IsNullOrWhiteSpace(name))
            {
                throw new SaveParseException(
                    SaveParseErrorCode.InvalidCharacterName,
                    "An active character profile has a blank name.",
                    absoluteOffset);
            }

            return name;
        }
        catch (DecoderFallbackException exception)
        {
            throw new SaveParseException(
                SaveParseErrorCode.InvalidCharacterName,
                "A character profile name is not valid UTF-16LE.",
                absoluteOffset,
                exception);
        }
    }
}
