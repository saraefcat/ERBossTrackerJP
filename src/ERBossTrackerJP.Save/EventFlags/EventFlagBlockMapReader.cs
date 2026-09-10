using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using ERBossTrackerJP.Save.Exceptions;

namespace ERBossTrackerJP.Save.EventFlags;

public sealed class EventFlagBlockMapReader
{
    public const int MaximumMappingCount = 65_536;
    public const int MaximumLineLength = 64;

    private static readonly Encoding StrictUtf8 =
        new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    public IReadOnlyDictionary<uint, uint> Read(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        try
        {
            using var reader = new StreamReader(
                stream,
                StrictUtf8,
                detectEncodingFromByteOrderMarks: false,
                bufferSize: 1024,
                leaveOpen: true);
            return Read(reader);
        }
        catch (DecoderFallbackException exception)
        {
            throw new SaveParseException(
                SaveParseErrorCode.InvalidEventFlagBlockMap,
                "The event flag block map is not valid UTF-8.",
                innerException: exception);
        }
    }

    public IReadOnlyDictionary<uint, uint> Read(TextReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);

        var mappings = new Dictionary<uint, uint>();
        var usedOffsets = new HashSet<uint>();
        int lineNumber = 0;
        string? line;

        while ((line = reader.ReadLine()) is not null)
        {
            lineNumber++;
            ReadOnlySpan<char> trimmed = line.AsSpan().Trim();

            if (lineNumber == 1 && !trimmed.IsEmpty && trimmed[0] == '\uFEFF')
            {
                trimmed = trimmed[1..].TrimStart();
            }

            if (trimmed.IsEmpty)
            {
                continue;
            }

            if (trimmed.Length > MaximumLineLength)
            {
                throw InvalidLine(lineNumber, "The line is longer than the supported limit.");
            }

            int commaIndex = trimmed.IndexOf(',');

            if (commaIndex <= 0 ||
                commaIndex != trimmed.LastIndexOf(',') ||
                commaIndex == trimmed.Length - 1)
            {
                throw InvalidLine(lineNumber, "Expected exactly two comma-separated unsigned integers.");
            }

            ReadOnlySpan<char> blockText = trimmed[..commaIndex].Trim();
            ReadOnlySpan<char> offsetText = trimmed[(commaIndex + 1)..].Trim();

            if (!uint.TryParse(
                    blockText,
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out uint block))
            {
                throw InvalidLine(lineNumber, "The event flag block number is not a valid unsigned integer.");
            }

            if (!uint.TryParse(
                    offsetText,
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out uint offset))
            {
                throw InvalidLine(lineNumber, "The event flag block offset is not a valid unsigned integer.");
            }

            if (mappings.Count == MaximumMappingCount)
            {
                throw InvalidLine(lineNumber, $"The block map exceeds {MaximumMappingCount} entries.");
            }

            if (!mappings.TryAdd(block, offset))
            {
                throw InvalidLine(lineNumber, $"Event flag block {block} is duplicated.");
            }

            if (!usedOffsets.Add(offset))
            {
                throw InvalidLine(lineNumber, $"Event flag block offset {offset} is duplicated.");
            }
        }

        if (mappings.Count == 0)
        {
            throw new SaveParseException(
                SaveParseErrorCode.InvalidEventFlagBlockMap,
                "The event flag block map contains no mappings.");
        }

        return new ReadOnlyDictionary<uint, uint>(mappings);
    }

    private static SaveParseException InvalidLine(int lineNumber, string message) =>
        new(
            SaveParseErrorCode.InvalidEventFlagBlockMap,
            $"Invalid event flag block map at line {lineNumber}: {message}",
            lineNumber: lineNumber);
}
