using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Domain.Enums;

namespace Abhyanvaya.IntegrationTests.Fixtures;

/// <summary>
/// Test double for authenticated tenant-scoped faculty user.
/// </summary>
public sealed class TestCurrentUserService : ICurrentUserService
{
    public int UserId { get; set; } = 1;

    public string Role { get; set; } = nameof(UserRole.Admin);

    public int TenantId { get; set; } = 1;

    public int StaffId { get; set; }

    public int CourseId { get; set; } = 1;

    public int GroupId { get; set; } = 1;
}
