using Refreshify.Core.Diagnostics;

namespace Refreshify.Core.Tests.Diagnostics;

public class ErrorCodesTests
{
    [Theory]
    [InlineData(0, "0")]
    [InlineData(3010, "3010")]
    [InlineData(unchecked((int)0x800F081F), "0x800F081F")]
    [InlineData(-1, "0xFFFFFFFF")]
    public void Formats_exit_codes_in_decimal_and_hresults_in_hex(int code, string expected) =>
        Assert.Equal(expected, ErrorCodes.Format(code));

    [Fact]
    public void Finds_the_first_hresult_printed_by_a_tool() =>
        Assert.Equal(
            unchecked((int)0x800705B4),
            ErrorCodes.FindHResult(["Sending resync command to local computer", "The following error occurred: This operation returned because the timeout period expired. (0x800705B4)"]));

    [Fact]
    public void Ignores_hex_numbers_that_are_not_hresults() =>
        Assert.Null(ErrorCodes.FindHResult(["Address 0x00401000 loaded"]));
}
