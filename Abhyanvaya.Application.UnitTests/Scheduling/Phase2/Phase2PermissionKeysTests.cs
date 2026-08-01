using Abhyanvaya.Domain.Authorization;

namespace Abhyanvaya.Application.UnitTests.Scheduling.Phase2;

public sealed class Phase2PermissionKeysTests
{
    [Theory]
    [InlineData(PermissionKeys.SchedulingTimetableView)]
    [InlineData(PermissionKeys.SchedulingTimetableManage)]
    public void PermissionKeys_All_ContainsPhase2Keys(string key)
    {
        Assert.Contains(key, PermissionKeys.All);
    }
}
