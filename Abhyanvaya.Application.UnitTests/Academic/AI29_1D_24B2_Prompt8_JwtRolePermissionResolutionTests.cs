using System.IO;
using Xunit;

namespace Abhyanvaya.Application.UnitTests.Academic;

/// <summary>
/// AI29.1D.24B.2 Prompt 8 — JWT application-role permission resolution must not be
/// silently emptied by ambient tenant query filters during login.
/// </summary>
public sealed class AI29_1D_24B2_Prompt8_JwtRolePermissionResolutionTests
{
    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Abhyanvaya.sln"))
                || File.Exists(Path.Combine(dir.FullName, "Abhyanvaya.Infrastructure", "Services", "JwtService.cs")))
                return dir.FullName;
            dir = dir.Parent;
        }

        throw new InvalidOperationException("Could not locate repository root from test base directory.");
    }

    [Fact]
    public void JwtService_resolves_application_roles_with_IgnoreQueryFilters()
    {
        var path = Path.Combine(FindRepoRoot(), "Abhyanvaya.Infrastructure", "Services", "JwtService.cs");
        Assert.True(File.Exists(path), $"Missing JwtService.cs at {path}");
        var source = File.ReadAllText(path);

        Assert.Contains("IgnoreQueryFilters()", source);
        Assert.Contains("ApplicationRolePermissions", source);
        Assert.Contains("roleIds", source);
        Assert.Contains("role.TenantId == user.TenantId", source);
        Assert.Contains("UserApplicationRoles", source);
        Assert.DoesNotContain(
            "SelectMany(u => u.ApplicationRole.ApplicationRolePermissions",
            source);
    }
}
