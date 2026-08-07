using Abhyanvaya.Application.Academic.Observability;
using Abhyanvaya.Application.Academic.ReadModels;
using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.DTOs.Academic;
using Abhyanvaya.Domain.Entities;
using Abhyanvaya.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Abhyanvaya.Application.Academic;

public sealed class AcademicHierarchyService : IAcademicHierarchyService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IAcademicCatalogService _catalog;
    private readonly IAcademicTreeService _tree;
    private readonly IAcademicTelemetryService _telemetry;
    private readonly ILogger<AcademicHierarchyService> _logger;

    public AcademicHierarchyService(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IAcademicCatalogService catalog,
        IAcademicTreeService tree,
        IAcademicTelemetryService telemetry,
        ILogger<AcademicHierarchyService> logger)
    {
        _db = db;
        _currentUser = currentUser;
        _catalog = catalog;
        _tree = tree;
        _telemetry = telemetry;
        _logger = logger;
    }

    public Task<AcademicHierarchyDto> GetAcademicHierarchyAsync(
        bool includeInactive = false,
        bool includeSections = true,
        bool includeSubjects = true,
        CancellationToken cancellationToken = default)
        => _telemetry.TrackAsync(
            AcademicOperations.HierarchyBuild,
            "AcademicHierarchy.Build",
            async ct =>
            {
                var model = await _tree.BuildTreeAsync(includeInactive, includeSections, includeSubjects, ct);
                _logger.LogInformation(
                    "Academic hierarchy built EnablePrograms={EnablePrograms} Roots={Roots}",
                    model.EnablePrograms, model.Roots.Count);
                return new AcademicHierarchyDto
                {
                    EnablePrograms = model.EnablePrograms,
                    Roots = model.Roots.Select(MapNode).ToList(),
                };
            },
            cancellationToken);

    private static AcademicHierarchyNodeDto MapNode(AcademicHierarchyNode n) => new()
    {
        Kind = n.EntityType,
        Id = n.EntityId,
        Code = n.Code,
        Name = n.DisplayName,
        DisplayOrder = n.DisplayOrder,
        IsActive = n.IsActive,
        Children = n.Children.Select(MapNode).ToList(),
    };

    public async Task<AcademicHierarchyStatisticsDto> GetHierarchyStatisticsAsync(CancellationToken cancellationToken = default)
    {
        var cfg = await _catalog.GetConfigurationAsync(cancellationToken);
        var tenantId = _currentUser.TenantId;
        return new AcademicHierarchyStatisticsDto
        {
            EnablePrograms = cfg.EnablePrograms,
            ProgramCount = await _db.Programs.CountAsync(p => p.TenantId == tenantId, cancellationToken),
            CourseCount = await _db.Courses.CountAsync(c => c.TenantId == tenantId, cancellationToken),
            GroupCount = await _db.Groups.CountAsync(g => g.TenantId == tenantId, cancellationToken),
            SemesterCount = await _db.Semesters.CountAsync(s => s.TenantId == tenantId, cancellationToken),
            SectionCount = await _db.Sections.CountAsync(s => s.TenantId == tenantId, cancellationToken),
            SubjectCount = await _db.Subjects.CountAsync(s => s.TenantId == tenantId, cancellationToken),
        };
    }

    public Task<IReadOnlyList<ProgramStatisticsDto>> GetProgramStatisticsAsync(CancellationToken cancellationToken = default)
        => _telemetry.TrackAsync(
            AcademicOperations.ProgramStatistics,
            "ProgramStatistics.Load",
            async ct =>
            {
                var programs = await _catalog.GetProgramsAsync(includeInactive: true, ct);
                var result = new List<ProgramStatisticsDto>(programs.Count);
                foreach (var p in programs)
                {
                    var stats = await BuildProgramStatisticsAsync(p.Id, p.ProgramCode, p.ProgramName, p.Status, ct);
                    result.Add(stats);
                }
                return (IReadOnlyList<ProgramStatisticsDto>)result;
            },
            cancellationToken);

    public async Task<ProgramStatisticsDto?> GetProgramStatisticsAsync(int programId, CancellationToken cancellationToken = default)
    {
        var program = await _catalog.GetProgramAsync(programId, cancellationToken);
        if (program is null) return null;
        return await BuildProgramStatisticsAsync(program.Id, program.ProgramCode, program.ProgramName, program.Status, cancellationToken);
    }

    public Task<ProgramDto?> GetProgramSummaryAsync(int programId, CancellationToken cancellationToken = default)
        => _catalog.GetProgramAsync(programId, cancellationToken);

    public async Task<AcademicHierarchyDto> GetProgramHierarchyAsync(int programId, CancellationToken cancellationToken = default)
    {
        var full = await GetAcademicHierarchyAsync(includeInactive: true, includeSections: true, includeSubjects: true, cancellationToken);
        var root = full.Roots.FirstOrDefault(r => r.Kind == "Program" && r.Id == programId);
        if (root is null)
        {
            var program = await _catalog.GetProgramAsync(programId, cancellationToken);
            if (program is null) throw new KeyNotFoundException("Program not found.");
            return new AcademicHierarchyDto
            {
                EnablePrograms = true,
                Roots =
                [
                    new AcademicHierarchyNodeDto
                    {
                        Kind = "Program",
                        Id = program.Id,
                        Code = program.ProgramCode,
                        Name = program.ProgramName,
                        DisplayOrder = program.DisplayOrder,
                        IsActive = program.IsActive,
                        Children = [],
                    }
                ],
            };
        }

        return new AcademicHierarchyDto { EnablePrograms = true, Roots = [root] };
    }

    public async Task<IReadOnlyList<Course>> GetProgramCoursesAsync(int programId, CancellationToken cancellationToken = default)
    {
        await EnsureProgramExistsAsync(programId, cancellationToken);
        var courses = await _catalog.GetCoursesAsync(cancellationToken);
        return courses.Where(c => c.ProgramId == programId)
            .OrderBy(c => c.DisplayOrder).ThenBy(c => c.Name)
            .ToList();
    }

    public async Task<IReadOnlyList<Group>> GetProgramGroupsAsync(int programId, CancellationToken cancellationToken = default)
    {
        var courseIds = (await GetProgramCoursesAsync(programId, cancellationToken)).Select(c => c.Id).ToHashSet();
        var groups = await _catalog.GetGroupsAsync(cancellationToken);
        return groups.Where(g => courseIds.Contains(g.CourseId))
            .OrderBy(g => g.DisplayOrder).ThenBy(g => g.Name)
            .ToList();
    }

    public async Task<IReadOnlyList<Semester>> GetProgramSemestersAsync(int programId, CancellationToken cancellationToken = default)
    {
        var courseIds = (await GetProgramCoursesAsync(programId, cancellationToken)).Select(c => c.Id).ToHashSet();
        var semesters = await _catalog.GetSemestersAsync(cancellationToken);
        return semesters.Where(s => courseIds.Contains(s.CourseId))
            .OrderBy(s => s.DisplayOrder).ThenBy(s => s.Name)
            .ToList();
    }

    public async Task<IReadOnlyList<SectionDto>> GetProgramSectionsAsync(int programId, CancellationToken cancellationToken = default)
    {
        var courseIds = (await GetProgramCoursesAsync(programId, cancellationToken)).Select(c => c.Id).ToHashSet();
        var sections = await _catalog.GetSectionsAsync(cancellationToken);
        return sections.Where(s => courseIds.Contains(s.CourseId))
            .OrderBy(s => s.DisplayOrder).ThenBy(s => s.SectionName)
            .ToList();
    }

    public async Task<int> GetProgramStudentCountAsync(int programId, CancellationToken cancellationToken = default)
    {
        var courseIds = await CourseIdsAsync(programId, cancellationToken);
        if (courseIds.Count == 0) return 0;
        return await _db.Students.CountAsync(s => s.TenantId == _currentUser.TenantId && courseIds.Contains(s.CourseId), cancellationToken);
    }

    public async Task<int> GetProgramFacultyCountAsync(int programId, CancellationToken cancellationToken = default)
    {
        var courseIds = await CourseIdsAsync(programId, cancellationToken);
        if (courseIds.Count == 0) return 0;
        var subjectIds = await _db.Subjects.AsNoTracking()
            .Where(s => s.TenantId == _currentUser.TenantId && courseIds.Contains(s.CourseId))
            .Select(s => s.Id)
            .ToListAsync(cancellationToken);
        if (subjectIds.Count == 0) return 0;
        return await _db.StaffSubjectAssignments.AsNoTracking()
            .Where(a => a.TenantId == _currentUser.TenantId && subjectIds.Contains(a.SubjectId))
            .Select(a => a.StaffId)
            .Distinct()
            .CountAsync(cancellationToken);
    }

    public Task<int> GetProgramCourseCountAsync(int programId, CancellationToken cancellationToken = default)
        => _db.Courses.CountAsync(c => c.TenantId == _currentUser.TenantId && c.ProgramId == programId, cancellationToken);

    private async Task EnsureProgramExistsAsync(int programId, CancellationToken ct)
    {
        var exists = await _db.Programs.AsNoTracking()
            .AnyAsync(p => p.Id == programId && p.TenantId == _currentUser.TenantId, ct);
        if (!exists) throw new KeyNotFoundException("Program not found.");
    }

    private async Task<List<int>> CourseIdsAsync(int programId, CancellationToken ct)
        => await _db.Courses.AsNoTracking()
            .Where(c => c.TenantId == _currentUser.TenantId && c.ProgramId == programId)
            .Select(c => c.Id)
            .ToListAsync(ct);

    private async Task<ProgramStatisticsDto> BuildProgramStatisticsAsync(
        int programId,
        string code,
        string name,
        string status,
        CancellationToken ct)
    {
        var courseIds = await CourseIdsAsync(programId, ct);
        var tenantId = _currentUser.TenantId;

        var totalCourses = courseIds.Count;
        var totalGroups = courseIds.Count == 0
            ? 0
            : await _db.Groups.CountAsync(g => g.TenantId == tenantId && courseIds.Contains(g.CourseId), ct);
        var totalSemesters = courseIds.Count == 0
            ? 0
            : await _db.Semesters.CountAsync(s => s.TenantId == tenantId && courseIds.Contains(s.CourseId), ct);
        var totalSections = courseIds.Count == 0
            ? 0
            : await _db.Sections.CountAsync(s => s.TenantId == tenantId && courseIds.Contains(s.CourseId), ct);
        var totalSubjects = courseIds.Count == 0
            ? 0
            : await _db.Subjects.CountAsync(s => s.TenantId == tenantId && courseIds.Contains(s.CourseId), ct);
        var totalStudents = await GetProgramStudentCountAsync(programId, ct);
        var totalFaculty = await GetProgramFacultyCountAsync(programId, ct);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var runningClasses = courseIds.Count == 0
            ? 0
            : await _db.ClassSchedules.CountAsync(
                s => s.TenantId == tenantId && s.IsActive && s.ScheduleDate == today && courseIds.Contains(s.CourseId), ct);

        decimal attendancePercentage = 0m;
        if (courseIds.Count > 0)
        {
            var subjectIds = await _db.Subjects.AsNoTracking()
                .Where(s => s.TenantId == tenantId && courseIds.Contains(s.CourseId))
                .Select(s => s.Id)
                .ToListAsync(ct);
            if (subjectIds.Count > 0)
            {
                var from = DateTime.UtcNow.Date.AddDays(-30);
                var rows = await _db.Attendances.AsNoTracking()
                    .Where(a => a.TenantId == tenantId && subjectIds.Contains(a.SubjectId) && a.Date >= from)
                    .GroupBy(_ => 1)
                    .Select(g => new
                    {
                        Total = g.Count(),
                        Present = g.Count(x => x.Status == AttendanceStatus.Present),
                    })
                    .FirstOrDefaultAsync(ct);
                if (rows is { Total: > 0 })
                    attendancePercentage = Math.Round(100m * rows.Present / rows.Total, 2);
            }
        }

        decimal roomUtilization = 0m;
        if (courseIds.Count > 0)
        {
            var entryCount = await _db.SchedulingTimetableEntries.AsNoTracking()
                .CountAsync(e => e.TenantId == tenantId && courseIds.Contains(e.CourseId) && e.RoomId > 0, ct);
            var roomCount = await _db.SchedulingRooms.AsNoTracking()
                .CountAsync(r => r.TenantId == tenantId, ct);
            if (roomCount > 0)
            {
                // Read-only heuristic: entries per room capped at 100%.
                roomUtilization = Math.Min(100m, Math.Round(100m * entryCount / (roomCount * 30m), 2));
            }
        }

        return new ProgramStatisticsDto
        {
            ProgramId = programId,
            ProgramCode = code,
            ProgramName = name,
            Status = status,
            StudentCount = totalStudents,
            FacultyCount = totalFaculty,
            CourseCount = totalCourses,
            TotalGroups = totalGroups,
            TotalSemesters = totalSemesters,
            TotalSections = totalSections,
            TotalSubjects = totalSubjects,
            RunningClasses = runningClasses,
            AttendancePercentage = attendancePercentage,
            RoomUtilization = roomUtilization,
        };
    }
}
