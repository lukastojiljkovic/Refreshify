using System.ServiceProcess;

namespace Refreshify.Core.Platform;

public sealed record ServiceState(string DisplayName, ServiceStartMode StartType, ServiceControllerStatus Status);

/// <summary>Windows services through the Service Control Manager. Stopping and starting needs the elevated worker.</summary>
public static class Services
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(60);

    /// <summary>The service's state, or <see langword="null"/> when it isn't installed.</summary>
    public static ServiceState? Query(string name)
    {
        using var service = new ServiceController(name);
        try
        {
            return new ServiceState(service.DisplayName, service.StartType, service.Status);
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    /// <summary>
    /// Stops the service and the services that depend on it, and returns those that were running in the order to start them
    /// again, so the caller can restore the previous state.
    /// </summary>
    public static Task<IReadOnlyList<string>> StopAsync(string name, CancellationToken cancellationToken) => Task.Run<IReadOnlyList<string>>(() =>
    {
        using var service = new ServiceController(name);
        if (service.Status == ServiceControllerStatus.Stopped)
            return [];

        // Dependent services are listed in stopping order.
        var dependents = service.DependentServices
            .Where(dependent => dependent.Status != ServiceControllerStatus.Stopped)
            .Select(dependent => dependent.ServiceName)
            .Reverse();
        IReadOnlyList<string> running = [service.ServiceName, .. dependents];

        if (service.Status != ServiceControllerStatus.StopPending)
            service.Stop();
        Wait(service, ServiceControllerStatus.Stopped, "stop", cancellationToken);
        return running;
    }, cancellationToken);

    public static Task StartAsync(string name, CancellationToken cancellationToken) => Task.Run(() =>
    {
        using var service = new ServiceController(name);
        if (service.Status == ServiceControllerStatus.Running)
            return;

        if (service.Status != ServiceControllerStatus.StartPending)
            service.Start();
        Wait(service, ServiceControllerStatus.Running, "start", cancellationToken);
    }, cancellationToken);

    private static void Wait(ServiceController service, ServiceControllerStatus status, string verb, CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow + Timeout;
        for (service.Refresh(); service.Status != status; service.Refresh())
        {
            if (DateTime.UtcNow > deadline)
                throw new System.TimeoutException($"The {service.DisplayName} service didn't {verb} in time.");
            cancellationToken.WaitHandle.WaitOne(TimeSpan.FromMilliseconds(250));
            cancellationToken.ThrowIfCancellationRequested();
        }
    }
}
