using System.Text.Json;
using System.Text.Json.Serialization;

namespace Refreshify.Core.Engine;

/// <summary>Past runs as one JSON file each, named by run id so the newest sort last.</summary>
public sealed class HistoryStore(string directory, int capacity = 50)
{
    public static string DefaultDirectory { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Refreshify", "History");

    public void Save(RunRecord run)
    {
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, $"{run.Id}.json"), JsonSerializer.Serialize(run, HistoryJson.Default.RunRecord));

        foreach (var old in Files().Skip(capacity))
            File.Delete(old);
    }

    /// <summary>Newest first. Files that can't be read are skipped.</summary>
    public IReadOnlyList<RunRecord> Load() => [.. Files().Select(Read).OfType<RunRecord>()];

    public void Clear()
    {
        foreach (var file in Files())
            File.Delete(file);
    }

    private IEnumerable<string> Files() =>
        Directory.Exists(directory) ? Directory.GetFiles(directory, "*.json").OrderDescending(StringComparer.Ordinal) : [];

    private static RunRecord? Read(string file)
    {
        try
        {
            return JsonSerializer.Deserialize(File.ReadAllText(file), HistoryJson.Default.RunRecord);
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}

[JsonSourceGenerationOptions(
    WriteIndented = true, UseStringEnumConverter = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(RunRecord))]
internal sealed partial class HistoryJson : JsonSerializerContext;
