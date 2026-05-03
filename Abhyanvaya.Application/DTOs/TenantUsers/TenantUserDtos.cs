namespace Abhyanvaya.Application.DTOs.TenantUsers;

public sealed class CreateTenantUserRequest
{
    public required string Username { get; set; }
    public required string Password { get; set; }

    /// <summary><see cref="Domain.Enums.UserRole.Admin"/> or <see cref="Domain.Enums.UserRole.Faculty"/>.</summary>
    public required string Role { get; set; }

    /// <summary>Required when <c>Role</c> is Faculty.</summary>
    public int? CourseId { get; set; }

    /// <summary>Required when <c>Role</c> is Faculty.</summary>
    public int? GroupId { get; set; }

    /// <summary>Required when <c>Role</c> is Faculty — directory row whose subject assignments define access.</summary>
    public int? StaffId { get; set; }

    public IReadOnlyList<int> ApplicationRoleIds { get; set; } = [];
}

public sealed class AdminResetUserPasswordRequest
{
    public required string NewPassword { get; set; }
}

public sealed class LinkUserStaffRequest
{
    /// <summary>Set to link Faculty login to directory row; clear with null.</summary>
    public int? StaffId { get; set; }
}
