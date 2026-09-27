using Refreshify.Core.Platform;
using Refreshify.Core.Tools;

namespace Refreshify.Core.Tests.Platform;

public class ScriptsTests
{
    private const string ResourcePrefix = "Refreshify.Scripts.";

    public static TheoryData<string> Scripts => [.. typeof(Tool).Assembly.GetManifestResourceNames()
        .Where(name => name.StartsWith(ResourcePrefix, StringComparison.Ordinal))
        .Select(name => name[ResourcePrefix.Length..^".ps1".Length])];

    [Theory]
    [MemberData(nameof(Scripts))]
    public async Task Embedded_scripts_parse_without_errors(string name)
    {
        using var stream = typeof(Tool).Assembly.GetManifestResourceStream($"{ResourcePrefix}{name}.ps1")!;
        var text = await new StreamReader(stream).ReadToEndAsync(TestContext.Current.CancellationToken);
        var spec = PowerShell.Inline("""
            $errors = $null
            [void][System.Management.Automation.Language.Parser]::ParseInput($Text, [ref]$null, [ref]$errors)
            Emit @{ errors = @($errors | ForEach-Object { "$($_.Extent.StartLineNumber): $($_.Message)" }) }
            """, "parse", new Dictionary<string, string> { ["Text"] = text });

        var result = await ProcessRunner.Default.RunAsync(spec, null, TestContext.Current.CancellationToken);

        var errors = PowerShell.Find(PowerShell.Messages(result.Output), "errors")!.Value.GetProperty("errors");
        Assert.Empty(errors.EnumerateArray().Select(error => error.GetString()));
    }
}
