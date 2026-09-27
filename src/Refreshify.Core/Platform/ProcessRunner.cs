using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace Refreshify.Core.Platform;

/// <param name="FileName">Full path of the executable. The elevated worker never relies on a <c>PATH</c> lookup.</param>
public sealed record ProcessSpec(string FileName, string Arguments)
{
    /// <summary>The output encoding. Console tools default to the OEM code page.</summary>
    public Encoding? Encoding { get; init; }

    /// <summary>How the command appears in traces and reports, when the real arguments are unreadable (encoded scripts).</summary>
    public string? DisplayName { get; init; }

    public string CommandLine => DisplayName ?? $"{Path.GetFileName(FileName)} {Arguments}".Trim();

    public static ProcessSpec System32(string executable, string arguments) =>
        new(Path.Combine(Environment.SystemDirectory, executable), arguments);
}

public sealed record ProcessResult(int ExitCode, IReadOnlyList<string> Output);

public interface IProcessRunner
{
    Task<ProcessResult> RunAsync(ProcessSpec spec, Action<string>? onLine, CancellationToken cancellationToken);
}

/// <summary>Runs a hidden process and streams its output line by line.</summary>
public sealed class ProcessRunner : IProcessRunner
{
    public static readonly ProcessRunner Default = new();

    private static readonly Lazy<Encoding> Oem = new(() =>
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return Encoding.GetEncoding(CultureInfo.CurrentCulture.TextInfo.OEMCodePage);
    });

    public static Encoding OemEncoding => Oem.Value;

    public async Task<ProcessResult> RunAsync(ProcessSpec spec, Action<string>? onLine, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var startInfo = new ProcessStartInfo(spec.FileName, spec.Arguments)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException($"{spec.FileName} didn't start.");
        process.StandardInput.Close();

        var lines = new List<string>();
        void Emit(string line)
        {
            lock (lines)
                lines.Add(line);
            onLine?.Invoke(line);
        }

        var encoding = spec.Encoding ?? OemEncoding;
        await using (cancellationToken.Register(() => Kill(process)))
        {
            await Task.WhenAll(
                ReadLinesAsync(process.StandardOutput.BaseStream, encoding, Emit),
                ReadLinesAsync(process.StandardError.BaseStream, encoding, Emit));
            await process.WaitForExitAsync(CancellationToken.None);
        }

        cancellationToken.ThrowIfCancellationRequested();
        return new ProcessResult(process.ExitCode, lines);
    }

    private static void Kill(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
            // Already exited.
        }
    }

    /// <summary>Splits on both <c>\r</c> and <c>\n</c>: DISM, SFC and chkdsk redraw their progress with carriage returns.</summary>
    private static async Task ReadLinesAsync(Stream stream, Encoding encoding, Action<string> emit)
    {
        var decoder = encoding.GetDecoder();
        var bytes = new byte[4096];
        var chars = new char[encoding.GetMaxCharCount(bytes.Length)];
        var line = new StringBuilder();

        int read;
        while ((read = await stream.ReadAsync(bytes)) > 0)
        {
            var count = decoder.GetChars(bytes, 0, read, chars, 0);
            for (var i = 0; i < count; i++)
            {
                if (chars[i] is '\r' or '\n')
                    Flush();
                else if (chars[i] != '\0')
                    line.Append(chars[i]);
            }
        }

        Flush();

        void Flush()
        {
            var text = line.ToString().TrimEnd();
            line.Clear();
            if (text.Length > 0)
                emit(text);
        }
    }
}
