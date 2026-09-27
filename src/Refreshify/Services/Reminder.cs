using System.Security;
using System.Security.Principal;
using System.Text;
using Microsoft.Win32;
using Refreshify.Core.Engine;
using Refreshify.Core.Platform;
using Refreshify.Models;
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;

namespace Refreshify.Services;

/// <summary>
/// The optional reminder to refresh Windows. A per-user scheduled task starts <c>Refreshify.exe --reminder</c> daily;
/// that check shows a notification when one is due, then exits. Clicking the notification opens <c>refreshify:</c>,
/// which starts Refreshify, or brings it to the front.
/// </summary>
internal static class Reminder
{
    private const string AppId = "LukaStojiljkovic.Refreshify";
    private const string AppIdKeyPath = @"Software\Classes\AppUserModelId\" + AppId;
    private const string ProtocolKeyPath = @"Software\Classes\refreshify";

    // Every user on a PC shares the task namespace, so each user's task carries their SID.
    private static readonly string TaskName = $"Refreshify reminder-{WindowsIdentity.GetCurrent().User}";

    /// <summary>Creates the daily task when the reminder is on, and removes it when it's off.</summary>
    /// <returns><see langword="false"/> if Task Scheduler didn't accept the task.</returns>
    public static async Task<bool> ScheduleAsync(int months)
    {
        if (months == 0)
        {
            // A task left behind is harmless: the check finds the reminder off and exits.
            await SchtasksAsync($"/Delete /TN \"{TaskName}\" /F");
            return true;
        }

        var xml = Path.Combine(Path.GetTempPath(), $"refreshify-reminder-{Guid.NewGuid():N}.xml");
        await File.WriteAllTextAsync(xml, TaskXml(), Encoding.Unicode);
        try
        {
            return await SchtasksAsync($"/Create /TN \"{TaskName}\" /XML \"{xml}\" /F") == 0;
        }
        finally
        {
            File.Delete(xml);
        }
    }

    /// <summary>The scheduled task's check.</summary>
    public static void ShowIfDue()
    {
        var now = DateTimeOffset.Now;
        if (!ReminderSchedule.IsDue(AppSettings.ReminderMonths, now, AppSettings.LastFullRefresh, AppSettings.LastReminder))
            return;

        Register();
        var last = AppSettings.LastFullRefresh is { } time ? $"Your last full refresh was on {RunModel.When(time)}. " : string.Empty;
        var toast = new XmlDocument();
        toast.LoadXml($"""
            <toast activationType="protocol" launch="refreshify:">
              <visual>
                <binding template="ToastGeneric">
                  <text>Time to refresh Windows</text>
                  <text>{SecurityElement.Escape(last)}Run all cleans up, repairs and updates your PC in one go.</text>
                </binding>
              </visual>
            </toast>
            """);
        ToastNotificationManager.CreateToastNotifier(AppId).Show(new ToastNotification(toast));
        AppSettings.LastReminder = now;
    }

    /// <summary>Removes the task, the notification sender and the link, for the uninstaller.</summary>
    public static async Task RemoveAsync()
    {
        await ScheduleAsync(0);
        Registry.CurrentUser.DeleteSubKeyTree(AppIdKeyPath, throwOnMissingSubKey: false);
        Registry.CurrentUser.DeleteSubKeyTree(ProtocolKeyPath, throwOnMissingSubKey: false);
    }

    /// <summary>Names the notification's sender, and makes <c>refreshify:</c> start Refreshify without arguments.</summary>
    private static void Register()
    {
        using (var app = Registry.CurrentUser.CreateSubKey(AppIdKeyPath))
        {
            app.SetValue("DisplayName", "Refreshify");
            app.SetValue("IconUri", Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.png"));
        }

        using var protocol = Registry.CurrentUser.CreateSubKey(ProtocolKeyPath);
        protocol.SetValue(null, "URL:Refreshify");
        protocol.SetValue("URL Protocol", string.Empty);
        using var command = protocol.CreateSubKey(@"shell\open\command");
        command.SetValue(null, $"\"{Environment.ProcessPath}\"");
    }

    private static async Task<int> SchtasksAsync(string arguments) =>
        (await ProcessRunner.Default.RunAsync(ProcessSpec.System32("schtasks.exe", arguments), null, CancellationToken.None)).ExitCode;

    /// <summary>Daily at noon, or as soon as the PC is on after a missed noon; never elevated.</summary>
    private static string TaskXml() => $"""
        <?xml version="1.0" encoding="UTF-16"?>
        <Task version="1.2" xmlns="http://schemas.microsoft.com/windows/2004/02/mit/task">
          <RegistrationInfo>
            <Author>Refreshify</Author>
            <Description>Reminds you to refresh Windows. Turn it off in Refreshify's settings.</Description>
          </RegistrationInfo>
          <Triggers>
            <CalendarTrigger>
              <StartBoundary>{DateTime.Today.AddHours(12):s}</StartBoundary>
              <ScheduleByDay><DaysInterval>1</DaysInterval></ScheduleByDay>
            </CalendarTrigger>
          </Triggers>
          <Principals>
            <Principal id="Author">
              <UserId>{WindowsIdentity.GetCurrent().User}</UserId>
              <LogonType>InteractiveToken</LogonType>
              <RunLevel>LeastPrivilege</RunLevel>
            </Principal>
          </Principals>
          <Settings>
            <StartWhenAvailable>true</StartWhenAvailable>
            <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>
            <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>
            <ExecutionTimeLimit>PT5M</ExecutionTimeLimit>
            <MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>
          </Settings>
          <Actions Context="Author">
            <Exec>
              <Command>{SecurityElement.Escape(Environment.ProcessPath)}</Command>
              <Arguments>--reminder</Arguments>
            </Exec>
          </Actions>
        </Task>
        """;
}
