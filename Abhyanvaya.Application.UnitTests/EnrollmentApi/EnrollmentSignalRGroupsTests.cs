using Abhyanvaya.Application.EnrollmentApi;

namespace Abhyanvaya.Application.UnitTests.EnrollmentApi;

public sealed class EnrollmentSignalRGroupsTests
{
    [Fact]
    public void Tenant_group_is_scoped_by_tenant_id()
    {
        Assert.Equal("enrollment-tenant:42", EnrollmentSignalRGroups.Tenant(42));
    }

    [Fact]
    public void Batch_group_is_scoped_by_batch_id()
    {
        var batchId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        Assert.Equal($"enrollment-batch:{batchId}", EnrollmentSignalRGroups.Batch(batchId));
    }

    [Fact]
    public void Tenant_groups_are_unique_per_tenant()
    {
        Assert.NotEqual(EnrollmentSignalRGroups.Tenant(1), EnrollmentSignalRGroups.Tenant(2));
    }
}
