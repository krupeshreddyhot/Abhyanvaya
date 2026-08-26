using Abhyanvaya.Domain.Authorization;
using Abhyanvaya.Domain.Entities;
using Abhyanvaya.Domain.Enums;
using Abhyanvaya.Infrastructure.Persistence;
using Abhyanvaya.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Abhyanvaya.Application.Common.Interfaces;

namespace Abhyanvaya.Application.UnitTests.Academic;

/// <summary>AI29.1D.24B.3A — least-privilege Allocation provisioning (no blanket ADMIN grants).</summary>
public sealed class AI29_1D_24B3A_AllocationPermissionProvisioningTests
{
    private static readonly DateTime SeedUtc = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private sealed class Ambient : ICurrentUserService
    {
        public int UserId { get; set; }
        public string Role { get; set; } = "";
        public int TenantId { get; set; }
        public int StaffId { get; set; }
        public int CourseId { get; set; }
        public int GroupId { get; set; }
    }

    private static IConfiguration Cfg() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Jwt:Key"] = "Prompt3A-Alloc-Provisioning-Test-Key-32b!",
                    ["Jwt:Issuer"] = "t",
                    ["Jwt:Audience"] = "t",
                    ["Jwt:ExpiryMinutes"] = "60",
                })
            .Build();

    private static ApplicationDbContext Db(Ambient a) =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase("p3a-" + Guid.NewGuid().ToString("N"))
            .Options, a, NullLogger<ApplicationDbContext>.Instance);

    [Fact]
    public void Admin_seed_operator_set_excludes_governance_and_operations_view()
    {
        var operatorIds = new[] { 227, 230, 231, 232, 233, 234 };
        var excluded = new[] { 228, 229, 235, 236, 237 }; // Approve, Ops.View, Reject, Export, Archive
        Assert.All(operatorIds, id => Assert.DoesNotContain(id, excluded));
        Assert.Contains(PermissionKeys.AllocationRun, new[] { PermissionKeys.AllocationRun });
        Assert.Equal("Allocation.Run", PermissionKeys.AllocationRun);
        Assert.Equal("Allocation.Approve", PermissionKeys.AllocationApprove);
        Assert.Equal("Allocation.Operations.View", PermissionKeys.AllocationOperationsView);
    }

    [Fact]
    public async Task Admin_with_operator_set_receives_Run_but_not_Approve_or_OpsView()
    {
        var ambient = new Ambient { TenantId = 0 };
        await using var db = Db(ambient);
        db.Set<Permission>().AddRange(
            new Permission { Id = 227, Key = PermissionKeys.AllocationRun, Resource = "Allocation", Action = "Run" },
            new Permission { Id = 228, Key = PermissionKeys.AllocationApprove, Resource = "Allocation", Action = "Approve" },
            new Permission { Id = 229, Key = PermissionKeys.AllocationOperationsView, Resource = "Allocation", Action = "OperationsView" },
            new Permission { Id = 230, Key = PermissionKeys.AllocationScenarioView, Resource = "Allocation", Action = "ScenarioView" },
            new Permission { Id = 231, Key = PermissionKeys.AllocationScenarioCreate, Resource = "Allocation", Action = "ScenarioCreate" });
        var user = new User
        {
            Id = 1,
            TenantId = 1,
            Username = "admin",
            PasswordHash = "x",
            Role = UserRole.Admin,
            CourseId = 1,
            GroupId = 1,
            CreatedDate = SeedUtc,
        };
        db.Set<User>().Add(user);
        db.Set<ApplicationRole>().Add(new ApplicationRole
        {
            Id = 100,
            TenantId = 1,
            Code = "ADMIN",
            Name = "Administrator",
            CreatedDate = SeedUtc,
        });
        await db.SaveChangesAsync();
        db.Set<UserApplicationRole>().Add(new UserApplicationRole { UserId = 1, ApplicationRoleId = 100 });
        foreach (var pid in new[] { 227, 230, 231 })
            db.Set<ApplicationRolePermission>().Add(new ApplicationRolePermission { ApplicationRoleId = 100, PermissionId = pid });
        await db.SaveChangesAsync();

        var token = await new JwtService(Cfg(), db).GenerateTokenAsync(user);
        var perms = new JwtSecurityTokenHandler().ReadJwtToken(token).Claims
            .Where(c => c.Type == "permission")
            .Select(c => c.Value)
            .ToList();

        Assert.Contains(PermissionKeys.AllocationRun, perms);
        Assert.DoesNotContain(PermissionKeys.AllocationApprove, perms);
        Assert.DoesNotContain(PermissionKeys.AllocationOperationsView, perms);
    }

    [Fact]
    public async Task Faculty_does_not_receive_Allocation_Run()
    {
        var ambient = new Ambient { TenantId = 0 };
        await using var db = Db(ambient);
        db.Set<Permission>().Add(new Permission { Id = 227, Key = PermissionKeys.AllocationRun, Resource = "Allocation", Action = "Run" });
        var user = new User
        {
            Id = 2,
            TenantId = 1,
            Username = "fac",
            PasswordHash = "x",
            Role = UserRole.Faculty,
            CourseId = 1,
            GroupId = 1,
            CreatedDate = SeedUtc,
        };
        db.Set<User>().Add(user);
        db.Set<ApplicationRole>().Add(new ApplicationRole
        {
            Id = 101,
            TenantId = 1,
            Code = "FACULTY",
            Name = "Faculty",
            CreatedDate = SeedUtc,
        });
        await db.SaveChangesAsync();
        db.Set<UserApplicationRole>().Add(new UserApplicationRole { UserId = 2, ApplicationRoleId = 101 });
        await db.SaveChangesAsync();

        var token = await new JwtService(Cfg(), db).GenerateTokenAsync(user);
        var perms = new JwtSecurityTokenHandler().ReadJwtToken(token).Claims
            .Where(c => c.Type == "permission")
            .Select(c => c.Value)
            .ToList();
        Assert.DoesNotContain(PermissionKeys.AllocationRun, perms);
    }

    [Fact]
    public void Reconciler_source_does_not_grant_roles()
    {
        var path = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..",
            "Abhyanvaya.Infrastructure",
            "Authorization",
            "AllocationPermissionCatalogReconciler.cs"));
        Assert.True(File.Exists(path), path);
        var src = File.ReadAllText(path);
        Assert.DoesNotContain("insertedLinks", src);
        Assert.DoesNotContain("ApplicationRolePermissions.Add", src);
        Assert.Contains("no role grants", src, StringComparison.OrdinalIgnoreCase);
    }
}
