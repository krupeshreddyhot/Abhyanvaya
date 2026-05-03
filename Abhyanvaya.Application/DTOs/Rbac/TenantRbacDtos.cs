namespace Abhyanvaya.Application.DTOs.Rbac;

public sealed class PermissionCatalogDto
{
    public int Id { get; set; }
    public string Key { get; set; } = null!;
    public string Resource { get; set; } = null!;
    public string Action { get; set; } = null!;
}

public sealed class ApplicationRoleListDto
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public string Code { get; set; } = null!;
    public string? Description { get; set; }
    public int PermissionCount { get; set; }
    public int AssignedUserCount { get; set; }
}

public sealed class ApplicationRoleDetailDto
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public string Code { get; set; } = null!;
    public string? Description { get; set; }
    public IReadOnlyList<int> PermissionIds { get; set; } = [];
}

public sealed class CreateApplicationRoleRequest
{
    public required string Name { get; set; }
    public required string Code { get; set; }
    public string? Description { get; set; }
}

public sealed class UpdateApplicationRoleRequest
{
    public required string Name { get; set; }
    public string? Description { get; set; }
}

public sealed class SetApplicationRolePermissionsRequest
{
    public IReadOnlyList<int> PermissionIds { get; set; } = [];
}

public sealed class TenantUserRbacDto
{
    public int Id { get; set; }
    public string Username { get; set; } = null!;
    public string EnumRole { get; set; } = null!;
    public int? StaffId { get; set; }
    public IReadOnlyList<int> ApplicationRoleIds { get; set; } = [];
}

public sealed class SetUserApplicationRolesRequest
{
    public IReadOnlyList<int> ApplicationRoleIds { get; set; } = [];
}
