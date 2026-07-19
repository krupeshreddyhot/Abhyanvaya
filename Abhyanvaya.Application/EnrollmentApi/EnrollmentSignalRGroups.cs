namespace Abhyanvaya.Application.EnrollmentApi;

public static class EnrollmentSignalRGroups
{
    public static string Tenant(int tenantId) => $"enrollment-tenant:{tenantId}";

    public static string Batch(Guid batchId) => $"enrollment-batch:{batchId}";

    public static string Recognition(int tenantId) => $"recognition-tenant:{tenantId}";

    public static string RecognitionWorker(int tenantId) => $"recognition-worker:{tenantId}";
}
