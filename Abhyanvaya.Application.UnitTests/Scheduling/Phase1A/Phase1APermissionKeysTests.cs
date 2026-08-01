using Abhyanvaya.Domain.Authorization;

namespace Abhyanvaya.Application.UnitTests.Scheduling.Phase1A;

public sealed class Phase1APermissionKeysTests
{
    [Theory]
    [InlineData(PermissionKeys.SchedulingRoomAvailabilityView)]
    [InlineData(PermissionKeys.SchedulingRoomAvailabilityManage)]
    [InlineData(PermissionKeys.SchedulingFacultyAvailabilityView)]
    [InlineData(PermissionKeys.SchedulingFacultyAvailabilityManage)]
    [InlineData(PermissionKeys.SchedulingTemplateView)]
    [InlineData(PermissionKeys.SchedulingTemplateManage)]
    public void PermissionKeys_All_ContainsPhase1AKeys(string key)
    {
        Assert.Contains(key, PermissionKeys.All);
    }

    [Fact]
    public void PermissionKeys_All_DoesNotContainRetiredSchedulingDepartmentKeys()
    {
#pragma warning disable CS0618
        Assert.DoesNotContain(PermissionKeys.SchedulingDepartmentView, PermissionKeys.All);
        Assert.DoesNotContain(PermissionKeys.SchedulingDepartmentManage, PermissionKeys.All);
#pragma warning restore CS0618
    }
}
