using Microsoft.UI.Xaml;
using Microsoft.Win32;
using Refreshify.Core.Tools;
using Refreshify.Core.Updates;

namespace Refreshify.Services;

/// <summary>
/// User preferences under HKCU\Software\Refreshify, where the reminder and the uninstaller find them too. The
/// <c>Tools</c> subkey holds each tool's <i>Run all</i> choice.
/// </summary>
internal static class AppSettings
{
    private const string KeyPath = @"Software\Refreshify";
    private const string ToolsKeyPath = KeyPath + @"\Tools";

    /// <summary>The reminder intervals Settings offers, in months. Zero turns the reminder off.</summary>
    public static readonly int[] ReminderIntervals = [0, 1, 2, 3, 6];

    /// <summary>Deletes every preference, for the uninstaller.</summary>
    public static void Clear() => Registry.CurrentUser.DeleteSubKeyTree(KeyPath, throwOnMissingSubKey: false);

    public static ElementTheme Theme
    {
        get => (ElementTheme)Read(nameof(Theme), (int)ElementTheme.Default) is var theme && Enum.IsDefined(theme) ? theme : ElementTheme.Default;
        set => Write(nameof(Theme), (int)value);
    }

    public static bool CreateRestorePoint
    {
        get => Read(nameof(CreateRestorePoint), 1) != 0;
        set => Write(nameof(CreateRestorePoint), value ? 1 : 0);
    }

    public static bool FixAutomatically
    {
        get => Read(nameof(FixAutomatically), 1) != 0;
        set => Write(nameof(FixAutomatically), value ? 1 : 0);
    }

    public static int TempFileAgeHours
    {
        get => Math.Clamp(Read(nameof(TempFileAgeHours), 24), 0, ToolOptions.MaxTempFileAgeHours);
        set => Write(nameof(TempFileAgeHours), Math.Clamp(value, 0, ToolOptions.MaxTempFileAgeHours));
    }

    public static bool ShowTechnicalDetails
    {
        get => Read(nameof(ShowTechnicalDetails), 0) != 0;
        set => Write(nameof(ShowTechnicalDetails), value ? 1 : 0);
    }

    public static bool HidePersonalDetails
    {
        get => Read(nameof(HidePersonalDetails), 1) != 0;
        set => Write(nameof(HidePersonalDetails), value ? 1 : 0);
    }

    public static int ReminderMonths
    {
        get => Read(nameof(ReminderMonths), 0) is var months && ReminderIntervals.Contains(months) ? months : 0;
        set => Write(nameof(ReminderMonths), ReminderIntervals.Contains(value) ? value : 0);
    }

    public static bool ShowWelcome
    {
        get => Read(nameof(ShowWelcome), 1) != 0;
        set => Write(nameof(ShowWelcome), value ? 1 : 0);
    }

    /// <summary>When <i>Run all</i> last finished without being cancelled.</summary>
    public static DateTimeOffset? LastFullRefresh
    {
        get => ReadTime(nameof(LastFullRefresh));
        set => WriteTime(nameof(LastFullRefresh), value);
    }

    public static DateTimeOffset? LastReminder
    {
        get => ReadTime(nameof(LastReminder));
        set => WriteTime(nameof(LastReminder), value);
    }

    /// <summary>Whether the background update check runs at startup. On by default.</summary>
    public static bool CheckForUpdatesAutomatically
    {
        get => Read(nameof(CheckForUpdatesAutomatically), 1) != 0;
        set => Write(nameof(CheckForUpdatesAutomatically), value ? 1 : 0);
    }

    /// <summary>When the update check last reached GitHub; written by the update service, never by the UI.</summary>
    public static DateTimeOffset? LastUpdateCheckUtc
    {
        get => ReadTime(nameof(LastUpdateCheckUtc));
        set => WriteTime(nameof(LastUpdateCheckUtc), value);
    }

    /// <summary>The version that last ran, so an update from an older version is noticed once.</summary>
    public static string? LastRunVersion
    {
        get => ReadString(nameof(LastRunVersion));
        set => WriteString(nameof(LastRunVersion), value);
    }

    /// <summary>The same store, as the update service sees it.</summary>
    public static IUpdatePreferences UpdatePreferences { get; } = new UpdatePreferencesAdapter();

    public static ToolOptions ToolOptions => new(TempFileAgeHours);

    private sealed class UpdatePreferencesAdapter : IUpdatePreferences
    {
        public bool CheckForUpdatesAutomatically
        {
            get => AppSettings.CheckForUpdatesAutomatically;
            set => AppSettings.CheckForUpdatesAutomatically = value;
        }

        public DateTimeOffset? LastUpdateCheckUtc
        {
            get => AppSettings.LastUpdateCheckUtc;
            set => AppSettings.LastUpdateCheckUtc = value;
        }
    }

    public static bool IsInRunAll(ToolInfo tool)
    {
        using var key = Registry.CurrentUser.OpenSubKey(ToolsKeyPath);
        return key?.GetValue(tool.Id) is int value ? value != 0 : tool.IncludedByDefault;
    }

    public static void SetInRunAll(ToolInfo tool, bool included)
    {
        using var key = Registry.CurrentUser.CreateSubKey(ToolsKeyPath);
        key.SetValue(tool.Id, included ? 1 : 0, RegistryValueKind.DWord);
    }

    private static int Read(string name, int fallback)
    {
        using var key = Registry.CurrentUser.OpenSubKey(KeyPath);
        return key?.GetValue(name) is int value ? value : fallback;
    }

    private static void Write(string name, int value)
    {
        using var key = Registry.CurrentUser.CreateSubKey(KeyPath);
        key.SetValue(name, value, RegistryValueKind.DWord);
    }

    /// <summary>Times are QWORDs of Unix seconds.</summary>
    private static DateTimeOffset? ReadTime(string name)
    {
        using var key = Registry.CurrentUser.OpenSubKey(KeyPath);
        return key?.GetValue(name) is long seconds ? DateTimeOffset.FromUnixTimeSeconds(seconds) : null;
    }

    private static void WriteTime(string name, DateTimeOffset? value)
    {
        using var key = Registry.CurrentUser.CreateSubKey(KeyPath);
        if (value is { } time)
            key.SetValue(name, time.ToUnixTimeSeconds(), RegistryValueKind.QWord);
        else
            key.DeleteValue(name, throwOnMissingValue: false);
    }

    private static string? ReadString(string name)
    {
        using var key = Registry.CurrentUser.OpenSubKey(KeyPath);
        return key?.GetValue(name) as string;
    }

    private static void WriteString(string name, string? value)
    {
        using var key = Registry.CurrentUser.CreateSubKey(KeyPath);
        if (value is null)
            key.DeleteValue(name, throwOnMissingValue: false);
        else
            key.SetValue(name, value, RegistryValueKind.String);
    }
}
