using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.DTOs.Academic;
using Abhyanvaya.Application.Internal;
using Abhyanvaya.Domain.Entities;
using Abhyanvaya.Domain.Entities.Academic;
using Abhyanvaya.Domain.Events;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Abhyanvaya.Application.Academic;

public sealed class AcademicCatalogService : IAcademicCatalogService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IAcademicHierarchyCache _cache;
    private readonly IAcademicStatisticsCache _statisticsCache;
    private readonly IDomainEventDispatcher _domainEvents;
    private readonly IValidator<CreateProgramRequest> _createValidator;
    private readonly IValidator<UpdateProgramRequest> _updateValidator;
    private readonly IValidator<AssignCourseProgramRequest> _assignValidator;
    private readonly IValidator<UpsertProgramPolicyRequest> _policyValidator;

    public AcademicCatalogService(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IAcademicHierarchyCache cache,
        IAcademicStatisticsCache statisticsCache,
        IDomainEventDispatcher domainEvents,
        IValidator<CreateProgramRequest> createValidator,
        IValidator<UpdateProgramRequest> updateValidator,
        IValidator<AssignCourseProgramRequest> assignValidator,
        IValidator<UpsertProgramPolicyRequest> policyValidator)
    {
        _db = db;
        _currentUser = currentUser;
        _cache = cache;
        _statisticsCache = statisticsCache;
        _domainEvents = domainEvents;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _assignValidator = assignValidator;
        _policyValidator = policyValidator;
    }

    public async Task<TenantAcademicConfigurationDto> GetConfigurationAsync(CancellationToken cancellationToken = default)
    {
        var cfg = await EnsureConfigurationAsync(cancellationToken);
        return MapConfig(cfg);
    }

    public async Task<TenantAcademicConfigurationDto> UpdateConfigurationAsync(
        UpdateTenantAcademicConfigurationRequest request,
        CancellationToken cancellationToken = default)
    {
        var cfg = await EnsureConfigurationAsync(cancellationToken);
        cfg.EnablePrograms = request.EnablePrograms;
        cfg.UpdatedDate = DateTime.UtcNow;
        cfg.UpdatedBy = _currentUser.UserId > 0 ? _currentUser.UserId : null;
        await _db.SaveChangesAsync(cancellationToken);
        await InvalidateCachesAsync(cancellationToken);
        return MapConfig(cfg);
    }

    public async Task<IReadOnlyList<ProgramDto>> GetProgramsAsync(bool includeInactive = false, CancellationToken cancellationToken = default)
    {
        var cached = await _cache.GetProgramsAsync(cancellationToken);
        IReadOnlyList<Program> rows;
        if (cached is null)
        {
            rows = await _db.Programs.AsNoTracking()
                .Where(p => p.TenantId == _currentUser.TenantId)
                .OrderBy(p => p.DisplayOrder).ThenBy(p => p.ProgramName)
                .ToListAsync(cancellationToken);
            await _cache.SetProgramsAsync(rows, cancellationToken);
        }
        else
        {
            rows = cached;
        }

        if (!includeInactive)
            rows = rows.Where(p => p.IsActive && !string.Equals(p.Status, "Archived", StringComparison.OrdinalIgnoreCase)).ToList();

        return await MapProgramsAsync(rows, cancellationToken);
    }

    public async Task<ProgramDto?> GetProgramAsync(int id, CancellationToken cancellationToken = default)
    {
        var row = await _db.Programs.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id && p.TenantId == _currentUser.TenantId, cancellationToken);
        if (row is null) return null;
        return (await MapProgramsAsync([row], cancellationToken)).FirstOrDefault();
    }

    public async Task<ProgramDto> CreateProgramAsync(CreateProgramRequest request, CancellationToken cancellationToken = default)
    {
        await _createValidator.ValidateAndThrowAsync(request, cancellationToken);
        var collegeId = await ResolveCollegeIdAsync(cancellationToken);
        var code = request.ProgramCode.Trim().ToUpperInvariant();

        var exists = await _db.Programs.AnyAsync(p =>
            p.TenantId == _currentUser.TenantId && p.ProgramCode == code, cancellationToken);
        if (exists) throw new ValidationException("Program code already exists.");

        var entity = new Program
        {
            CollegeId = collegeId,
            ProgramCode = code,
            ProgramName = request.ProgramName.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            DisplayOrder = request.DisplayOrder,
            IsActive = request.IsActive,
            Status = request.IsActive ? "Active" : "Inactive",
            Icon = NormalizeOptional(request.Icon),
            ThemeColor = NormalizeOptional(request.ThemeColor),
            AcademicCalendarId = request.AcademicCalendarId is > 0 ? request.AcademicCalendarId : null,
            TenantId = _currentUser.TenantId,
            CreatedDate = DateTime.UtcNow,
            CreatedBy = _currentUser.UserId > 0 ? _currentUser.UserId : null,
        };
        await _db.AddAsync(entity);
        await _db.SaveChangesAsync(cancellationToken);
        entity.AddDomainEvent(new ProgramCreated(entity.Id, _currentUser.TenantId, code, DateTime.UtcNow));
        await DomainEventPublisher.DispatchAndClearAsync(entity, _domainEvents, cancellationToken);
        await InvalidateCachesAsync(cancellationToken);
        return (await GetProgramAsync(entity.Id, cancellationToken))!;
    }

    public async Task<ProgramDto> UpdateProgramAsync(int id, UpdateProgramRequest request, CancellationToken cancellationToken = default)
    {
        await _updateValidator.ValidateAndThrowAsync(request, cancellationToken);
        var entity = await _db.Programs.FirstOrDefaultAsync(p => p.Id == id && p.TenantId == _currentUser.TenantId, cancellationToken)
            ?? throw new KeyNotFoundException("Program not found.");

        var code = request.ProgramCode.Trim().ToUpperInvariant();
        var dup = await _db.Programs.AnyAsync(p =>
            p.TenantId == _currentUser.TenantId && p.Id != id && p.ProgramCode == code, cancellationToken);
        if (dup) throw new ValidationException("Program code already exists.");

        var status = string.IsNullOrWhiteSpace(request.Status) ? entity.Status : request.Status.Trim();
        entity.ProgramCode = code;
        entity.ProgramName = request.ProgramName.Trim();
        entity.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        entity.DisplayOrder = request.DisplayOrder;
        entity.Status = status;
        entity.IsActive = string.Equals(status, "Active", StringComparison.OrdinalIgnoreCase) && request.IsActive;
        if (string.Equals(status, "Inactive", StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, "Archived", StringComparison.OrdinalIgnoreCase))
            entity.IsActive = false;
        entity.Icon = NormalizeOptional(request.Icon);
        entity.ThemeColor = NormalizeOptional(request.ThemeColor);
        entity.AcademicCalendarId = request.AcademicCalendarId is > 0 ? request.AcademicCalendarId : null;
        entity.UpdatedDate = DateTime.UtcNow;
        entity.UpdatedBy = _currentUser.UserId > 0 ? _currentUser.UserId : null;
        entity.AddDomainEvent(new ProgramUpdated(entity.Id, _currentUser.TenantId, code, DateTime.UtcNow));
        await _db.SaveChangesAsync(cancellationToken);
        await DomainEventPublisher.DispatchAndClearAsync(entity, _domainEvents, cancellationToken);
        await InvalidateCachesAsync(cancellationToken);
        return (await GetProgramAsync(id, cancellationToken))!;
    }

    public async Task ArchiveProgramAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _db.Programs.FirstOrDefaultAsync(p => p.Id == id && p.TenantId == _currentUser.TenantId, cancellationToken)
            ?? throw new KeyNotFoundException("Program not found.");
        entity.Status = "Archived";
        entity.IsActive = false;
        entity.UpdatedDate = DateTime.UtcNow;
        entity.AddDomainEvent(new ProgramArchived(entity.Id, _currentUser.TenantId, entity.ProgramCode, DateTime.UtcNow));
        await _db.SaveChangesAsync(cancellationToken);
        await DomainEventPublisher.DispatchAndClearAsync(entity, _domainEvents, cancellationToken);
        await InvalidateCachesAsync(cancellationToken);
    }

    public async Task DeleteProgramAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _db.Programs.FirstOrDefaultAsync(p => p.Id == id && p.TenantId == _currentUser.TenantId, cancellationToken)
            ?? throw new KeyNotFoundException("Program not found.");

        var hasCourses = await _db.Courses.AnyAsync(c => c.TenantId == _currentUser.TenantId && c.ProgramId == id, cancellationToken);
        if (hasCourses)
            throw new ValidationException("A Program cannot be deleted while Courses are linked. Reassign or unlink courses first.");

        entity.IsDeleted = true;
        entity.UpdatedDate = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        await InvalidateCachesAsync(cancellationToken);
    }

    public async Task AssignCourseToProgramAsync(AssignCourseProgramRequest request, CancellationToken cancellationToken = default)
    {
        await _assignValidator.ValidateAndThrowAsync(request, cancellationToken);
        var cfg = await EnsureConfigurationAsync(cancellationToken);

        var course = await _db.Courses.FirstOrDefaultAsync(c => c.Id == request.CourseId && c.TenantId == _currentUser.TenantId, cancellationToken)
            ?? throw new KeyNotFoundException("Course not found.");

        var previousProgramId = course.ProgramId;

        if (!cfg.EnablePrograms)
        {
            course.ProgramId = null;
            course.UpdatedDate = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
            if (previousProgramId is not null)
            {
                // Course entity implements BaseEntity domain events
                course.AddDomainEvent(new CourseRemoved(course.Id, previousProgramId, _currentUser.TenantId, DateTime.UtcNow));
                await DomainEventPublisher.DispatchAndClearAsync(course, _domainEvents, cancellationToken);
            }
            await InvalidateCachesAsync(cancellationToken);
            return;
        }

        if (request.ProgramId is > 0)
        {
            var program = await _db.Programs.AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == request.ProgramId && p.TenantId == _currentUser.TenantId, cancellationToken)
                ?? throw new ValidationException("Invalid Program.");
            if (program.Status == "Archived" || !program.IsActive)
                throw new ValidationException("Archived or inactive Programs cannot receive new Courses.");
        }

        course.ProgramId = request.ProgramId is > 0 ? request.ProgramId : null;
        course.UpdatedDate = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        if (course.ProgramId is null && previousProgramId is not null)
            course.AddDomainEvent(new CourseRemoved(course.Id, previousProgramId, _currentUser.TenantId, DateTime.UtcNow));
        else
            course.AddDomainEvent(new CourseAssigned(course.Id, course.ProgramId, _currentUser.TenantId, DateTime.UtcNow));

        await DomainEventPublisher.DispatchAndClearAsync(course, _domainEvents, cancellationToken);
        await InvalidateCachesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Course>> GetCoursesAsync(CancellationToken cancellationToken = default)
    {
        var cached = await _cache.GetCoursesAsync(cancellationToken);
        if (cached is not null) return cached;

        var rows = await _db.Courses.AsNoTracking()
            .Where(c => c.TenantId == _currentUser.TenantId)
            .OrderBy(c => c.DisplayOrder).ThenBy(c => c.Name)
            .ToListAsync(cancellationToken);
        await _cache.SetCoursesAsync(rows, cancellationToken);
        return rows;
    }

    public async Task<IReadOnlyList<Group>> GetGroupsAsync(CancellationToken cancellationToken = default)
    {
        var cached = await _cache.GetGroupsAsync(cancellationToken);
        if (cached is not null) return cached;

        var rows = await _db.Groups.AsNoTracking()
            .Where(g => g.TenantId == _currentUser.TenantId)
            .OrderBy(g => g.DisplayOrder).ThenBy(g => g.Name)
            .ToListAsync(cancellationToken);
        await _cache.SetGroupsAsync(rows, cancellationToken);
        return rows;
    }

    public async Task<IReadOnlyList<Semester>> GetSemestersAsync(CancellationToken cancellationToken = default)
    {
        var cached = await _cache.GetSemestersAsync(cancellationToken);
        if (cached is not null) return cached;

        var rows = await _db.Semesters.AsNoTracking()
            .Where(s => s.TenantId == _currentUser.TenantId)
            .OrderBy(s => s.DisplayOrder).ThenBy(s => s.Name)
            .ToListAsync(cancellationToken);
        await _cache.SetSemestersAsync(rows, cancellationToken);
        return rows;
    }

    public async Task<IReadOnlyList<SectionDto>> GetSectionsAsync(CancellationToken cancellationToken = default)
    {
        return await _db.Sections.AsNoTracking()
            .Where(s => s.TenantId == _currentUser.TenantId)
            .OrderBy(s => s.DisplayOrder)
            .ThenBy(s => s.SectionName)
            .Select(s => new SectionDto
            {
                Id = s.Id,
                CollegeId = s.CollegeId,
                AcademicYearId = s.AcademicYearId,
                CourseId = s.CourseId,
                GroupId = s.GroupId,
                SemesterId = s.SemesterId,
                SectionCode = s.SectionCode,
                SectionName = s.SectionName,
                DisplayOrder = s.DisplayOrder,
                MaximumStrength = s.MaximumStrength,
                Status = s.Status,
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SubjectCatalogItemDto>> GetSubjectsAsync(CancellationToken cancellationToken = default)
    {
        return await (
            from s in _db.Subjects.AsNoTracking()
            join ts in _db.TenantSubjects.AsNoTracking() on s.TenantSubjectId equals ts.Id
            where s.TenantId == _currentUser.TenantId
            orderby s.DisplayOrder, ts.Name
            select new SubjectCatalogItemDto
            {
                Id = s.Id,
                CourseId = s.CourseId,
                GroupId = s.GroupId,
                SemesterId = s.SemesterId,
                Code = ts.Code ?? "",
                Name = ts.Name,
                DisplayOrder = s.DisplayOrder,
            }).ToListAsync(cancellationToken);
    }

    public async Task<ProgramPolicyDto?> GetProgramPolicyAsync(int programId, CancellationToken cancellationToken = default)
    {
        var exists = await _db.Programs.AsNoTracking()
            .AnyAsync(p => p.Id == programId && p.TenantId == _currentUser.TenantId, cancellationToken);
        if (!exists) return null;

        var policy = await _db.ProgramPolicies.AsNoTracking()
            .FirstOrDefaultAsync(p => p.ProgramId == programId && p.TenantId == _currentUser.TenantId, cancellationToken);
        return policy is null ? null : MapPolicy(policy);
    }

    public async Task<ProgramPolicyDto> UpsertProgramPolicyAsync(
        int programId,
        UpsertProgramPolicyRequest request,
        CancellationToken cancellationToken = default)
    {
        await _policyValidator.ValidateAndThrowAsync(request, cancellationToken);
        _ = await _db.Programs.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == programId && p.TenantId == _currentUser.TenantId, cancellationToken)
            ?? throw new KeyNotFoundException("Program not found.");

        var policy = await _db.ProgramPolicies
            .FirstOrDefaultAsync(p => p.ProgramId == programId && p.TenantId == _currentUser.TenantId, cancellationToken);

        if (policy is null)
        {
            policy = new ProgramPolicy
            {
                ProgramId = programId,
                TenantId = _currentUser.TenantId,
                CreatedDate = DateTime.UtcNow,
                CreatedBy = _currentUser.UserId > 0 ? _currentUser.UserId : null,
            };
            await _db.AddAsync(policy);
        }

        policy.MinimumAttendancePercent = request.MinimumAttendancePercent;
        policy.CreditsRequired = request.CreditsRequired;
        policy.PassMarks = request.PassMarks;
        policy.MaximumBacklogs = request.MaximumBacklogs;
        policy.MaximumSubjects = request.MaximumSubjects;
        policy.AcademicRules = string.IsNullOrWhiteSpace(request.AcademicRules) ? null : request.AcademicRules.Trim();
        policy.UpdatedDate = DateTime.UtcNow;
        policy.UpdatedBy = _currentUser.UserId > 0 ? _currentUser.UserId : null;
        await _db.SaveChangesAsync(cancellationToken);
        return MapPolicy(policy);
    }

    internal async Task<TenantAcademicConfiguration> EnsureConfigurationAsync(CancellationToken ct)
    {
        var cfg = await _db.TenantAcademicConfigurations
            .FirstOrDefaultAsync(c => c.TenantId == _currentUser.TenantId, ct);
        if (cfg is not null) return cfg;

        var collegeId = await ResolveCollegeIdAsync(ct);
        cfg = new TenantAcademicConfiguration
        {
            CollegeId = collegeId,
            EnablePrograms = false,
            TenantId = _currentUser.TenantId,
            CreatedDate = DateTime.UtcNow,
            CreatedBy = _currentUser.UserId > 0 ? _currentUser.UserId : null,
        };
        await _db.AddAsync(cfg);
        await _db.SaveChangesAsync(ct);
        return cfg;
    }

    private async Task<int> ResolveCollegeIdAsync(CancellationToken ct)
    {
        var id = await _db.Colleges.AsNoTracking()
            .Where(c => c.TenantId == _currentUser.TenantId)
            .OrderBy(c => c.Id)
            .Select(c => c.Id)
            .FirstOrDefaultAsync(ct);
        return id > 0 ? id : _currentUser.TenantId;
    }

    private static TenantAcademicConfigurationDto MapConfig(TenantAcademicConfiguration cfg) => new()
    {
        Id = cfg.Id,
        CollegeId = cfg.CollegeId,
        EnablePrograms = cfg.EnablePrograms,
    };

    private static ProgramPolicyDto MapPolicy(ProgramPolicy p) => new()
    {
        Id = p.Id,
        ProgramId = p.ProgramId,
        MinimumAttendancePercent = p.MinimumAttendancePercent,
        CreditsRequired = p.CreditsRequired,
        PassMarks = p.PassMarks,
        MaximumBacklogs = p.MaximumBacklogs,
        MaximumSubjects = p.MaximumSubjects,
        AcademicRules = p.AcademicRules,
    };

    private async Task InvalidateCachesAsync(CancellationToken cancellationToken)
    {
        await _cache.InvalidateHierarchyAsync(cancellationToken);
        await _statisticsCache.InvalidateAsync(cancellationToken);
    }

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private async Task<IReadOnlyList<ProgramDto>> MapProgramsAsync(IReadOnlyList<Program> rows, CancellationToken ct)
    {
        if (rows.Count == 0) return [];
        var ids = rows.Select(r => r.Id).ToList();
        var courseCounts = await _db.Courses.AsNoTracking()
            .Where(c => c.TenantId == _currentUser.TenantId && c.ProgramId != null && ids.Contains(c.ProgramId.Value))
            .GroupBy(c => c.ProgramId!.Value)
            .Select(g => new { ProgramId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ProgramId, x => x.Count, ct);

        var result = new List<ProgramDto>(rows.Count);
        foreach (var p in rows)
        {
            var courseIds = await _db.Courses.AsNoTracking()
                .Where(c => c.TenantId == _currentUser.TenantId && c.ProgramId == p.Id)
                .Select(c => c.Id)
                .ToListAsync(ct);
            var studentCount = courseIds.Count == 0
                ? 0
                : await _db.Students.CountAsync(s => s.TenantId == _currentUser.TenantId && courseIds.Contains(s.CourseId), ct);
            var facultyCount = 0;
            if (courseIds.Count > 0)
            {
                var subjectIds = await _db.Subjects.AsNoTracking()
                    .Where(s => s.TenantId == _currentUser.TenantId && courseIds.Contains(s.CourseId))
                    .Select(s => s.Id)
                    .ToListAsync(ct);
                if (subjectIds.Count > 0)
                {
                    facultyCount = await _db.StaffSubjectAssignments.AsNoTracking()
                        .Where(a => a.TenantId == _currentUser.TenantId && subjectIds.Contains(a.SubjectId))
                        .Select(a => a.StaffId)
                        .Distinct()
                        .CountAsync(ct);
                }
            }

            result.Add(new ProgramDto
            {
                Id = p.Id,
                CollegeId = p.CollegeId,
                ProgramCode = p.ProgramCode,
                ProgramName = p.ProgramName,
                Description = p.Description,
                DisplayOrder = p.DisplayOrder,
                IsActive = p.IsActive,
                Status = p.Status,
                Icon = p.Icon,
                ThemeColor = p.ThemeColor,
                AcademicCalendarId = p.AcademicCalendarId,
                CourseCount = courseCounts.GetValueOrDefault(p.Id),
                StudentCount = studentCount,
                FacultyCount = facultyCount,
            });
        }
        return result;
    }
}
