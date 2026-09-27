using System.ServiceProcess;
using Refreshify.Core.Platform;

namespace Refreshify.Core.Tools;

/// <summary>Restarts services, listed in dependency order: a service before the services that depend on it.</summary>
public sealed class ServiceRestartTool(ToolInfo info, IReadOnlyList<string> services, string summary) : Tool(info)
{
    public override async Task<ToolResult> RunAsync(ToolContext context, CancellationToken cancellationToken)
    {
        foreach (var name in services)
        {
            if (Services.Query(name) is not { } state)
                return ToolResult.Skipped($"The {name} service isn't installed on this PC.");
            if (state.StartType == ServiceStartMode.Disabled)
                return ToolResult.Skipped($"The {state.DisplayName} service is turned off on this PC, so it was left alone.");
        }

        context.Status("Stopping services");
        var dependents = new List<string>();
        foreach (var name in services.Reverse())
            dependents.AddRange(await Services.StopAsync(name, cancellationToken));

        context.Status("Starting services again");
        foreach (var name in services.Concat(dependents).Distinct(StringComparer.OrdinalIgnoreCase))
            await Services.StartAsync(name, CancellationToken.None);

        return ToolResult.Succeeded(summary);
    }
}
