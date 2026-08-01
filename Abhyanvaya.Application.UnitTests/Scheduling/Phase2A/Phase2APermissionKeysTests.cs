using Abhyanvaya.Domain.Authorization;

namespace Abhyanvaya.Application.UnitTests.Scheduling.Phase2A;

public sealed class Phase2APermissionKeysTests
{
    [Theory]
    [InlineData(PermissionKeys.SchedulingVersionView)]
    [InlineData(PermissionKeys.SchedulingVersionManage)]
    [InlineData(PermissionKeys.SchedulingReview)]
    [InlineData(PermissionKeys.SchedulingApprove)]
    [InlineData(PermissionKeys.SchedulingPublish)]
    [InlineData(PermissionKeys.SchedulingArchive)]
    [InlineData(PermissionKeys.SchedulingClone)]
    [InlineData(PermissionKeys.SchedulingHistoryView)]
    public void PermissionKeys_All_ContainsPhase2AKeys(string key)
    {
        Assert.Contains(key, PermissionKeys.All);
    }
}
