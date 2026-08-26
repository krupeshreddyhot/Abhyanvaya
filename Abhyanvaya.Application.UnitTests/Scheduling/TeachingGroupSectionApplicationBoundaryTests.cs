using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.Scheduling;
using Abhyanvaya.Domain.Entities.Academic;
using Abhyanvaya.Domain.Entities.Scheduling;
using Abhyanvaya.Domain.Enums.Scheduling;
using Abhyanvaya.Domain.Exceptions;
using Abhyanvaya.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Abhyanvaya.Application.UnitTests.Scheduling;

/// <summary>AI-SCHED-TG.4A Prompt 3 — TeachingGroupSection application boundary.</summary>
public sealed class TeachingGroupSectionApplicationBoundaryTests
{
    private sealed class AmbientCurrentUser : ICurrentUserService
    {
        public int UserId { get; set; } = 1;
        public string Role { get; set; } = "Admin";
        public int TenantId { get; set; } = 1;
        public int StaffId { get; set; }
        public int CourseId { get; set; }
        public int GroupId { get; set; }
    }

    private static (ApplicationDbContext Db, TeachingGroupSectionApplicationService Service) CreateSut(int tenantId = 1)
    {
        var user = new AmbientCurrentUser { TenantId = tenantId };
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase("tg4a-p3-" + Guid.NewGuid().ToString("N"))
            .Options;
        var db = new ApplicationDbContext(options, user, NullLogger<ApplicationDbContext>.Instance);
        return (db, new TeachingGroupSectionApplicationService(db, db, user, new TimetableSectionProjector(db, user)));
    }

    private static async Task<(TeachingGroup Tg, Section SecA, Section SecB, Section SecWrong)> SeedAsync(
        ApplicationDbContext db,
        int tenantId = 1,
        TeachingGroupType type = TeachingGroupType.Custom,
        TeachingGroupStatus status = TeachingGroupStatus.Active)
    {
        var tg = new TeachingGroup
        {
            TenantId = tenantId,
            AcademicYearId = 1,
            CourseId = 1,
            GroupId = 2,
            SemesterId = 3,
            SubjectId = 17,
            SubjectAllocationId = 10,
            Type = type,
            MembershipSource = TeachingGroupMembershipSource.ExplicitStudents,
            Status = status,
            ActivityKind = TeachingGroupActivityKind.Lecture,
            Code = "TG1",
            Name = "TG One",
            EffectiveFrom = DateOnly.FromDateTime(DateTime.UtcNow),
            CreatedDate = DateTime.UtcNow,
        };
        db.Set<TeachingGroup>().Add(tg);

        var secA = NewSection(tenantId, 1, "A");
        var secB = NewSection(tenantId, 1, "B");
        var secWrong = NewSection(tenantId, 1, "X", courseId: 99, groupId: 99, semesterId: 99);
        db.Set<Section>().AddRange(secA, secB, secWrong);
        await db.SaveChangesAsync();
        return (tg, secA, secB, secWrong);
    }

    private static Section NewSection(
        int tenantId,
        int academicYearId,
        string code,
        int courseId = 1,
        int groupId = 2,
        int semesterId = 3) => new()
    {
        TenantId = tenantId,
        CollegeId = 1,
        AcademicYearId = academicYearId,
        CourseId = courseId,
        GroupId = groupId,
        SemesterId = semesterId,
        SectionCode = code,
        SectionName = "Section " + code,
        Status = "Active",
        CreatedDate = DateTime.UtcNow,
    };

    [Fact]
    public async Task Replace_valid_sections_succeeds()
    {
        var (db, service) = CreateSut();
        var (tg, secA, secB, _) = await SeedAsync(db);

        var result = await service.ReplaceSectionsAsync(tg.Id, [secA.Id, secB.Id]);
        Assert.Equal(2, result.Count);
        Assert.Contains(result, x => x.SectionId == secA.Id);
        Assert.Contains(result, x => x.SectionId == secB.Id);
        Assert.Equal(2, await db.Set<TeachingGroupSection>().CountAsync());
    }

    [Fact]
    public async Task Cross_tenant_TeachingGroup_is_not_found()
    {
        var (db, service) = CreateSut(tenantId: 1);
        var (tg, secA, _, _) = await SeedAsync(db, tenantId: 2);
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.ReplaceSectionsAsync(tg.Id, [secA.Id]));
    }

    [Fact]
    public async Task Wrong_academic_scope_section_is_rejected()
    {
        var (db, service) = CreateSut();
        var (tg, _, _, wrong) = await SeedAsync(db);
        var ex = await Assert.ThrowsAsync<DomainException>(() => service.ReplaceSectionsAsync(tg.Id, [wrong.Id]));
        Assert.Equal(TeachingGroupRules.TeachingGroupSectionIncompatibleMessage, ex.Message);
    }

    [Fact]
    public async Task Duplicate_add_is_rejected()
    {
        var (db, service) = CreateSut();
        var (tg, secA, _, _) = await SeedAsync(db);
        await service.AddSectionAsync(tg.Id, secA.Id);
        await Assert.ThrowsAsync<DomainException>(() => service.AddSectionAsync(tg.Id, secA.Id));
    }

    [Fact]
    public async Task Multiple_sections_supported_for_combined_type()
    {
        var (db, service) = CreateSut();
        var (tg, secA, secB, _) = await SeedAsync(db, type: TeachingGroupType.CombinedSections);
        var result = await service.ReplaceSectionsAsync(tg.Id, [secA.Id, secB.Id]);
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task Combined_type_rejects_single_section()
    {
        var (db, service) = CreateSut();
        var (tg, secA, _, _) = await SeedAsync(db, type: TeachingGroupType.CombinedSections);
        await Assert.ThrowsAsync<DomainException>(() => service.ReplaceSectionsAsync(tg.Id, [secA.Id]));
    }

    [Fact]
    public async Task Remove_one_of_multiple_sections_succeeds_for_custom()
    {
        var (db, service) = CreateSut();
        var (tg, secA, secB, _) = await SeedAsync(db);
        await service.ReplaceSectionsAsync(tg.Id, [secA.Id, secB.Id]);
        await service.RemoveSectionAsync(tg.Id, secA.Id);
        var remaining = await service.GetSectionsAsync(tg.Id);
        Assert.Single(remaining);
        Assert.Equal(secB.Id, remaining[0].SectionId);
    }

    [Fact]
    public async Task Clear_all_sections_allowed_for_custom_type()
    {
        var (db, service) = CreateSut();
        var (tg, secA, _, _) = await SeedAsync(db);
        await service.ReplaceSectionsAsync(tg.Id, [secA.Id]);
        var cleared = await service.ReplaceSectionsAsync(tg.Id, []);
        Assert.Empty(cleared);
    }

    [Fact]
    public async Task SectionDerived_rejects_clear_all()
    {
        var (db, service) = CreateSut();
        var (tg, secA, _, _) = await SeedAsync(db, type: TeachingGroupType.SectionDerived);
        await service.ReplaceSectionsAsync(tg.Id, [secA.Id]);
        await Assert.ThrowsAsync<DomainException>(() => service.ReplaceSectionsAsync(tg.Id, []));
    }

    [Fact]
    public async Task Archived_TeachingGroup_rejects_mutation()
    {
        var (db, service) = CreateSut();
        var (tg, secA, _, _) = await SeedAsync(db, status: TeachingGroupStatus.Archived);
        await Assert.ThrowsAsync<DomainException>(() => service.ReplaceSectionsAsync(tg.Id, [secA.Id]));
    }

    [Fact]
    public async Task Locked_TeachingGroup_rejects_section_mutation()
    {
        var (db, service) = CreateSut();
        var (tg, secA, _, _) = await SeedAsync(db, status: TeachingGroupStatus.Locked);
        await Assert.ThrowsAsync<DomainException>(() => service.AddSectionAsync(tg.Id, secA.Id));
    }

    [Fact]
    public async Task Missing_TeachingGroup_does_not_create_TeachingGroup()
    {
        var (db, service) = CreateSut();
        await SeedAsync(db);
        var before = await db.Set<TeachingGroup>().IgnoreQueryFilters().CountAsync();
        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.ReplaceSectionsAsync(99999, [1]));
        Assert.Equal(before, await db.Set<TeachingGroup>().IgnoreQueryFilters().CountAsync());
    }

    [Fact]
    public async Task Mutation_does_not_write_TimetableSection_or_StudentSection()
    {
        var (db, service) = CreateSut();
        var (tg, secA, secB, _) = await SeedAsync(db);
        await service.ReplaceSectionsAsync(tg.Id, [secA.Id, secB.Id]);
        Assert.Empty(await db.Set<TimetableSection>().IgnoreQueryFilters().ToListAsync());
        Assert.Empty(await db.Set<StudentSection>().IgnoreQueryFilters().ToListAsync());
    }

    [Fact]
    public void Service_has_no_SubjectAllocation_inference_or_query_filter_bypass()
    {
        var src = File.ReadAllText(Path.Combine(FindRepoRoot(),
            "Abhyanvaya.Application", "Scheduling", "TeachingGroupSectionApplicationService.cs"));
        Assert.DoesNotContain(".IgnoreQueryFilters", src);
        Assert.DoesNotContain("CreateTeachingGroup", src);
        Assert.DoesNotContain("FindTeachingGroup", src);
        Assert.DoesNotContain("new TimetableSection", src);
        Assert.DoesNotContain("new StudentSection", src);
        Assert.DoesNotContain("AttendanceSession", src);
    }

    [Fact]
    public void Authorization_remains_api_policy_boundary_for_bridge()
    {
        // Prompt 3 is application service only; Prompt 5 must keep CanManageSchedulingTimetable on PUT /sections.
        var controller = File.ReadAllText(Path.Combine(FindRepoRoot(),
            "Abhyanvaya.API", "Controllers", "SectionsController.cs"));
        Assert.Contains("CanManageSchedulingTimetable", controller);
        Assert.Contains("SetTimetableSectionsAsync", controller);
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Abhyanvaya.Infrastructure", "Abhyanvaya.Infrastructure.csproj")))
                return dir.FullName;
            dir = dir.Parent;
        }

        throw new InvalidOperationException("Repo root not found.");
    }
}
