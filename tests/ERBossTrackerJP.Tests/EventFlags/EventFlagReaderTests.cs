using ERBossTrackerJP.Save.EventFlags;
using ERBossTrackerJP.Save.Exceptions;

namespace ERBossTrackerJP.Tests.EventFlags;

public sealed class EventFlagReaderTests
{
    private readonly EventFlagReader _reader = new();

    [Theory]
    [InlineData(1000u, 0b1000_0000)]
    [InlineData(1007u, 0b0000_0001)]
    public void IsSet_ReadsBitsInMsbFirstOrder(uint eventId, byte value)
    {
        byte[] eventFlags = [value];
        var blockOffsets = new Dictionary<uint, uint> { [1] = 0 };

        bool result = _reader.IsSet(eventFlags, eventId, blockOffsets);

        Assert.True(result);
    }

    [Fact]
    public void IsSet_ThrowsWhenBlockIsMissing()
    {
        var exception = Assert.Throws<SaveParseException>(() =>
            _reader.IsSet([0], 1000, new Dictionary<uint, uint>()));

        Assert.Equal(SaveParseErrorCode.MissingEventFlagBlock, exception.ErrorCode);
    }

    [Fact]
    public void IsSet_ThrowsWhenCalculatedOffsetIsOutsideSection()
    {
        var blockOffsets = new Dictionary<uint, uint> { [1] = 1 };

        var exception = Assert.Throws<SaveParseException>(() =>
            _reader.IsSet(new byte[125], 1000, blockOffsets));

        Assert.Equal(SaveParseErrorCode.EventFlagOutOfBounds, exception.ErrorCode);
        Assert.Equal(125, exception.Offset);
    }
}
