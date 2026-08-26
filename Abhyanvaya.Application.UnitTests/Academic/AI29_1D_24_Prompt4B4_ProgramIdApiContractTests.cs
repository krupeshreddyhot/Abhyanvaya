using System.Text.Json;
using Abhyanvaya.Application.Academic;
using Abhyanvaya.Application.Academic.Validators;
using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.DTOs.Academic;
using Abhyanvaya.Application.DTOs.Course;
using Abhyanvaya.Application.UnitTests.Scheduling.Phase2;
using Abhyanvaya.Domain.Common;
using Abhyanvaya.Domain.Entities;
using Abhyanvaya.Domain.Entities.Academic;
using Abhyanvaya.Domain.Events;
using Moq;

namespace Abhyanvaya.Application.UnitTests.Academic;

/// <summary>
/// AI29.1D.24 Prompt 4B.4 — ProgramId API contract (assign / explicit null / omitted) + Programs-disabled legacy.
/// Deserializes JSON the same way as ASP.NET Core camelCase requests, then exercises CourseMasterWriteService.
/// </summary>
public sealed class AI29_1D_24_Prompt4B4_ProgramIdApiContractTests
{
    private const int TenantId = 1;

    private static readonly JsonSerializerOptions ApiJson = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private sealed class Harness
    {
        public List<Course> Courses { get; } = [];
        public List<Program> Programs { get; } = [];
        public List<Department> Departments { get; } = [];
        public List<TenantAcademicConfiguration> Configs { get; } = [];
        public Mock<IApplicationDbContext> Db { get; } = new();
        public Mock<ICurrentUserService> User { get; } = new();
        public Mock<IAcademicHierarchyCache> Hierarchy { get; } = new();
        public Mock<IAcademicStatisticsCache> Stats { get; } = new();
        public Mock<IDomainEventDispatcher> Dispatcher { get; } = new();
        public Mock<ICacheService> MasterCache { get; } = new();
        public Mock<IAcademicStructureService> Structure { get; } = new();
        public List<AssignCourseProgramRequest> AssignCalls { get; } = [];
        public AcademicCatalogService Catalog { get; }
        public CourseMasterWriteService Write { get; }

        public Harness(bool enablePrograms = true)
        {
            User.SetupGet(u => u.TenantId).Returns(TenantId);
            User.SetupGet(u => u.UserId).Returns(1);

            Configs.Add(new TenantAcademicConfiguration
            {
                Id = 1,
                TenantId = TenantId,
                EnablePrograms = enablePrograms,
                CollegeId = 1,
            });

            Programs.Add(new Program
            {
                Id = 15,
                TenantId = TenantId,
                CollegeId = 1,
                DepartmentId = 1,
                ProgramCode = "COM",
                ProgramName = "Commerce",
                IsActive = true,
                Status = "Active",
            });
            Programs.Add(new Program
            {
                Id = 20,
                TenantId = TenantId,
                CollegeId = 1,
                DepartmentId = 1,
                ProgramCode = "SCI",
                ProgramName = "Science",
                IsActive = true,
                Status = "Active",
            });

            Departments.Add(new Department
            {
                Id = 1,
                TenantId = TenantId,
                CollegeId = 1,
                Name = "Commerce",
                Code = "001",
                IsActive = true,
            });

            Refresh();

            Db.Setup(d => d.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
            Db.Setup(d => d.AddAsync(It.IsAny<Course>()))
                .Returns<Course>(c =>
                {
                    if (c.Id == 0)
                        c.Id = Courses.Count == 0 ? 1 : Courses.Max(x => x.Id) + 1;
                    c.TenantId = TenantId;
                    Courses.Add(c);
                    Refresh();
                    return Task.CompletedTask;
                });
            Db.Setup(d => d.ExecuteInTransactionAsync(It.IsAny<Func<CancellationToken, Task>>(), It.IsAny<CancellationToken>()))
                .Returns<Func<CancellationToken, Task>, CancellationToken>((action, ct) => action(ct));

            Dispatcher
                .Setup(d => d.DispatchAsync(It.IsAny<IReadOnlyCollection<IDomainEvent>>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
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

            Structure
                .Setup(s => s.AssignCourseToProgramAsync(It.IsAny<AssignCourseProgramRequest>(), It.IsAny<CancellationToken>()))
                .Returns<AssignCourseProgramRequest, CancellationToken>((req, ct) =>
                {
                    AssignCalls.Add(req);
                    return Catalog.AssignCourseToProgramAsync(req, ct);
                });

            Write = new CourseMasterWriteService(Db.Object, MasterCache.Object, User.Object, Structure.Object);
        }

        public void Refresh()
        {
            Db.Setup(d => d.Courses).Returns(Courses.AsAsyncQueryable());
            Db.Setup(d => d.Programs).Returns(Programs.AsAsyncQueryable());
            Db.Setup(d => d.Departments).Returns(Departments.AsAsyncQueryable());
            Db.Setup(d => d.TenantAcademicConfigurations).Returns(Configs.AsAsyncQueryable());
        }
    }

    [Fact]
    public void Json_programId_15_Sets_Specified_And_Value()
    {
        var req = JsonSerializer.Deserialize<UpdateCourseRequest>(
            """{"id":1,"code":"BCOM","name":"B.Com", "departmentId": 1,"programId":15}""", ApiJson)!;
        Assert.True(req.ProgramIdSpecified);
        Assert.Equal(15, req.ProgramId);
    }

    [Fact]
    public void Json_programId_null_Sets_Specified_With_Null()
    {
        var req = JsonSerializer.Deserialize<UpdateCourseRequest>(
            """{"id":1,"code":"BCOM","name":"B.Com", "departmentId": 1,"programId":null}""", ApiJson)!;
        Assert.True(req.ProgramIdSpecified);
        Assert.Null(req.ProgramId);
    }

    [Fact]
    public void Json_programId_Omitted_Does_Not_Set_Specified()
    {
        var req = JsonSerializer.Deserialize<UpdateCourseRequest>(
            """{"id":1,"code":"BCOM","name":"B.Com", "departmentId": 1}""", ApiJson)!;
        Assert.False(req.ProgramIdSpecified);
        Assert.Null(req.ProgramId);
    }

    [Fact]
    public async Task Update_programId_15_Assigns_Program_15()
    {
        var h = new Harness();
        h.Courses.Add(new Course { Id = 1, TenantId = TenantId, Code = "BCOM", Name = "B.Com", DepartmentId = 1, ProgramId = null });
        h.Refresh();

        var req = JsonSerializer.Deserialize<UpdateCourseRequest>(
            """{"id":1,"code":"BCOM","name":"B.Com", "departmentId": 1,"programId":15}""", ApiJson)!;
        var row = await h.Write.UpdateAsync(req);

        Assert.Equal(15, row.ProgramId);
        Assert.Single(h.AssignCalls);
        Assert.Equal(15, h.AssignCalls[0].ProgramId);
    }

    [Fact]
    public async Task Update_programId_null_Explicitly_Removes_Program()
    {
        var h = new Harness();
        h.Courses.Add(new Course { Id = 1, TenantId = TenantId, Code = "BCOM", Name = "B.Com", DepartmentId = 1, ProgramId = 15 });
        h.Refresh();

        var req = JsonSerializer.Deserialize<UpdateCourseRequest>(
            """{"id":1,"code":"BCOM","name":"B.Com", "departmentId": 1,"programId":null}""", ApiJson)!;
        var row = await h.Write.UpdateAsync(req);

        Assert.Null(row.ProgramId);
        Assert.Single(h.AssignCalls);
        Assert.Null(h.AssignCalls[0].ProgramId);
    }

    [Fact]
    public async Task Update_programId_Omitted_Does_Not_Modify_Existing_Program()
    {
        var h = new Harness();
        h.Courses.Add(new Course { Id = 1, TenantId = TenantId, Code = "BCOM", Name = "B.Com", DepartmentId = 1, ProgramId = 15 });
        h.Refresh();

        var req = JsonSerializer.Deserialize<UpdateCourseRequest>(
            """{"id":1,"code":"BCOM2","name":"Bachelor of Commerce", "departmentId": 1}""", ApiJson)!;
        Assert.False(req.ProgramIdSpecified);

        var row = await h.Write.UpdateAsync(req);

        Assert.Equal("BCOM2", row.Code);
        Assert.Equal(15, row.ProgramId);
        Assert.Empty(h.AssignCalls);
        StructureNeverCalledWhenOmitted(h);
    }

    private static void StructureNeverCalledWhenOmitted(Harness h)
        => h.Structure.Verify(
            s => s.AssignCourseToProgramAsync(It.IsAny<AssignCourseProgramRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);

    [Fact]
    public async Task Create_programId_15_Assigns_Program_15()
    {
        var h = new Harness();
        var req = JsonSerializer.Deserialize<CreateCourseRequest>(
            """{"code":"BCOM","name":"B.Com","departmentId":1,"programId":15}""", ApiJson)!;

        var row = await h.Write.CreateAsync(req);

        Assert.Equal(15, row.ProgramId);
        Assert.Single(h.AssignCalls);
        Assert.Equal(15, h.AssignCalls[0].ProgramId);
    }

    [Fact]
    public async Task Create_programId_Omitted_Leaves_Unassigned()
    {
        var h = new Harness();
        var req = JsonSerializer.Deserialize<CreateCourseRequest>(
            """{"code":"BCOM","name":"B.Com","departmentId":1}""", ApiJson)!;

        var row = await h.Write.CreateAsync(req);

        Assert.Null(row.ProgramId);
        // Assign may be called with null (no-op) when Programs enabled — relationship stays unassigned.
        Assert.True(h.AssignCalls.Count is 0 or 1);
        if (h.AssignCalls.Count == 1)
            Assert.Null(h.AssignCalls[0].ProgramId);
    }

    [Fact]
    public async Task Programs_Disabled_Legacy_Update_Ignores_programId_And_Does_Not_Assign()
    {
        var h = new Harness(enablePrograms: false);
        h.Courses.Add(new Course { Id = 1, TenantId = TenantId, Code = "BCOM", Name = "B.Com", DepartmentId = 1, ProgramId = null });
        h.Refresh();

        var req = JsonSerializer.Deserialize<UpdateCourseRequest>(
            """{"id":1,"code":"BCOM2","name":"B.Com", "departmentId": 1,"programId":15}""", ApiJson)!;
        var row = await h.Write.UpdateAsync(req);

        Assert.Equal("BCOM2", row.Code);
        Assert.Null(row.ProgramId);
        Assert.Empty(h.AssignCalls);
    }

    [Fact]
    public async Task Programs_Disabled_Legacy_Create_Does_Not_Assign()
    {
        var h = new Harness(enablePrograms: false);
        var req = JsonSerializer.Deserialize<CreateCourseRequest>(
            """{"code":"BCOM","name":"B.Com","departmentId":1,"programId":15}""", ApiJson)!;

        var row = await h.Write.CreateAsync(req);

        Assert.Null(row.ProgramId);
        Assert.Empty(h.AssignCalls);
    }

    [Fact]
    public void Response_Contract_Includes_ProgramId()
    {
        var row = new CourseMasterRowDto(1, "BCOM", "B.Com", 1, 15);
        var json = JsonSerializer.Serialize(row, ApiJson);
        using var doc = JsonDocument.Parse(json);
        Assert.Equal(15, doc.RootElement.GetProperty("programId").GetInt32());
        Assert.Equal("BCOM", doc.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public void No_Second_Assignment_Endpoint_In_CourseController()
    {
        var path = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..",
            "Abhyanvaya.API", "Controllers", "CourseController.cs"));
        var source = File.ReadAllText(path);
        Assert.DoesNotContain("[HttpPost(\"assign-course\")]", source);
        Assert.DoesNotContain("[Route(\"api/programs", source);
        Assert.Contains("ICourseMasterWriteService", source);
        Assert.Contains("[Route(\"api/course\")]", source);
    }
}
