using Refreshify.Core.Platform;

namespace Refreshify.Core.Tests.Platform;

public class PowerShellTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Emitted_messages_are_parsed_as_json()
    {
        var result = await ProcessRunner.Default.RunAsync(PowerShell.Inline("Emit @{ state = 'ok'; count = 3 }", "test"), null, Ct);

        var message = Assert.Single(PowerShell.Messages(result.Output));
        Assert.Equal("ok", message.GetProperty("state").GetString());
        Assert.Equal(3, message.GetProperty("count").GetInt32());
        Assert.Equal(0, result.ExitCode);
    }

    [Fact]
    public async Task Unhandled_errors_are_reported_with_their_hresult()
    {
        var spec = PowerShell.Inline("throw [System.IO.FileNotFoundException]::new('missing thing')", "test");

        var result = await ProcessRunner.Default.RunAsync(spec, null, Ct);

        var error = PowerShell.Error(PowerShell.Messages(result.Output));
        Assert.NotNull(error);
        Assert.Equal("missing thing", error.Message);
        Assert.Equal(unchecked((int)0x80070002), error.HResult);
        Assert.Equal(1, result.ExitCode);
    }

    [Fact]
    public async Task Parameters_are_passed_as_literal_strings()
    {
        var parameters = new Dictionary<string, string> { ["Value"] = "it's $(not) `expanded`" };

        var result = await ProcessRunner.Default.RunAsync(PowerShell.Inline("Emit @{ value = $Value }", "test", parameters), null, Ct);

        Assert.Equal("it's $(not) `expanded`", Assert.Single(PowerShell.Messages(result.Output)).GetProperty("value").GetString());
    }

    [Fact]
    public void Embedded_scripts_are_loaded_by_name()
    {
        var spec = PowerShell.Script("RestorePoint");

        Assert.Contains("-EncodedCommand", spec.Arguments);
        Assert.Equal("powershell.exe RestorePoint.ps1", spec.CommandLine);
    }
}
