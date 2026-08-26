using System.Security.Claims;
using Abhyanvaya.Application.Academic;
using Abhyanvaya.Application.Academic.Validators;
using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.DTOs.Academic;
using Abhyanvaya.Application.DTOs.Course;
using Abhyanvaya.Application.UnitTests.Scheduling.Phase2;
using Abhyanvaya.Domain.Authorization;
using Abhyanvaya.Domain.Common;
using Abhyanvaya.Domain.Entities;
using Abhyanvaya.Domain.Entities.Academic;
using Abhyanvaya.Domain.Enums;
using Abhyanvaya.Domain.Events;
using FluentValidation;
using Moq;

namespace Abhyanvaya.Application.UnitTests.Academic;

/// <summary>
/// AI29.1D.24 Prompt 4B.5 — cross-tenant isolation + existing CanAssignCourseToProgram policy (no new permission names).
/// Cross-tenant assign → ValidationException ("Invalid Program.") → API maps to 400.
/// Missing assign permission → Forbid (403) at CourseController before mutation.
/// </summary>
public sealed class AI29_1D_24_Prompt4B5_TenantAuthorizationTests
{
    private const int TenantA = 1;
    private const int TenantB = 2;

    /// <summary>
    /// Mirrors <c>AuthorizationPolicies.CanAssignCourseToProgram</c> assertion in Program.cs
    /// (Program.Manage OR Setup.Courses.Manage; SuperAdmin; TenantId &gt; 0).
    /// </summary>
    private static bool EvaluateCanAssignCourseToProgram(ClaimsPrincipal user)
    {
        if (user.Identity?.IsAuthenticated != true)
            return false;

        var role = user.FindFirst(ClaimTypes.Role)?.Value;
        if (string.Equals(role, nameof(UserRole.SuperAdmin), StringComparison.OrdinalIgnoreCase))
            return true;

        if (!int.TryParse(user.FindFirst("TenantId")?.Value, out var tid) || tid <= 0)
            return false;

        return user.HasClaim("permission", PermissionKeys.ProgramManage)
               || user.HasClaim("permission", PermissionKeys.SetupCoursesManage);
    }

    private static ClaimsPrincipal Principal(
        string? role,
        int? tenantId,
        params string[] permissions)
    {
        var claims = new List<Claim>();
        if (role is not null)
            claims.Add(new Claim(ClaimTypes.Role, role));
        if (tenantId is not null)
            claims.Add(new Claim("TenantId", tenantId.Value.ToString()));
        foreach (var p in permissions)
            claims.Add(new Claim("permission", p));

        var identity = new ClaimsIdentity(claims, authenticationType: "test");
        return new ClaimsPrincipal(identity);
    }

    private sealed class CrossTenantHarness
    {
        public List<Course> Courses { get; } = [];
        public List<Program> Programs { get; } = [];
        public Mock<IApplicationDbContext> Db { get; } = new();
        public Mock<ICurrentUserService> User { get; } = new();
        public Mock<IAcademicHierarchyCache> Hierarchy { get; } = new();
        public Mock<IAcademicStatisticsCache> Stats { get; } = new();
        public Mock<IDomainEventDispatcher> Dispatcher { get; } = new();
        public Mock<ICacheService> MasterCache { get; } = new();
        public List<IDomainEvent> Dispatched { get; } = [];
        public int SaveChangesCalls { get; private set; }
        public AcademicCatalogService Catalog { get; }
        public CourseMasterWriteService Write { get; }

        public CrossTenantHarness()
        {
            User.SetupGet(u => u.TenantId).Returns(TenantA);
            User.SetupGet(u => u.UserId).Returns(11);

            // Tenant A course (linked to nothing).
            Courses.Add(new Course
            {
                Id = 100,
                TenantId = TenantA,
                Code = "CA",
                Name = "Course A",
                ProgramId = null,
            });

            // Actual Program belonging to Tenant B (not null stub).
            Programs.Add(new Program
            {
                Id = 200,
                TenantId = TenantB,
                ProgramCode = "PB",
                ProgramName = "Program B",
                IsActive = true,
                Status = "Active",
            });

            var configs = new List<TenantAcademicConfiguration>
            {
                new() { Id = 1, TenantId = TenantA, EnablePrograms = true, CollegeId = 1 },
            };

            Db.Setup(d => d.Courses).Returns(Courses.AsAsyncQueryable());
            Db.Setup(d => d.Programs).Returns(Programs.AsAsyncQueryable());
            Db.Setup(d => d.TenantAcademicConfigurations).Returns(configs.AsAsyncQueryable());
            Db.Setup(d => d.SaveChangesAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(() =>
                {
                    SaveChangesCalls++;
                    return 1;
                });
            Db.Setup(d => d.ExecuteInTransactionAsync(It.IsAny<Func<CancellationToken, Task>>(), It.IsAny<CancellationToken>()))
                .Returns<Func<CancellationToken, Task>, CancellationToken>(async (action, ct) =>
                {
                    var snap = Courses.Select(c => new Course
                    {
                        Id = c.Id,
                        TenantId = c.TenantId,
                        Code = c.Code,
                        Name = c.Name,
                        ProgramId = c.ProgramId,
                    }).ToList();
                    try
                    {
                        await action(ct);
                    }
                    catch
                    {
                        Courses.Clear();
                        Courses.AddRange(snap);
                        Db.Setup(d => d.Courses).Returns(Courses.AsAsyncQueryable());
                        throw;
                    }
                });

            Dispatcher
                .Setup(d => d.DispatchAsync(It.IsAny<IReadOnlyCollection<IDomainEvent>>(), It.IsAny<CancellationToken>()))
                .Returns<IReadOnlyCollection<IDomainEvent>, CancellationToken>((events, _) =>
                {
                    Dispatched.AddRange(events);
                    return Task.CompletedTask;
                });
            Hierarchy.Setup(c => c.InvalidateHierarchyAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            Stats.Setup(c => c.InvalidateAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            MasterCache.Setup(c => c.RemoveAsync(It.IsAny<string>())).Returns(Task.CompletedTask);

            Catalog = new AcademicCatalogService(
                Db.Object,
                User.Object,
                Hierarchy.Object,
                Stats.Object,
                Dispatcher.Object,
                new CreateProgramRequestValidator(),
                new UpdateProgramRequestValidator(),
                new AssignCourseProgramRequestValidator(),
                new UpsertProgramPolicyRequestValidator());

            var structure = new Mock<IAcademicStructureService>();
            structure
                .Setup(s => s.AssignCourseToProgramAsync(It.IsAny<AssignCourseProgramRequest>(), It.IsAny<CancellationToken>()))
                .Returns<AssignCourseProgramRequest, CancellationToken>((req, ct) => Catalog.AssignCourseToProgramAsync(req, ct));

            Write = new CourseMasterWriteService(Db.Object, MasterCache.Object, User.Object, structure.Object);
        }
    }

    [Fact]
    public async Task CrossTenant_CourseA_To_ProgramB_Rejected_No_Mutation_Event_Or_Cache()
    {
        var h = new CrossTenantHarness();
        var courseBefore = h.Courses.Single();
        Assert.Equal(TenantA, courseBefore.TenantId);
        Assert.Equal(TenantB, h.Programs.Single().TenantId);
        var savesBefore = h.SaveChangesCalls;

        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            h.Catalog.AssignCourseToProgramAsync(new AssignCourseProgramRequest
            {
                CourseId = 100,
                ProgramId = 200, // Program B
            }));

        Assert.Contains("Invalid Program", ex.Message, StringComparison.OrdinalIgnoreCase);

        // API convention: CourseController / ProgramsController map ValidationException → 400 BadRequest.
        Assert.Null(h.Courses.Single().ProgramId);
        Assert.Equal("CA", h.Courses.Single().Code);
        Assert.Equal(savesBefore, h.SaveChangesCalls);
        Assert.Empty(h.Dispatched);
        Assert.Empty(h.Dispatched.OfType<CourseAssigned>());
        Assert.Empty(h.Dispatched.OfType<CourseRemoved>());
        h.Dispatcher.Verify(
            d => d.DispatchAsync(It.IsAny<IReadOnlyCollection<IDomainEvent>>(), It.IsAny<CancellationToken>()),
            Times.Never);
        h.Hierarchy.Verify(c => c.InvalidateHierarchyAsync(It.IsAny<CancellationToken>()), Times.Never);
        h.Stats.Verify(c => c.InvalidateAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CrossTenant_Update_CourseMaster_Rolls_Back_Code_Name_And_Program()
    {
        var h = new CrossTenantHarness();
        h.Courses.Single().ProgramId = null;
        h.Courses.Single().Code = "CA";
        h.Courses.Single().Name = "Course A";

        var req = new UpdateCourseRequest { Id = 100, Code = "CA2", Name = "Course A Renamed" };
        req.SetProgramId(200);

        await Assert.ThrowsAsync<ValidationException>(() => h.Write.UpdateAsync(req));

        var course = h.Courses.Single();
        Assert.Equal("CA", course.Code);
        Assert.Equal("Course A", course.Name);
        Assert.Null(course.ProgramId);
        Assert.Empty(h.Dispatched);
        h.Hierarchy.Verify(c => c.InvalidateHierarchyAsync(It.IsAny<CancellationToken>()), Times.Never);
        h.Stats.Verify(c => c.InvalidateAsync(It.IsAny<CancellationToken>()), Times.Never);
        h.MasterCache.Verify(c => c.RemoveAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void Policy_ProgramManage_Allows_Assign()
    {
        var user = Principal(nameof(UserRole.Admin), TenantA, PermissionKeys.ProgramManage);
        Assert.True(EvaluateCanAssignCourseToProgram(user));
    }

    [Fact]
    public void Policy_SetupCoursesManage_Allows_Assign()
    {
        var user = Principal(nameof(UserRole.Admin), TenantA, PermissionKeys.SetupCoursesManage);
        Assert.True(EvaluateCanAssignCourseToProgram(user));
    }

    [Fact]
    public void Policy_Either_Permission_Is_Sufficient()
    {
        Assert.True(EvaluateCanAssignCourseToProgram(
            Principal(nameof(UserRole.Admin), TenantA, PermissionKeys.ProgramManage, PermissionKeys.SetupCoursesManage)));
    }

    [Fact]
    public void Policy_Unauthorized_Faculty_Without_Manage_Permissions_Denied()
    {
        // Typical faculty: Attendance.Manage only — cannot assign Course→Program.
        var faculty = Principal(
            nameof(UserRole.Faculty),
            TenantA,
            PermissionKeys.AttendanceManage,
            PermissionKeys.AttendanceView);
        Assert.False(EvaluateCanAssignCourseToProgram(faculty));
    }

    [Fact]
    public void Policy_Unauthorized_Admin_With_Only_ProgramView_Denied()
    {
        var adminViewOnly = Principal(nameof(UserRole.Admin), TenantA, PermissionKeys.ProgramView);
        Assert.False(EvaluateCanAssignCourseToProgram(adminViewOnly));
    }

    [Fact]
    public void Policy_Unauthenticated_Denied()
    {
        var anon = new ClaimsPrincipal(new ClaimsIdentity()); // not authenticated
        Assert.False(EvaluateCanAssignCourseToProgram(anon));
    }

    [Fact]
    public void Policy_Missing_TenantId_Denied_Even_With_Permission_Claim()
    {
        var user = Principal(nameof(UserRole.Admin), tenantId: null, PermissionKeys.ProgramManage);
        Assert.False(EvaluateCanAssignCourseToProgram(user));
    }

    [Fact]
    public void Policy_SuperAdmin_Allowed_Without_Permission_Claim()
    {
        var sa = Principal(nameof(UserRole.SuperAdmin), tenantId: null);
        Assert.True(EvaluateCanAssignCourseToProgram(sa));
    }

    [Fact]
    public void Policy_No_New_Permission_Names_Introduced()
    {
        // Existing keys only — Prompt 4B.5 must not invent new permission strings.
        Assert.Equal("Program.Manage", PermissionKeys.ProgramManage);
        Assert.Equal("Setup.Courses.Manage", PermissionKeys.SetupCoursesManage);

        var programCs = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "Abhyanvaya.API", "Program.cs"));
        var source = File.ReadAllText(programCs);
        Assert.Contains("CanAssignCourseToProgram", source);
        Assert.Contains("PermissionKeys.ProgramManage", source);
        Assert.Contains("PermissionKeys.SetupCoursesManage", source);
        Assert.DoesNotContain("CourseProgram.Assign", source);
        Assert.DoesNotContain("Program.AssignCourse", source);
    }

    [Fact]
    public void CourseController_Forbids_Before_Write_When_Assign_Unauthorized()
    {
        var path = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..",
            "Abhyanvaya.API", "Controllers", "CourseController.cs"));
        var source = File.ReadAllText(path);
        Assert.Contains("CanAssignCourseToProgram", source);
        Assert.Contains("return Forbid()", source);
        Assert.Contains("EnsureProgramAssignAuthorizedAsync", source);
        // Forbid runs before write service — no mutation on 403 path.
        Assert.Contains("if (forbid is not null)", source);
    }

    [Fact]
    public void Api_Maps_ValidationException_To_BadRequest_400()
    {
        var courseCtrl = File.ReadAllText(Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..",
            "Abhyanvaya.API", "Controllers", "CourseController.cs")));
        var programsCtrl = File.ReadAllText(Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..",
            "Abhyanvaya.API", "Controllers", "ProgramsController.cs")));

        Assert.Contains("catch (ValidationException ex)", courseCtrl);
        Assert.Contains("return BadRequest(ex.Message)", courseCtrl);
        Assert.Contains("catch (ValidationException ex)", programsCtrl);
        Assert.Contains("return BadRequest(ex.Message)", programsCtrl);
    }
}
