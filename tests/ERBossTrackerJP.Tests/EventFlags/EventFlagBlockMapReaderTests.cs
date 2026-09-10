using System.Text;
using ERBossTrackerJP.Save.EventFlags;
using ERBossTrackerJP.Save.Exceptions;

namespace ERBossTrackerJP.Tests.EventFlags;

public sealed class EventFlagBlockMapReaderTests
{
    private readonly EventFlagBlockMapReader _reader = new();

    [Fact]
    public void Read_ParsesUnsignedMappingsAndIgnoresBlankLines()
    {
        using var text = new StringReader("  0, 0  \r\n\r\n1045540,6223\n");

        IReadOnlyDictionary<uint, uint> mappings = _reader.Read(text);

        Assert.Equal(2, mappings.Count);
        Assert.Equal(0u, mappings[0]);
        Assert.Equal(6223u, mappings[1_045_540]);
    }

    [Theory]
    [InlineData("1")]
    [InlineData("1,2,3")]
    [InlineData("-1,2")]
    [InlineData("1,-2")]
    [InlineData("4294967296,1")]
    [InlineData("1,4294967296")]
    [InlineData("block,1")]
    [InlineData("1,")]
    public void Read_RejectsMalformedLine(string line)
    {
        using var text = new StringReader(line);

        var exception = Assert.Throws<SaveParseException>(() => _reader.Read(text));

        Assert.Equal(SaveParseErrorCode.InvalidEventFlagBlockMap, exception.ErrorCode);
        Assert.Equal(1, exception.LineNumber);
    }

    [Fact]
    public void Read_RejectsDuplicateBlock()
    {
        using var text = new StringReader("1,2\n1,3");

        var exception = Assert.Throws<SaveParseException>(() => _reader.Read(text));

        Assert.Equal(SaveParseErrorCode.InvalidEventFlagBlockMap, exception.ErrorCode);
        Assert.Equal(2, exception.LineNumber);
    }

    [Fact]
    public void Read_RejectsDuplicateOffset()
    {
        using var text = new StringReader("1,2\n3,2");

        var exception = Assert.Throws<SaveParseException>(() => _reader.Read(text));

        Assert.Equal(SaveParseErrorCode.InvalidEventFlagBlockMap, exception.ErrorCode);
        Assert.Equal(2, exception.LineNumber);
    }

    [Fact]
    public void Read_RejectsEmptyMap()
    {
        using var text = new StringReader(" \r\n\t\n");

        var exception = Assert.Throws<SaveParseException>(() => _reader.Read(text));

        Assert.Equal(SaveParseErrorCode.InvalidEventFlagBlockMap, exception.ErrorCode);
        Assert.Null(exception.LineNumber);
    }

    [Fact]
    public void Read_RejectsInvalidUtf8()
    {
        using var stream = new MemoryStream([0xFF]);

        var exception = Assert.Throws<SaveParseException>(() => _reader.Read(stream));

        Assert.Equal(SaveParseErrorCode.InvalidEventFlagBlockMap, exception.ErrorCode);
        Assert.IsType<DecoderFallbackException>(exception.InnerException);
    }

    [Fact]
    public void Read_AcceptsUtf8ByteOrderMark()
    {
        byte[] bytes = [.. Encoding.UTF8.GetPreamble(), .. "1,2\n"u8];
        using var stream = new MemoryStream(bytes);

        IReadOnlyDictionary<uint, uint> mappings = _reader.Read(stream);

        Assert.Equal(2u, mappings[1]);
    }

    [Fact]
    public void Read_RejectsUtf16ByteOrderMark()
    {
        byte[] bytes = [.. Encoding.Unicode.GetPreamble(), .. Encoding.Unicode.GetBytes("1,2\n")];
        using var stream = new MemoryStream(bytes);

        var exception = Assert.Throws<SaveParseException>(() => _reader.Read(stream));

        Assert.Equal(SaveParseErrorCode.InvalidEventFlagBlockMap, exception.ErrorCode);
    }

    [Fact]
    public void EmbeddedMap_MatchesPinnedSourceData()
    {
        IReadOnlyDictionary<uint, uint> mappings = EmbeddedEventFlagBlockMap.Load();

        Assert.Equal(EmbeddedEventFlagBlockMap.ExpectedMappingCount, mappings.Count);
        Assert.Equal(0u, mappings[0]);
        Assert.Equal(6223u, mappings[1_045_540]);
        Assert.Equal(14_225u, mappings[2_253_469]);
    }
}
