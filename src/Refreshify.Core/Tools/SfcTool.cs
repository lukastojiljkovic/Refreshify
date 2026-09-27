using System.ServiceProcess;
using System.Text;
using Refreshify.Core.Platform;

namespace Refreshify.Core.Tools;

public sealed class SfcTool(ToolInfo info) : Tool(info)
{
    private const string Status = "Checking system files";

    public override async Task<ToolResult> RunAsync(ToolContext context, CancellationToken cancellationToken)
    {
        // SFC can't repair in either state and would only report a generic failure after a long scan.
        if (SystemState.IsComponentRestartPending)
            return SfcAnalyzer.ToResult(SfcVerdict.RestartPending);
        if (Services.Query("TrustedInstaller") is { StartType: ServiceStartMode.Disabled })
            return SfcAnalyzer.ToResult(SfcVerdict.ServiceUnavailable);

        context.Status(Status);
        var logOffset = SfcAnalyzer.LogLength(SfcAnalyzer.LogPath);
        var spec = ProcessSpec.System32("sfc.exe", "/scannow") with { Encoding = Encoding.Unicode };
        var result = await context.RunAsync(spec, line =>
        {
            if (SfcAnalyzer.ParseProgress(line) is { } percent)
                context.Status(Status, percent);
        }, cancellationToken);

        var srLines = SfcAnalyzer.ReadSrLines(SfcAnalyzer.LogPath, logOffset);
        context.LogExcerpt = SfcAnalyzer.Excerpt(srLines);
        return SfcAnalyzer.ToResult(SfcAnalyzer.Analyze(result.Output, srLines));
    }
}
