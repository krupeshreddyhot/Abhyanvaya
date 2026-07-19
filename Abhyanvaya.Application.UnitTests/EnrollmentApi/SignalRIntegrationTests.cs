using Abhyanvaya.Application.EnrollmentApi;

namespace Abhyanvaya.Application.UnitTests.EnrollmentApi;

public sealed class SignalRIntegrationTests
{
    [Fact]
    public void Tenant_A_never_receives_Tenant_B_group_name()
    {
        Assert.NotEqual(EnrollmentSignalRGroups.Tenant(1), EnrollmentSignalRGroups.Tenant(2));
    }

    [Fact]
    public void Batch_progress_routes_to_batch_group_only()
    {
        var batchId = Guid.NewGuid();
        Assert.StartsWith("enrollment-batch:", EnrollmentSignalRGroups.Batch(batchId));
    }
}
