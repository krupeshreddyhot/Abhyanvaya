namespace Abhyanvaya.Infrastructure.Enrollment.Background;

internal static class EnrollmentWorkerIdentity
{
    public static string CreateWorkerId() =>
        $"{Environment.MachineName}:{Environment.ProcessId}:{Guid.NewGuid():N}";

    public static string NodeId => Environment.MachineName;
}
