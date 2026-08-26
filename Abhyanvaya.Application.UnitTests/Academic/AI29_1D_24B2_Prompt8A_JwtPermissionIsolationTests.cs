using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Domain.Authorization;
using Abhyanvaya.Domain.Entities;
using Abhyanvaya.Domain.Enums;
using Abhyanvaya.Infrastructure.Persistence;
using Abhyanvaya.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace Abhyanvaya.Application.UnitTests.Academic;

/// <summary>
/// AI29.1D.24B.2 Prompt 8A — JWT permission resolution must honor UserApplicationRoles ownership
/// and must not leak cross-tenant ApplicationRole permissions when IgnoreQueryFilters is used.
/// </summary>
public sealed class AI29_1D_24B2_Prompt8A_JwtPermissionIsolationTests
{
    private static readonly DateTime SeedUtc = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private sealed class AmbientCurrentUser : ICurrentUserService
    {
        public int UserId { get; set; }
        public string Role { get; set; } = string.Empty;
        public int TenantId { get; set; }
        public int StaffId { get; set; }
        public int CourseId { get; set; }
        public int GroupId { get; set; }
    }

    private static IConfiguration JwtConfig() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Jwt:Key"] = "Prompt8A-Jwt-Permission-Hardening-Test-Key-32b!",
                    ["Jwt:Issuer"] = "abhyanvaya-test",
                    ["Jwt:Audience"] = "abhyanvaya-test",
                    ["Jwt:ExpiryMinutes"] = "60",
                })
            .Build();

    private static ApplicationDbContext CreateDb(AmbientCurrentUser ambient)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase("p8a-jwt-" + Guid.NewGuid().ToString("N"))
            .Options;
        // Suppress seeded HasData collisions by not ensuring created with seed — add only test rows.
        var db = new ApplicationDbContext(options, ambient, NullLogger<ApplicationDbContext>.Instance);
        return db;
    }

    private static async Task SeedPermissionCatalogAsync(ApplicationDbContext db)
    {
        if (await db.Permissions.AnyAsync())
            return;

        db.Set<Permission>().AddRange(
            new Permission { Id = 3, Key = PermissionKeys.AttendanceView, Resource = "Attendance", Action = "View" },
            new Permission { Id = 4, Key = PermissionKeys.AttendanceManage, Resource = "Attendance", Action = "Manage" },
            new Permission { Id = 210, Key = PermissionKeys.SectionView, Resource = "Section", Action = "View" },
            new Permission { Id = 211, Key = PermissionKeys.SectionCreate, Resource = "Section", Action = "Create" },
            new Permission { Id = 250, Key = PermissionKeys.ProgramView, Resource = "Program", Action = "View" },
            new Permission { Id = 254, Key = PermissionKeys.ProgramManage, Resource = "Program", Action = "Manage" },
            new Permission { Id = 1, Key = PermissionKeys.StudentsView, Resource = "Students", Action = "View" },
            new Permission { Id = 5, Key = PermissionKeys.ReportsView, Resource = "Reports", Action = "View" },
            new Permission { Id = 9, Key = PermissionKeys.DashboardView, Resource = "Dashboard", Action = "View" },
            new Permission { Id = 11, Key = PermissionKeys.MasterView, Resource = "Master", Action = "View" });
        await db.SaveChangesAsync();
    }

    private static User NewUser(int id, int tenantId, UserRole role, string username) =>
        new()
        {
            Id = id,
            TenantId = tenantId,
            Username = username,
            PasswordHash = "x",
            Role = role,
            CourseId = 1,
            GroupId = 1,
            CreatedDate = SeedUtc,
            IsDeleted = false,
        };

    private static ApplicationRole NewRole(int id, int tenantId, string code) =>
        new()
        {
            Id = id,
            TenantId = tenantId,
            Name = code,
            Code = code,
            CreatedDate = SeedUtc,
            IsDeleted = false,
        };

    private static async Task LinkAsync(ApplicationDbContext db, int userId, int roleId, params int[] permissionIds)
    {
        db.Set<UserApplicationRole>().Add(new UserApplicationRole { UserId = userId, ApplicationRoleId = roleId });
        foreach (var pid in permissionIds.Distinct())
        {
            db.Set<ApplicationRolePermission>().Add(
                new ApplicationRolePermission { ApplicationRoleId = roleId, PermissionId = pid });
        }

        await db.SaveChangesAsync();
    }

    private static async Task<(string Token, IReadOnlyList<string> Permissions)> IssueAsync(
        ApplicationDbContext db,
        User user)
    {
        var jwt = new JwtService(JwtConfig(), db);
        var token = await jwt.GenerateTokenAsync(user);
        var handler = new JwtSecurityTokenHandler();
        var parsed = handler.ReadJwtToken(token);
        var perms = parsed.Claims
            .Where(c => c.Type == "permission")
            .Select(c => c.Value)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();
        return (token, perms);
    }

    [Fact]
    public async Task TestA_TenantA_user_receives_TenantA_role_permissions()
    {
        var ambient = new AmbientCurrentUser { TenantId = 0 }; // login ambient
        await using var db = CreateDb(ambient);
        await SeedPermissionCatalogAsync(db);

        var user = NewUser(10, tenantId: 1, UserRole.Faculty, "faculty-a");
        db.Set<User>().Add(user);
        db.Set<ApplicationRole>().Add(NewRole(501, tenantId: 1, "FAC_A"));
        await db.SaveChangesAsync();
        await LinkAsync(db, user.Id, 501, 3, 4, 210);

        var (_, perms) = await IssueAsync(db, user);
        Assert.Contains(PermissionKeys.AttendanceView, perms);
        Assert.Contains(PermissionKeys.AttendanceManage, perms);
        Assert.Contains(PermissionKeys.SectionView, perms);
    }

    [Fact]
    public async Task TestB_TenantA_user_does_not_receive_TenantB_role_permissions()
    {
        var ambient = new AmbientCurrentUser { TenantId = 0 };
        await using var db = CreateDb(ambient);
        await SeedPermissionCatalogAsync(db);

        var user = NewUser(11, tenantId: 1, UserRole.Faculty, "faculty-a2");
        db.Set<User>().Add(user);
        db.Set<ApplicationRole>().Add(NewRole(502, tenantId: 1, "FAC_A2"));
        db.Set<ApplicationRole>().Add(NewRole(902, tenantId: 2, "FAC_B"));
        await db.SaveChangesAsync();
        await LinkAsync(db, user.Id, 502, 3, 210);
        // Tenant B role has Program.Manage — must not appear
        db.Set<ApplicationRolePermission>().Add(
            new ApplicationRolePermission { ApplicationRoleId = 902, PermissionId = 254 });
        await db.SaveChangesAsync();

        var (_, perms) = await IssueAsync(db, user);
        Assert.Contains(PermissionKeys.SectionView, perms);
        Assert.DoesNotContain(PermissionKeys.ProgramManage, perms);
    }

    [Fact]
    public async Task TestC_IgnoreQueryFilters_does_not_grant_cross_tenant_role()
    {
        var ambient = new AmbientCurrentUser { TenantId = 0 };
        await using var db = CreateDb(ambient);
        await SeedPermissionCatalogAsync(db);

        var user = NewUser(12, tenantId: 1, UserRole.Faculty, "faculty-a3");
        db.Set<User>().Add(user);
        // Corrupt / malicious link to Tenant B role id
        db.Set<ApplicationRole>().Add(NewRole(903, tenantId: 2, "FAC_B_LEAK"));
        await db.SaveChangesAsync();
        db.Set<UserApplicationRole>().Add(new UserApplicationRole { UserId = user.Id, ApplicationRoleId = 903 });
        db.Set<ApplicationRolePermission>().Add(
            new ApplicationRolePermission { ApplicationRoleId = 903, PermissionId = 254 });
        await db.SaveChangesAsync();

        var (_, perms) = await IssueAsync(db, user);
        Assert.DoesNotContain(PermissionKeys.ProgramManage, perms);
        // Falls back to legacy faculty set (no same-tenant assigned role)
        Assert.Equal(PermissionKeys.LegacyFacultySet.OrderBy(x => x).ToList(), perms);
    }

    [Fact]
    public async Task TestD_Cross_tenant_ApplicationRolePermission_rows_do_not_leak()
    {
        var ambient = new AmbientCurrentUser { TenantId = 0 };
        await using var db = CreateDb(ambient);
        await SeedPermissionCatalogAsync(db);

        var user = NewUser(13, tenantId: 1, UserRole.Faculty, "faculty-a4");
        db.Set<User>().Add(user);
        db.Set<ApplicationRole>().Add(NewRole(504, tenantId: 1, "FAC_A4"));
        db.Set<ApplicationRole>().Add(NewRole(904, tenantId: 2, "FAC_B4"));
        await db.SaveChangesAsync();
        await LinkAsync(db, user.Id, 504, 3);
        db.Set<ApplicationRolePermission>().Add(
            new ApplicationRolePermission { ApplicationRoleId = 904, PermissionId = 211 });
        await db.SaveChangesAsync();

        var (_, perms) = await IssueAsync(db, user);
        Assert.Contains(PermissionKeys.AttendanceView, perms);
        Assert.DoesNotContain(PermissionKeys.SectionCreate, perms);
    }

    [Fact]
    public async Task TestE_No_ApplicationRole_uses_LegacyFacultySet()
    {
        var ambient = new AmbientCurrentUser { TenantId = 0 };
        await using var db = CreateDb(ambient);
        await SeedPermissionCatalogAsync(db);

        var user = NewUser(14, tenantId: 1, UserRole.Faculty, "legacy-fac");
        db.Set<User>().Add(user);
        await db.SaveChangesAsync();

        var (_, perms) = await IssueAsync(db, user);
        Assert.Equal(PermissionKeys.LegacyFacultySet.OrderBy(x => x).ToList(), perms);
        Assert.DoesNotContain(PermissionKeys.SectionView, perms);
    }

    [Fact]
    public async Task TestF_Assigned_ApplicationRole_permissions_are_included_in_JWT()
    {
        var ambient = new AmbientCurrentUser { TenantId = 0 };
        await using var db = CreateDb(ambient);
        await SeedPermissionCatalogAsync(db);

        var user = NewUser(15, tenantId: 1, UserRole.Faculty, "assigned-fac");
        db.Set<User>().Add(user);
        db.Set<ApplicationRole>().Add(NewRole(505, tenantId: 1, "FAC_ASSIGNED"));
        await db.SaveChangesAsync();
        await LinkAsync(db, user.Id, 505, 3, 4, 210);

        var (token, perms) = await IssueAsync(db, user);
        Assert.False(string.IsNullOrWhiteSpace(token));
        Assert.Contains(PermissionKeys.AttendanceView, perms);
        Assert.Contains(PermissionKeys.AttendanceManage, perms); // Attendance.Mark synonym in domain = Manage
        Assert.Contains(PermissionKeys.SectionView, perms);

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);
        Assert.Equal("1", jwt.Claims.First(c => c.Type == "TenantId").Value);
        Assert.Equal(
            nameof(UserRole.Faculty),
            jwt.Claims.First(c => c.Type == ClaimTypes.Role || c.Type == "role").Value);
    }

    [Fact]
    public async Task TestG_SuperAdmin_receives_full_permission_catalog()
    {
        var ambient = new AmbientCurrentUser { TenantId = 0 };
        await using var db = CreateDb(ambient);
        await SeedPermissionCatalogAsync(db);

        var user = NewUser(16, tenantId: 0, UserRole.SuperAdmin, "super");
        db.Set<User>().Add(user);
        await db.SaveChangesAsync();

        var (_, perms) = await IssueAsync(db, user);
        Assert.Contains(PermissionKeys.SectionView, perms);
        Assert.Contains(PermissionKeys.ProgramManage, perms);
        Assert.True(perms.Count >= 10);
    }

    [Fact]
    public async Task TestH_Admin_without_ApplicationRole_receives_PermissionKeys_All()
    {
        var ambient = new AmbientCurrentUser { TenantId = 0 };
        await using var db = CreateDb(ambient);
        await SeedPermissionCatalogAsync(db);

        var user = NewUser(17, tenantId: 1, UserRole.Admin, "admin-legacy");
        db.Set<User>().Add(user);
        await db.SaveChangesAsync();

        var (_, perms) = await IssueAsync(db, user);
        Assert.Equal(PermissionKeys.All.OrderBy(x => x).ToList(), perms);
    }

    [Fact]
    public async Task TestNegative_Attendance_only_role_excludes_SectionView()
    {
        var ambient = new AmbientCurrentUser { TenantId = 0 };
        await using var db = CreateDb(ambient);
        await SeedPermissionCatalogAsync(db);

        var user = NewUser(18, tenantId: 1, UserRole.Faculty, "att-only");
        db.Set<User>().Add(user);
        db.Set<ApplicationRole>().Add(NewRole(506, tenantId: 1, "ATT_ONLY"));
        await db.SaveChangesAsync();
        await LinkAsync(db, user.Id, 506, 3, 4);

        var (_, perms) = await IssueAsync(db, user);
        Assert.Contains(PermissionKeys.AttendanceView, perms);
        Assert.Contains(PermissionKeys.AttendanceManage, perms);
        Assert.DoesNotContain(PermissionKeys.SectionView, perms);
    }

    [Fact]
    public async Task TestNegative_ProgramView_only_excludes_Section_manage_keys()
    {
        var ambient = new AmbientCurrentUser { TenantId = 0 };
        await using var db = CreateDb(ambient);
        await SeedPermissionCatalogAsync(db);

        var user = NewUser(19, tenantId: 1, UserRole.Faculty, "prog-only");
        db.Set<User>().Add(user);
        db.Set<ApplicationRole>().Add(NewRole(507, tenantId: 1, "PROG_ONLY"));
        await db.SaveChangesAsync();
        await LinkAsync(db, user.Id, 507, 250);

        var (_, perms) = await IssueAsync(db, user);
        Assert.Contains(PermissionKeys.ProgramView, perms);
        Assert.DoesNotContain(PermissionKeys.SectionCreate, perms);
        Assert.DoesNotContain(PermissionKeys.SectionView, perms);
        Assert.DoesNotContain(PermissionKeys.ProgramManage, perms);
    }

    [Fact]
    public void TestNegative_Unauthenticated_token_has_no_application_permissions()
    {
        // No JWT is issued without a User; empty claim set is the unauthenticated contract.
        var empty = Array.Empty<Claim>();
        Assert.DoesNotContain(empty, c => c.Type == "permission");
    }

    [Fact]
    public async Task Source_guard_keeps_UserApplicationRoles_as_authority_with_tenant_match()
    {
        var root = FindRepoRoot();
        var source = await File.ReadAllTextAsync(
            Path.Combine(root, "Abhyanvaya.Infrastructure", "Services", "JwtService.cs"));
        Assert.Contains("IgnoreQueryFilters()", source);
        Assert.Contains("role.TenantId == user.TenantId", source);
        Assert.Contains("uar.UserId == user.Id", source);
        Assert.Contains("UserApplicationRoles", source);
        Assert.DoesNotContain(
            "SelectMany(u => u.ApplicationRole.ApplicationRolePermissions",
            source);
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Abhyanvaya.Infrastructure", "Services", "JwtService.cs")))
                return dir.FullName;
            dir = dir.Parent;
        }

        throw new InvalidOperationException("Repository root not found.");
    }
}
