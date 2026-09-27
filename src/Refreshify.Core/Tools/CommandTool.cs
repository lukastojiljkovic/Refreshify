using Refreshify.Core.Platform;

namespace Refreshify.Core.Tools;

/// <param name="FileName">Full path. Environment variables such as <c>%SystemRoot%</c> here and in
/// <paramref name="Arguments"/> are expanded when it runs.</param>
/// <param name="Required">A failing required command stops the tool; a failing optional one only makes it a warning.</param>
public sealed record Command(string FileName, string Arguments, string Status, bool Required = true);

/// <summary>Runs a fixed sequence of commands that report success with exit code 0.</summary>
public sealed class CommandTool(ToolInfo info, IReadOnlyList<Command> commands, string summary) : Tool(info)
{
    public override async Task<ToolResult> RunAsync(ToolContext context, CancellationToken cancellationToken)
    {
        var warnings = 0;
        foreach (var command in commands)
        {
            context.Status(command.Status);
            var spec = new ProcessSpec(
                Environment.ExpandEnvironmentVariables(command.FileName), Environment.ExpandEnvironmentVariables(command.Arguments));
            var result = await context.RunAsync(spec, null, cancellationToken);
            if (result.ExitCode == 0)
                continue;

            if (command.Required)
                return ToolResult.Failed($"{command.Status} didn't work.", result.ExitCode);
            warnings++;
        }

        var restart = Info.Has(ToolTraits.RestartRequired);
        return warnings == 0
            ? ToolResult.Succeeded(summary) with { RestartRequired = restart }
            : ToolResult.Warning($"{summary} Some steps reported a problem; see the technical details.") with { RestartRequired = restart };
    }
}
