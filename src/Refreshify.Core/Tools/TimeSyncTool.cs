using System.ServiceProcess;
using Refreshify.Core.Diagnostics;
using Refreshify.Core.Platform;

namespace Refreshify.Core.Tools;

public sealed class TimeSyncTool(ToolInfo info) : Tool(info)
{
    private const string Service = "W32Time";

    public override async Task<ToolResult> RunAsync(ToolContext context, CancellationToken cancellationToken)
    {
        if (Services.Query(Service) is not { } time || time.StartType == ServiceStartMode.Disabled)
            return ToolResult.Skipped("The Windows Time service is turned off on this PC, so the clock wasn't synced.");

        var started = time.Status != ServiceControllerStatus.Running;
        if (started)
        {
            context.Status("Starting the Windows Time service");
            await Services.StartAsync(Service, cancellationToken);
        }

        context.Status("Syncing the clock");
        var result = await context.RunAsync(ProcessSpec.System32("w32tm.exe", "/resync"), null, cancellationToken);

        // A service that has just started may not have reached the time server yet.
        if (result.ExitCode != 0 && started)
        {
            await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
            result = await context.RunAsync(ProcessSpec.System32("w32tm.exe", "/resync"), null, cancellationToken);
        }

        if (result.ExitCode == 0)
            return ToolResult.Succeeded("Your clock is in sync with the time server.");

        var code = ErrorCodes.FindHResult(result.Output) ?? result.ExitCode;
        var issue = code == ErrorCodes.Timeout ? KnownIssues.NoConnection : KnownIssues.FromHResult(code);
        return ToolResult.Failed($"It stopped with error {ErrorCodes.Format(code)}.", code, issue);
    }
}
