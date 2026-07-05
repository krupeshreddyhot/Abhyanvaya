using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Abhyanvaya.API.Diagnostics;

/// <summary>
/// Point-in-time health snapshot for a single hosted <see cref="BackgroundService"/>, derived
/// entirely from the actual <see cref="IHostedService"/> registration and its own
/// <see cref="BackgroundService.ExecuteTask"/> — no polling loop, timer, or separate tracking
/// service is involved.
/// </summary>
public sealed record BackgroundWorkerStatus(
    bool Registered,
    bool Running,
    string StartupStatus,
    string Health);

/// <summary>
/// Read-only inspector that reports hosted background worker status by querying the DI
/// container's already-resolved <see cref="IHostedService"/> singletons. Since
/// <c>AddHostedService&lt;T&gt;</c> registers <c>T</c> as a singleton <see cref="IHostedService"/>,
/// the instance resolved here is the exact same instance the .NET Generic Host is running —
/// inspecting <see cref="BackgroundService.ExecuteTask"/> reflects the real, current state of the
/// worker with no additional tracking state, timers, or polling required.
/// </summary>
public static class BackgroundWorkerInspector
{
    public static BackgroundWorkerStatus Inspect(IServiceProvider services, Type workerType)
    {
        var instance = services.GetServices<IHostedService>()
            .FirstOrDefault(service => service.GetType() == workerType);

        if (instance is null)
        {
            return new BackgroundWorkerStatus(Registered: false, Running: false, StartupStatus: "NotRegistered", Health: "Unhealthy");
        }

        if (instance is not BackgroundService backgroundService)
        {
            // Registered as IHostedService but not a BackgroundService: no ExecuteTask to inspect.
            return new BackgroundWorkerStatus(Registered: true, Running: true, StartupStatus: "Started", Health: "Healthy");
        }

        var executeTask = backgroundService.ExecuteTask;
        if (executeTask is null)
        {
            // The host has not called StartAsync yet (should not happen once app.StartAsync() has completed).
            return new BackgroundWorkerStatus(Registered: true, Running: false, StartupStatus: "NotStarted", Health: "Unhealthy");
        }

        if (executeTask.IsFaulted)
        {
            return new BackgroundWorkerStatus(Registered: true, Running: false, StartupStatus: "Faulted", Health: "Unhealthy");
        }

        if (executeTask.IsCompleted)
        {
            // Completed while the host is still up: the worker's loop exited early/unexpectedly.
            return new BackgroundWorkerStatus(Registered: true, Running: false, StartupStatus: "Stopped", Health: "Unhealthy");
        }

        return new BackgroundWorkerStatus(Registered: true, Running: true, StartupStatus: "Started", Health: "Healthy");
    }
}
