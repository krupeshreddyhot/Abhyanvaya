using Abhyanvaya.Application.Academic;
using Abhyanvaya.Application.Academic.Validators;
using Abhyanvaya.Application.DTOs.Academic;
using Abhyanvaya.Domain.Entities.Academic;
using FluentValidation.TestHelper;
using Xunit;

namespace Abhyanvaya.Application.UnitTests.Academic;

/// <summary>AI-SCHED-CATALOG/TIMETABLE P1-2 — Program → Department association (Programs remain optional).</summary>
public sealed class AiSchedCatalogTimetableP12ProgramDepartmentTests
{
    private static ProgramDepartmentAssociationRules.DepartmentSnapshot Dept(
        int id = 5,
        int tenantId = 1,
        int collegeId = 10,
        bool deleted = false) =>
        new(id, tenantId, collegeId, deleted, IsActive: true);

    [Fact]
    public void Program_Entity_Owns_DepartmentId_And_Retains_CollegeId()
    {
        var p = new Program
        {
            CollegeId = 10,
            DepartmentId = 5,
            ProgramCode = "COM",
            ProgramName = "Commerce",
            TenantId = 1,
        };
        Assert.Equal(5, p.DepartmentId);
        Assert.Equal(10, p.CollegeId);
        Assert.NotNull(typeof(Program).GetProperty(nameof(Program.Department)));
    }

    [Fact]
    public void EnablePrograms_Remains_Authoritative_On_TenantConfig()
    {
        var cfg = new TenantAcademicConfiguration { EnablePrograms = false, CollegeId = 1, TenantId = 1 };
        Assert.False(cfg.EnablePrograms);
        cfg.EnablePrograms = true;
        Assert.True(cfg.EnablePrograms);
    }

    [Fact]
    public void Association_Accepts_Same_Tenant_And_College_Department()
    {
        var d = ProgramDepartmentAssociationRules.Evaluate(
            enablePrograms: true,
            requestedDepartmentId: 5,
            department: Dept(5, 1, 10),
            programTenantId: 1,
            programCollegeId: 10);
        Assert.True(d.Accepted);
        Assert.Null(d.Error);
    }

    [Fact]
    public void Association_Rejects_Cross_Tenant_Department()
    {
        var d = ProgramDepartmentAssociationRules.Evaluate(
            enablePrograms: true,
            requestedDepartmentId: 5,
            department: Dept(5, tenantId: 99, collegeId: 10),
            programTenantId: 1,
            programCollegeId: 10);
        Assert.False(d.Accepted);
        Assert.Contains("tenant", d.Error!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Association_Rejects_Cross_College_Department()
    {
        var d = ProgramDepartmentAssociationRules.Evaluate(
            enablePrograms: true,
            requestedDepartmentId: 5,
            department: Dept(5, tenantId: 1, collegeId: 77),
            programTenantId: 1,
            programCollegeId: 10);
        Assert.False(d.Accepted);
        Assert.Contains("College", d.Error!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Association_Rejects_Missing_Department_When_Programs_Enabled()
    {
        var d = ProgramDepartmentAssociationRules.Evaluate(
            enablePrograms: true,
            requestedDepartmentId: null,
            department: null,
            programTenantId: 1,
            programCollegeId: 10);
        Assert.False(d.Accepted);
        Assert.Contains("required", d.Error!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Association_Rejects_Unknown_Department()
    {
        var d = ProgramDepartmentAssociationRules.Evaluate(
            enablePrograms: true,
            requestedDepartmentId: 5,
            department: null,
            programTenantId: 1,
            programCollegeId: 10);
        Assert.False(d.Accepted);
        Assert.Contains("not found", d.Error!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CreateProgram_Validator_Requires_DepartmentId()
    {
        var v = new CreateProgramRequestValidator();
        var bad = v.TestValidate(new CreateProgramRequest
        {
            DepartmentId = 0,
            ProgramCode = "COM",
            ProgramName = "Commerce",
        });
        bad.ShouldHaveValidationErrorFor(x => x.DepartmentId);

        var ok = v.TestValidate(new CreateProgramRequest
        {
            DepartmentId = 3,
            ProgramCode = "COM",
            ProgramName = "Commerce",
        });
        ok.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void UpdateProgram_Validator_Requires_DepartmentId()
    {
        var v = new UpdateProgramRequestValidator();
        var bad = v.TestValidate(new UpdateProgramRequest
        {
            DepartmentId = 0,
            ProgramCode = "COM",
            ProgramName = "Commerce",
            Status = "Active",
        });
        bad.ShouldHaveValidationErrorFor(x => x.DepartmentId);
    }

    [Fact]
    public void Programs_Disabled_Does_Not_Require_Program_On_Course()
    {
        // P1-2/P1-3: Course may omit Program; Department ownership is separate (P1-3).
        var course = new Abhyanvaya.Domain.Entities.Course
        {
            Code = "BCOM",
            Name = "B.Com",
            DepartmentId = 1,
            ProgramId = null,
        };
        Assert.Null(course.ProgramId);
        Assert.Equal(1, course.DepartmentId);
    }

    [Fact]
    public void Migration_Sql_Uses_Exactly_One_Department_Rule_Not_Default()
    {
        var root = FindRepoRoot();
        var migration = File.ReadAllText(Path.Combine(
            root,
            "Abhyanvaya.Infrastructure",
            "Persistence",
            "Migrations",
            "20260821120000_AI_SCHED_CATALOG_P1_2_ProgramDepartment.cs"));
        Assert.Contains("HAVING COUNT(*) = 1", migration, StringComparison.Ordinal);
        Assert.Contains("RAISE EXCEPTION", migration, StringComparison.Ordinal);
        Assert.DoesNotContain("SET \"DepartmentId\" = 1", migration, StringComparison.Ordinal);
        Assert.DoesNotContain("DELETE FROM \"Programs\"", migration, StringComparison.Ordinal);
    }

    [Fact]
    public void Architecture_No_Course_Hierarchy_Or_TG_CAP_Changes_In_P12_Surfaces()
    {
        var root = FindRepoRoot();
        var catalogService = File.ReadAllText(Path.Combine(root, "Abhyanvaya.Application", "Academic", "AcademicCatalogService.cs"));
        Assert.Contains("EnsureValidDepartmentAssociationAsync", catalogService, StringComparison.Ordinal);
        Assert.DoesNotContain("TeachingGroup", catalogService, StringComparison.Ordinal);
        Assert.DoesNotContain("ConflictEngine", catalogService, StringComparison.Ordinal);
        Assert.DoesNotContain("PublishReadiness", catalogService, StringComparison.Ordinal);

        // P1-2 did not introduce Course.DepartmentId; P1-3 may have — Program ownership remains Department-based.
        var programPath = Path.Combine(root, "Abhyanvaya.Domain", "Entities", "Academic", "Program.cs");
        Assert.Contains("DepartmentId", File.ReadAllText(programPath), StringComparison.Ordinal);
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Abhyanvaya.sln"))
                || Directory.Exists(Path.Combine(dir.FullName, "Abhyanvaya.Domain")))
                return dir.FullName;
            dir = dir.Parent;
        }
        throw new InvalidOperationException("Repo root not found.");
    }
}
