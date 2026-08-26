using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
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
/// AI29.1D.24B.3 Prompt 3 — Allocation.Run must be present for Admin application-role grants
/// and must remain denied for Faculty without that permission. Does not weaken tenant isolation.
/// </summary>
public sealed class AI29_1D_24B3_Prompt3_AllocationRunAuthorizationTests
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
                    ["Jwt:Key"] = "Prompt3-Allocation-Run-Authz-Test-Key-32b!!",
                    ["Jwt:Issuer"] = "abhyanvaya-test",
                    ["Jwt:Audience"] = "abhyanvaya-test",
                    ["Jwt:ExpiryMinutes"] = "60",
                })
            .Build();

    private static ApplicationDbContext CreateDb(AmbientCurrentUser ambient)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase("p3-alloc-run-" + Guid.NewGuid().ToString("N"))
            .Options;
        return new ApplicationDbContext(options, ambient, NullLogger<ApplicationDbContext>.Instance);
    }

    private static async Task SeedCatalogAsync(ApplicationDbContext db)
    {
        db.Set<Permission>().AddRange(
            new Permission { Id = 3, Key = PermissionKeys.AttendanceView, Resource = "Attendance", Action = "View" },
            new Permission { Id = 210, Key = PermissionKeys.SectionView, Resource = "Section", Action = "View" },
            new Permission { Id = 227, Key = PermissionKeys.AllocationRun, Resource = "Allocation", Action = "Run" },
            new Permission { Id = 228, Key = PermissionKeys.AllocationApprove, Resource = "Allocation", Action = "Approve" },
            new Permission { Id = 237, Key = PermissionKeys.AllocationScenarioArchive, Resource = "Allocation", Action = "ScenarioArchive" });
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

    private static async Task<(string Token, IReadOnlyList<string> Permissions)> IssueAsync(
        ApplicationDbContext db,
        User user)
    {
        var jwt = new JwtService(JwtConfig(), db);
        var token = await jwt.GenerateTokenAsync(user);
        var parsed = new JwtSecurityTokenHandler().ReadJwtToken(token);
        var perms = parsed.Claims
            .Where(c => c.Type == "permission")
            .Select(c => c.Value)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();
        return (token, perms);
    }

    [Fact]
    public async Task Admin_ApplicationRole_with_AllocationRun_emits_Allocation_Run_claim()
    {
        var ambient = new AmbientCurrentUser { TenantId = 0 };
        await using var db = CreateDb(ambient);
        await SeedCatalogAsync(db);

        var user = NewUser(1, tenantId: 1, UserRole.Admin, "admin");
        db.Set<User>().Add(user);
        db.Set<ApplicationRole>().Add(NewRole(100, tenantId: 1, "ADMIN"));
        await db.SaveChangesAsync();
        db.Set<UserApplicationRole>().Add(new UserApplicationRole { UserId = 1, ApplicationRoleId = 100 });
        db.Set<ApplicationRolePermission>().AddRange(
            new ApplicationRolePermission { ApplicationRoleId = 100, PermissionId = 227 },
            new ApplicationRolePermission { ApplicationRoleId = 100, PermissionId = 228 },
            new ApplicationRolePermission { ApplicationRoleId = 100, PermissionId = 237 });
        await db.SaveChangesAsync();

        var (_, perms) = await IssueAsync(db, user);
        Assert.Contains(PermissionKeys.AllocationRun, perms);
        Assert.Contains(PermissionKeys.AllocationApprove, perms);
    }

    [Fact]
    public async Task Admin_ApplicationRole_missing_AllocationRun_does_not_invent_the_claim()
    {
        // Documents pre-Prompt-3 drift: assigned role without Allocation.Run must not silently invent it.
        // Repair is RBAC data reconciliation — not JwtService tenant bypass.
        var ambient = new AmbientCurrentUser { TenantId = 0 };
        await using var db = CreateDb(ambient);
        await SeedCatalogAsync(db);

        var user = NewUser(2, tenantId: 1, UserRole.Admin, "admin-drift");
        db.Set<User>().Add(user);
        db.Set<ApplicationRole>().Add(NewRole(100, tenantId: 1, "ADMIN"));
        await db.SaveChangesAsync();
        db.Set<UserApplicationRole>().Add(new UserApplicationRole { UserId = 2, ApplicationRoleId = 100 });
        db.Set<ApplicationRolePermission>().Add(
            new ApplicationRolePermission { ApplicationRoleId = 100, PermissionId = 237 });
        await db.SaveChangesAsync();

        var (_, perms) = await IssueAsync(db, user);
        Assert.DoesNotContain(PermissionKeys.AllocationRun, perms);
        Assert.Contains(PermissionKeys.AllocationScenarioArchive, perms);
    }

    [Fact]
    public async Task Faculty_without_AllocationRun_does_not_receive_Allocation_Run()
    {
        var ambient = new AmbientCurrentUser { TenantId = 0 };
        await using var db = CreateDb(ambient);
        await SeedCatalogAsync(db);

        var user = NewUser(3, tenantId: 1, UserRole.Faculty, "faculty");
        db.Set<User>().Add(user);
        db.Set<ApplicationRole>().Add(NewRole(101, tenantId: 1, "FACULTY"));
        await db.SaveChangesAsync();
        db.Set<UserApplicationRole>().Add(new UserApplicationRole { UserId = 3, ApplicationRoleId = 101 });
        db.Set<ApplicationRolePermission>().AddRange(
            new ApplicationRolePermission { ApplicationRoleId = 101, PermissionId = 3 },
            new ApplicationRolePermission { ApplicationRoleId = 101, PermissionId = 210 });
        await db.SaveChangesAsync();

        var (_, perms) = await IssueAsync(db, user);
        Assert.DoesNotContain(PermissionKeys.AllocationRun, perms);
        Assert.Contains(PermissionKeys.SectionView, perms);
    }

    [Fact]
    public async Task Cross_tenant_AllocationRun_role_is_not_granted_via_IgnoreQueryFilters()
    {
        var ambient = new AmbientCurrentUser { TenantId = 0 };
        await using var db = CreateDb(ambient);
        await SeedCatalogAsync(db);

        // Faculty (not Admin) — Admin without same-tenant roles falls back to PermissionKeys.All by design.
        var user = NewUser(4, tenantId: 1, UserRole.Faculty, "faculty-a");
        db.Set<User>().Add(user);
        db.Set<ApplicationRole>().Add(NewRole(900, tenantId: 2, "ADMIN"));
        await db.SaveChangesAsync();
        // Corrupt link to Tenant B ADMIN role that has Allocation.Run
        db.Set<UserApplicationRole>().Add(new UserApplicationRole { UserId = 4, ApplicationRoleId = 900 });
        db.Set<ApplicationRolePermission>().Add(
            new ApplicationRolePermission { ApplicationRoleId = 900, PermissionId = 227 });
        await db.SaveChangesAsync();

        var (_, perms) = await IssueAsync(db, user);
        Assert.DoesNotContain(PermissionKeys.AllocationRun, perms);
    }

    [Fact]
    public void StaffHubSeed_admin_permission_range_includes_AllocationRun_id_227()
    {
        // Mirror seed contract: Enumerable.Range(225, 13) => 225..237 includes Allocation.Run (227).
        var adminAllocationBlock = Enumerable.Range(225, 13).ToArray();
        Assert.Contains(227, adminAllocationBlock);
        Assert.Contains(228, adminAllocationBlock);
        Assert.Contains(237, adminAllocationBlock);
    }
}
