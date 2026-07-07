using System.Reflection;

namespace Abhyanvaya.API.Diagnostics;

/// <summary>
/// Small, reusable diagnostics helpers shared between the startup configuration summary log
/// (<c>Program.cs</c>) and the platform health endpoints (<c>/health</c>, <c>/health/ready</c>),
/// so both surfaces derive identical values from the same source instead of duplicating lookup logic.
/// </summary>
public static class StartupDiagnostics
{
    public static string ResolveApplicationVersion()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var informationalVersion = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

        return string.IsNullOrWhiteSpace(informationalVersion)
            ? assembly.GetName().Version?.ToString() ?? "Unknown"
            : informationalVersion;
    }

    /// <summary>Maps an EF Core provider assembly name (e.g. <c>Npgsql.EntityFrameworkCore.PostgreSQL</c>) to a friendly display name.</summary>
    public static string DescribeDatabaseProvider(string? efCoreProviderName) =>
        efCoreProviderName?.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) == true
            ? "PostgreSQL"
            : efCoreProviderName ?? "Unknown";

    /// <summary>
    /// Describes the concrete recognition/embedding queue implementation registered in DI.
    /// Falls back to the raw type name for any future implementation (e.g. RabbitMQ, Azure Queue)
    /// so this never needs to change when the queue implementation changes.
    /// </summary>
    public static string DescribeQueueImplementation(object queueInstance) => queueInstance.GetType().Name switch
    {
        nameof(Abhyanvaya.Infrastructure.Recognition.InMemoryClassroomPhotoQueue) => "InMemory Channel",
        nameof(Abhyanvaya.Infrastructure.Embedding.InMemoryStudentPhotoEmbeddingQueue) => "InMemory Channel",
        _ => queueInstance.GetType().Name,
    };
}
