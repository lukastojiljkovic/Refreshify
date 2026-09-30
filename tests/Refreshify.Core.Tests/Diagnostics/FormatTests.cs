using Refreshify.Core.Diagnostics;
using Refreshify.Core.Tools;

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

    [Theory]
    [InlineData(0, "0:00")]
    [InlineData(42.9, "0:42")]
    [InlineData(370, "6:10")]
    [InlineData(3723, "1:02:03")]
    [InlineData(-5, "0:00")]
    public void Durations_read_like_a_clock_and_are_never_negative(double seconds, string expected) =>
        Assert.Equal(expected, Format.Duration(TimeSpan.FromSeconds(seconds)));

    [Fact]
    public void Outcomes_are_counted_in_a_fixed_order_and_absent_ones_are_left_out() =>
        Assert.Equal("2 succeeded · 1 warning · 1 failed · 3 stopped", Format.Outcomes(
        [
            ToolOutcome.Failed, ToolOutcome.Succeeded, ToolOutcome.Cancelled, ToolOutcome.Warning,
            ToolOutcome.Succeeded, ToolOutcome.Cancelled, ToolOutcome.Cancelled,
        ]));

    [Fact]
    public void Skipped_outcomes_are_counted_too() =>
        Assert.Equal("1 succeeded · 2 skipped", Format.Outcomes([ToolOutcome.Skipped, ToolOutcome.Succeeded, ToolOutcome.Skipped]));
}
