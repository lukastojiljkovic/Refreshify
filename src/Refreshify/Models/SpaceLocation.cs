using Refreshify.Core.Diagnostics;

namespace Refreshify.Models;

/// <summary>A place the Disk space page can scan: the user's files, or one of the fixed drives.</summary>
/// <param name="Drive">The drive, when this is a whole drive rather than a folder.</param>
internal sealed record SpaceLocation(string Title, string Path, DriveInfo? Drive)
{
    /// <summary>What the location box offers: your files first, then every fixed drive.</summary>
    public static SpaceLocation[] All()
    {
        var user = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var locations = new List<SpaceLocation> { new($"Your files ({user})", user, null) };
        foreach (var drive in DriveInfo.GetDrives().Where(drive => drive.DriveType == DriveType.Fixed && drive.IsReady))
        {
            var label = drive.VolumeLabel is { Length: > 0 } name ? name : "Local Disk";
            locations.Add(new(
                $"{label} ({drive.Name.TrimEnd('\\')}) \u2014 {Format.Bytes(drive.AvailableFreeSpace)} free of {Format.Bytes(drive.TotalSize)}",
                drive.RootDirectory.FullName,
                drive));
        }

        return [.. locations];
    }
}
