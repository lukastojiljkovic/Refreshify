using System.Reflection;
using System.Text;
using System.Text.Json;

namespace Refreshify.Core.Platform;

public sealed record ScriptError(string Message, int HResult);

/// <summary>
/// Builds hidden Windows PowerShell invocations. Scripts report results as JSON lines prefixed with <c>@@</c>, so nothing
/// depends on localized cmdlet output; unhandled errors are reported with their HRESULT.
/// </summary>
public static class PowerShell
{
    private const string MessagePrefix = "@@";

    private const string Prelude = """
        $ErrorActionPreference = 'Stop'
        $ProgressPreference = 'SilentlyContinue'
        [Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)
        function Emit([hashtable] $Message) { [Console]::Out.WriteLine('@@' + (ConvertTo-Json $Message -Compress -Depth 4)) }
        trap { Emit @{ error = $_.Exception.Message; hresult = $_.Exception.HResult }; exit 1 }

        """;

    private static readonly string Executable = Path.Combine(Environment.SystemDirectory, @"WindowsPowerShell\v1.0\powershell.exe");

    /// <summary>Runs one of the scripts embedded from <c>Scripts\*.ps1</c>.</summary>
    public static ProcessSpec Script(string name, IReadOnlyDictionary<string, string>? parameters = null)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream($"Refreshify.Scripts.{name}.ps1")
            ?? throw new ArgumentException($"There's no embedded script named {name}.", nameof(name));
        using var reader = new StreamReader(stream);
        return Inline(reader.ReadToEnd(), $"powershell.exe {name}.ps1", parameters);
    }

    /// <param name="parameters">Assigned as single-quoted string variables, so their values are never evaluated.</param>
    public static ProcessSpec Inline(string script, string displayName, IReadOnlyDictionary<string, string>? parameters = null)
    {
        var text = new StringBuilder(Prelude);
        foreach (var (name, value) in parameters ?? new Dictionary<string, string>())
            text.Append('$').Append(name).Append(" = '").Append(value.Replace("'", "''")).AppendLine("'");
        text.Append(script);

        var encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(text.ToString()));
        return new ProcessSpec(Executable, $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand {encoded}")
        {
            Encoding = new UTF8Encoding(false),
            DisplayName = displayName,
        };
    }

    public static bool IsMessage(string line) => line.StartsWith(MessagePrefix, StringComparison.Ordinal);

    public static IReadOnlyList<JsonElement> Messages(IEnumerable<string> output) =>
        [.. output
            .Where(IsMessage)
            .Select(line => JsonDocument.Parse(line[MessagePrefix.Length..]).RootElement.Clone())];

    public static ScriptError? Error(IEnumerable<JsonElement> messages) =>
        messages
            .Where(message => message.TryGetProperty("error", out _))
            .Select(message => new ScriptError(
                message.GetProperty("error").GetString() ?? string.Empty,
                message.TryGetProperty("hresult", out var hresult) ? hresult.GetInt32() : 0))
            .FirstOrDefault();

    /// <summary>The first message that has <paramref name="property"/>, such as a script's final state.</summary>
    public static JsonElement? Find(IEnumerable<JsonElement> messages, string property) =>
        messages.Where(message => message.TryGetProperty(property, out _)).Cast<JsonElement?>().FirstOrDefault();
}
