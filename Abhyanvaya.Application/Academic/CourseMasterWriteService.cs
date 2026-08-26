using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.DTOs.Academic;
using Abhyanvaya.Application.DTOs.Course;
using Abhyanvaya.Domain.Entities;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Abhyanvaya.Application.Academic;

/// <summary>
/// Persists Course Code/Name/DepartmentId and orchestrates <see cref="IAcademicStructureService.AssignCourseToProgramAsync"/>
/// inside the existing <see cref="IUnitOfWork.ExecuteInTransactionAsync"/> boundary.
/// </summary>
public sealed class CourseMasterWriteService : ICourseMasterWriteService
{
    private readonly IApplicationDbContext _db;
    private readonly ICacheService _cache;
    private readonly ICurrentUserService _currentUser;
    private readonly IAcademicStructureService _structure;

    public CourseMasterWriteService(
        IApplicationDbContext db,
        ICacheService cache,
        ICurrentUserService currentUser,
        IAcademicStructureService structure)
    {
        _db = db;
        _cache = cache;
        _currentUser = currentUser;
        _structure = structure;
    }

    private static string CoursesCacheKey(int tenantId) => $"tenant:{tenantId}:master:courses";

    public async Task<CourseMasterRowDto> CreateAsync(CreateCourseRequest request, CancellationToken cancellationToken = default)
    {
        var code = NormalizeCode(request.Code);
        var name = NormalizeName(request.Name);
        EnsureCodeName(code, name);

        var exists = await _db.Courses.AnyAsync(x =>
            x.TenantId == _currentUser.TenantId &&
            (x.Name.ToLower() == name.ToLower() || x.Code.ToLower() == code.ToLower()), cancellationToken);
        if (exists)
            throw new ValidationException("Course code or name already exists.");

        var programsEnabled = await ProgramsEnabledAsync(cancellationToken);
        var requestedProgramId = programsEnabled && request.ProgramIdSpecified
            ? CourseProgramAssignmentRules.NormalizeProgramId(request.ProgramId)
            : null;

        await EnsureValidDepartmentOwnershipAsync(
            request.DepartmentId,
            requestedProgramId,
            programsEnabled,
            cancellationToken);

        var courseId = 0;

        await _db.ExecuteInTransactionAsync(async ct =>
        {
            var course = new Course
            {
                Code = code,
                Name = name,
                DepartmentId = request.DepartmentId,
                ProgramId = null,
                CreatedDate = DateTime.UtcNow,
            };

            await _db.AddAsync(course);
            await _db.SaveChangesAsync(ct);
            courseId = course.Id;

            if (programsEnabled)
            {
                // Create: omitted or null ⇒ unassigned; value ⇒ assign (authoritative command).
                var requested = request.ProgramIdSpecified ? request.ProgramId : null;
                await _structure.AssignCourseToProgramAsync(
                    new AssignCourseProgramRequest
                    {
                        CourseId = course.Id,
                        ProgramId = CourseProgramAssignmentRules.NormalizeProgramId(requested),
                    },
                    ct);
            }
        }, cancellationToken);

        await _cache.RemoveAsync(CoursesCacheKey(_currentUser.TenantId));
        return await LoadAsync(courseId, cancellationToken);
    }

    public async Task<CourseMasterRowDto> UpdateAsync(UpdateCourseRequest request, CancellationToken cancellationToken = default)
    {
        var code = NormalizeCode(request.Code);
        var name = NormalizeName(request.Name);
        EnsureCodeName(code, name);

        var course = await _db.Courses.FirstOrDefaultAsync(x =>
            x.Id == request.Id && x.TenantId == _currentUser.TenantId, cancellationToken)
            ?? throw new KeyNotFoundException("Course not found.");

        var dup = await _db.Courses.AnyAsync(x =>
            x.Id != request.Id &&
            x.TenantId == _currentUser.TenantId &&
            (x.Name.ToLower() == name.ToLower() || x.Code.ToLower() == code.ToLower()), cancellationToken);
        if (dup)
            throw new ValidationException("Another course already uses this code or name.");

        var programsEnabled = await ProgramsEnabledAsync(cancellationToken);

        // Program for consistency: explicit request when specified; otherwise current link.
        int? programForValidation;
        if (!programsEnabled)
            programForValidation = null;
        else if (request.ProgramIdSpecified)
            programForValidation = CourseProgramAssignmentRules.NormalizeProgramId(request.ProgramId);
        else
            programForValidation = CourseProgramAssignmentRules.NormalizeProgramId(course.ProgramId);

        await EnsureValidDepartmentOwnershipAsync(
            request.DepartmentId,
            programForValidation,
            programsEnabled,
            cancellationToken);

        await _db.ExecuteInTransactionAsync(async ct =>
        {
            var tracked = await _db.Courses.FirstOrDefaultAsync(x =>
                x.Id == request.Id && x.TenantId == _currentUser.TenantId, ct)
                ?? throw new KeyNotFoundException("Course not found.");

            tracked.Code = code;
            tracked.Name = name;
            tracked.DepartmentId = request.DepartmentId;
            tracked.UpdatedDate = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);

            if (programsEnabled && request.ProgramIdSpecified)
            {
                await _structure.AssignCourseToProgramAsync(
                    new AssignCourseProgramRequest
                    {
                        CourseId = tracked.Id,
                        ProgramId = CourseProgramAssignmentRules.NormalizeProgramId(request.ProgramId),
                    },
                    ct);
            }
            else if (!programsEnabled && tracked.ProgramId is not null)
            {
                await _structure.AssignCourseToProgramAsync(
                    new AssignCourseProgramRequest { CourseId = tracked.Id, ProgramId = null },
                    ct);
            }
        }, cancellationToken);

        await _cache.RemoveAsync(CoursesCacheKey(_currentUser.TenantId));
        return await LoadAsync(request.Id, cancellationToken);
    }

    private async Task EnsureValidDepartmentOwnershipAsync(
        int departmentId,
        int? programId,
        bool enablePrograms,
        CancellationToken ct)
    {
        CourseDepartmentAssociationRules.DepartmentSnapshot? deptSnap = null;
        if (departmentId > 0)
        {
            var dept = await _db.Departments.AsNoTracking()
                .FirstOrDefaultAsync(d => d.Id == departmentId && d.TenantId == _currentUser.TenantId, ct);
            if (dept is not null)
            {
                deptSnap = new CourseDepartmentAssociationRules.DepartmentSnapshot(
                    dept.Id, dept.TenantId, dept.CollegeId, dept.IsDeleted);
            }
        }

        CourseDepartmentAssociationRules.ProgramSnapshot? progSnap = null;
        if (programId is > 0)
        {
            var program = await _db.Programs.AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == programId && p.TenantId == _currentUser.TenantId, ct);
            if (program is not null)
            {
                progSnap = new CourseDepartmentAssociationRules.ProgramSnapshot(
                    program.Id, program.TenantId, program.CollegeId, program.DepartmentId, program.IsDeleted);
            }
        }

        var decision = CourseDepartmentAssociationRules.Evaluate(
            departmentId,
            deptSnap,
            _currentUser.TenantId,
            programId,
            progSnap,
            enablePrograms);
        if (!decision.Accepted)
            throw new ValidationException(decision.Error ?? "Invalid Course Department.");
    }

    private async Task<CourseMasterRowDto> LoadAsync(int id, CancellationToken cancellationToken)
    {
        var row = await _db.Courses.AsNoTracking()
            .Where(x => x.Id == id && x.TenantId == _currentUser.TenantId)
            .Select(x => new CourseMasterRowDto(x.Id, x.Code, x.Name, x.DepartmentId, x.ProgramId))
            .FirstAsync(cancellationToken);
        return row;
    }

    private async Task<bool> ProgramsEnabledAsync(CancellationToken cancellationToken)
    {
        return await _db.TenantAcademicConfigurations.AsNoTracking()
            .Where(c => c.TenantId == _currentUser.TenantId)
            .Select(c => c.EnablePrograms)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static string NormalizeCode(string? code) => (code ?? "").Trim().ToUpperInvariant();
    private static string NormalizeName(string? name) => (name ?? "").Trim();

    private static void EnsureCodeName(string code, string name)
    {
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(name))
            throw new ValidationException("Course code and name are required.");
    }
}
