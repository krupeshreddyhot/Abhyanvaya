using Abhyanvaya.Domain.Authorization;

namespace Abhyanvaya.Application.UnitTests.Scheduling.Phase1B;

public sealed class Phase1BPermissionKeysTests
{
    [Theory]
    [InlineData(PermissionKeys.SchedulingFacultyPreferencesView)]
    [InlineData(PermissionKeys.SchedulingFacultyPreferencesManage)]
    [InlineData(PermissionKeys.SchedulingRoomFeaturesView)]
    [InlineData(PermissionKeys.SchedulingRoomFeaturesManage)]
    [InlineData(PermissionKeys.SchedulingSubjectDeliveryView)]
    [InlineData(PermissionKeys.SchedulingSubjectDeliveryManage)]
    [InlineData(PermissionKeys.SchedulingHolidayTypesView)]
    [InlineData(PermissionKeys.SchedulingHolidayTypesManage)]
    public void PermissionKeys_All_ContainsPhase1BKeys(string key)
    {
        Assert.Contains(key, PermissionKeys.All);
    }
}
