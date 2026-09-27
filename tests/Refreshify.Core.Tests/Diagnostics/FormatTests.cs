using Refreshify.Core.Diagnostics;

namespace Refreshify.Core.Tests.Diagnostics;

public class FormatTests
{
    [Theory]
    [InlineData(0, "0 bytes")]
    [InlineData(1, "1 byte")]
    [InlineData(1023, "1,023 bytes")]
    [InlineData(1536, "1.5 KB")]
    [InlineData(5L * 1024 * 1024, "5.0 MB")]
    [InlineData(1288490189L, "1.2 GB")]
    public void Bytes_are_shown_in_the_largest_whole_unit(long bytes, string expected) =>
        Assert.Equal(expected, Format.Bytes(bytes));

    [Theory]
    [InlineData(1, "file", "1 file")]
    [InlineData(2, "file", "2 files")]
    [InlineData(1500, "update", "1,500 updates")]
    public void Counts_are_pluralized(int count, string noun, string expected) =>
        Assert.Equal(expected, Format.Count(count, noun));
}
