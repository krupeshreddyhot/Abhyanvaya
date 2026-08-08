using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.DTOs.Academic;
using Abhyanvaya.Domain.Entities.Academic;
using Microsoft.EntityFrameworkCore;

namespace Abhyanvaya.Application.Academic;

public sealed class SectionManagementService : ISectionManagementService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly ISectionCapacityEngine _capacity;
    private readonly ISectionVersioningService _versions;

    public SectionManagementService(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        ISectionCapacityEngine capacity,
        ISectionVersioningService versions)
    {
        _db = db;
        _currentUser = currentUser;
        _capacity = capacity;
        _versions = versions;
    }

    public async Task<IReadOnlyList<SectionDto>> GetSectionsAsync(
        int? academicYearId = null,
        int? courseId = null,
        int? groupId = null,
        int? semesterId = null,
        CancellationToken cancellationToken = default)
    {
        var q = _db.Sections.AsNoTracking().Where(s => s.TenantId == _currentUser.TenantId);
        if (academicYearId is > 0) q = q.Where(s => s.AcademicYearId == academicYearId);
        if (courseId is > 0) q = q.Where(s => s.CourseId == courseId);
        if (groupId is > 0) q = q.Where(s => s.GroupId == groupId);
        if (semesterId is > 0) q = q.Where(s => s.SemesterId == semesterId);

        var rows = await q.OrderBy(s => s.DisplayOrder).ThenBy(s => s.SectionCode).ToListAsync(cancellationToken);
        return await MapSectionsAsync(rows, cancellationToken);
    }

    public async Task<SectionDto?> GetSectionAsync(int id, CancellationToken cancellationToken = default)
    {
        var row = await _db.Sections.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == id && s.TenantId == _currentUser.TenantId, cancellationToken);
        if (row is null) return null;
        var mapped = await MapSectionsAsync([row], cancellationToken);
        return mapped.FirstOrDefault();
    }

    public async Task<SectionDto> CreateSectionAsync(CreateSectionRequest request, CancellationToken cancellationToken = default)
    {
        ValidateCreate(request);
        await EnsureScopeExistsAsync(request.AcademicYearId, request.CourseId, request.GroupId, request.SemesterId, cancellationToken);

        var code = request.SectionCode.Trim().ToUpperInvariant();
        var exists = await _db.Sections.AnyAsync(s =>
            s.TenantId == _currentUser.TenantId
            && s.AcademicYearId == request.AcademicYearId
            && s.CourseId == request.CourseId
            && s.GroupId == request.GroupId
            && s.SemesterId == request.SemesterId
            && s.SectionCode == code, cancellationToken);
        if (exists) throw new InvalidOperationException($"Section '{code}' already exists for this semester scope.");

        var collegeId = request.CollegeId is > 0
            ? request.CollegeId.Value
            : await ResolveCollegeIdAsync(cancellationToken);

        var maxCap = request.MaximumStrength > 0 ? request.MaximumStrength : 60;
        var entity = new Section
        {
            CollegeId = collegeId,
            AcademicYearId = request.AcademicYearId,
            CourseId = request.CourseId,
            GroupId = request.GroupId,
            SemesterId = request.SemesterId,
            SectionCode = code,
            SectionName = request.SectionName.Trim(),
            DisplayOrder = request.DisplayOrder,
            MaximumStrength = maxCap,
            MinimumCapacity = Math.Max(0, request.MinimumCapacity),
            RecommendedCapacity = request.RecommendedCapacity > 0 ? request.RecommendedCapacity : maxCap,
            ReservedSeats = Math.Max(0, request.ReservedSeats),
            WaitingListCount = Math.Max(0, request.WaitingListCount),
            Status = string.IsNullOrWhiteSpace(request.Status) ? "Active" : request.Status.Trim(),
            SectionTypeCode = string.IsNullOrWhiteSpace(request.SectionTypeCode) ? "Regular" : request.SectionTypeCode.Trim(),
            TenantId = _currentUser.TenantId,
            CreatedDate = DateTime.UtcNow,
            CreatedBy = _currentUser.UserId > 0 ? _currentUser.UserId : null,
        };

        await _db.AddAsync(entity);
        await _db.SaveChangesAsync(cancellationToken);
        await _versions.RecordAsync(entity, Domain.Academic.SectionVersionOperations.Create, "Section created", 0, cancellationToken);
        return (await GetSectionAsync(entity.Id, cancellationToken))!;
    }

    public async Task<SectionDto> UpdateSectionAsync(int id, UpdateSectionRequest request, CancellationToken cancellationToken = default)
    {
        var entity = await _db.Sections.FirstOrDefaultAsync(s => s.Id == id && s.TenantId == _currentUser.TenantId, cancellationToken)
            ?? throw new KeyNotFoundException("Section not found.");

        var code = request.SectionCode.Trim().ToUpperInvariant();
        var dup = await _db.Sections.AnyAsync(s =>
            s.TenantId == _currentUser.TenantId
            && s.Id != id
            && s.AcademicYearId == entity.AcademicYearId
            && s.CourseId == entity.CourseId
            && s.GroupId == entity.GroupId
            && s.SemesterId == entity.SemesterId
            && s.SectionCode == code, cancellationToken);
        if (dup) throw new InvalidOperationException($"Section '{code}' already exists for this semester scope.");

        entity.SectionCode = code;
        entity.SectionName = request.SectionName.Trim();
        entity.DisplayOrder = request.DisplayOrder;
        entity.MaximumStrength = request.MaximumStrength > 0 ? request.MaximumStrength : entity.MaximumStrength;
        entity.MinimumCapacity = Math.Max(0, request.MinimumCapacity);
        entity.RecommendedCapacity = request.RecommendedCapacity > 0 ? request.RecommendedCapacity : entity.MaximumStrength;
        entity.ReservedSeats = Math.Max(0, request.ReservedSeats);
        entity.WaitingListCount = Math.Max(0, request.WaitingListCount);
        if (!string.IsNullOrWhiteSpace(request.SectionTypeCode))
            entity.SectionTypeCode = request.SectionTypeCode.Trim();
        // Status changes are owned by ISectionLifecycleService (AI29.1B state machine).
        entity.UpdatedDate = DateTime.UtcNow;
        entity.UpdatedBy = _currentUser.UserId > 0 ? _currentUser.UserId : null;

        await _db.SaveChangesAsync(cancellationToken);
        var strength = await _db.StudentSections.CountAsync(x => x.SectionId == id && x.IsCurrent, cancellationToken);
        await _versions.RecordAsync(entity, Domain.Academic.SectionVersionOperations.Update, "Section updated", strength, cancellationToken);
        return (await GetSectionAsync(id, cancellationToken))!;
    }

    public async Task DeleteSectionAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _db.Sections.FirstOrDefaultAsync(s => s.Id == id && s.TenantId == _currentUser.TenantId, cancellationToken)
            ?? throw new KeyNotFoundException("Section not found.");

        var hasCurrent = await _db.StudentSections.AnyAsync(x => x.SectionId == id && x.IsCurrent, cancellationToken);
        if (hasCurrent) throw new InvalidOperationException("Cannot delete a section with current student allocations. Transfer students first.");

        entity.IsDeleted = true;
        entity.UpdatedDate = DateTime.UtcNow;
        entity.UpdatedBy = _currentUser.UserId > 0 ? _currentUser.UserId : null;
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task EnsureDefaultGeneralSectionAsync(
        int academicYearId,
        int courseId,
        int groupId,
        int semesterId,
        CancellationToken cancellationToken = default)
    {
        var exists = await _db.Sections.AnyAsync(s =>
            s.TenantId == _currentUser.TenantId
            && s.AcademicYearId == academicYearId
            && s.CourseId == courseId
            && s.GroupId == groupId
            && s.SemesterId == semesterId, cancellationToken);
        if (exists) return;

        await CreateSectionAsync(new CreateSectionRequest
        {
            AcademicYearId = academicYearId,
            CourseId = courseId,
            GroupId = groupId,
            SemesterId = semesterId,
            SectionCode = "GEN",
            SectionName = "General",
            DisplayOrder = 0,
            MaximumStrength = 60,
            Status = "Active",
        }, cancellationToken);
    }

    public async Task<IReadOnlyList<StudentSectionDto>> GetStudentSectionsAsync(
        int? sectionId = null,
        int? studentId = null,
        bool currentOnly = true,
        CancellationToken cancellationToken = default)
    {
        var q = _db.StudentSections.AsNoTracking().Where(x => x.TenantId == _currentUser.TenantId);
        if (sectionId is > 0) q = q.Where(x => x.SectionId == sectionId);
        if (studentId is > 0) q = q.Where(x => x.StudentId == studentId);
        if (currentOnly) q = q.Where(x => x.IsCurrent);

        var rows = await q.OrderByDescending(x => x.EffectiveFrom).ToListAsync(cancellationToken);
        return await MapStudentSectionsAsync(rows, cancellationToken);
    }

    public async Task<StudentSectionDto> AssignStudentAsync(AssignStudentSectionRequest request, CancellationToken cancellationToken = default)
    {
        var section = await _db.Sections.FirstOrDefaultAsync(s => s.Id == request.SectionId && s.TenantId == _currentUser.TenantId, cancellationToken)
            ?? throw new KeyNotFoundException("Section not found.");

        var studentOk = await _db.Students.AnyAsync(s =>
            s.Id == request.StudentId
            && s.TenantId == _currentUser.TenantId
            && s.CourseId == section.CourseId
            && s.GroupId == section.GroupId
            && s.SemesterId == section.SemesterId, cancellationToken);
        if (!studentOk) throw new InvalidOperationException("Student does not belong to the section's course/group/semester.");

        await _capacity.EnsureCanAcceptStudentAsync(section, cancellationToken);

        var current = await _db.StudentSections
            .Where(x => x.TenantId == _currentUser.TenantId && x.StudentId == request.StudentId && x.IsCurrent)
            .ToListAsync(cancellationToken);
        var today = request.EffectiveFrom ?? DateOnly.FromDateTime(DateTime.UtcNow);
        foreach (var c in current)
        {
            c.IsCurrent = false;
            c.EffectiveTo = today.AddDays(-1);
            c.UpdatedDate = DateTime.UtcNow;
        }

        var row = new StudentSection
        {
            StudentId = request.StudentId,
            SectionId = request.SectionId,
            EffectiveFrom = today,
            IsCurrent = true,
            TenantId = _currentUser.TenantId,
            CreatedDate = DateTime.UtcNow,
            CreatedBy = _currentUser.UserId > 0 ? _currentUser.UserId : null,
        };
        await _db.AddAsync(row);
        await _db.SaveChangesAsync(cancellationToken);
        return (await GetStudentSectionsAsync(studentId: request.StudentId, currentOnly: true, cancellationToken: cancellationToken)).First();
    }

    public async Task<StudentSectionDto> TransferStudentAsync(TransferStudentSectionRequest request, CancellationToken cancellationToken = default)
    {
        var result = await AssignStudentAsync(new AssignStudentSectionRequest
        {
            StudentId = request.StudentId,
            SectionId = request.TargetSectionId,
            EffectiveFrom = request.EffectiveFrom,
        }, cancellationToken);

        var latest = await _db.StudentSections
            .Where(x => x.TenantId == _currentUser.TenantId && x.StudentId == request.StudentId && x.IsCurrent)
            .OrderByDescending(x => x.Id)
            .FirstAsync(cancellationToken);
        latest.TransferReason = request.Reason;
        await _db.SaveChangesAsync(cancellationToken);
        return (await GetStudentSectionsAsync(studentId: request.StudentId, currentOnly: true, cancellationToken: cancellationToken)).First();
    }

    public async Task<IReadOnlyList<FacultySectionDto>> GetFacultySectionsAsync(
        int? sectionId = null,
        int? facultyId = null,
        bool currentOnly = true,
        CancellationToken cancellationToken = default)
    {
        var q = _db.FacultySectionAssignments.AsNoTracking().Where(x => x.TenantId == _currentUser.TenantId);
        if (sectionId is > 0) q = q.Where(x => x.SectionId == sectionId);
        if (facultyId is > 0) q = q.Where(x => x.FacultyId == facultyId);
        if (currentOnly) q = q.Where(x => x.IsCurrent);
        var rows = await q.OrderBy(x => x.SectionId).ToListAsync(cancellationToken);
        return await MapFacultySectionsAsync(rows, cancellationToken);
    }

    public async Task<FacultySectionDto> AssignFacultyAsync(AssignFacultySectionRequest request, CancellationToken cancellationToken = default)
    {
        _ = await _db.Sections.FirstOrDefaultAsync(s => s.Id == request.SectionId && s.TenantId == _currentUser.TenantId, cancellationToken)
            ?? throw new KeyNotFoundException("Section not found.");
        var facultyOk = await _db.StaffMembers.AnyAsync(s => s.Id == request.FacultyId && s.TenantId == _currentUser.TenantId, cancellationToken);
        if (!facultyOk) throw new InvalidOperationException("Invalid faculty.");

        var existing = await _db.FacultySectionAssignments
            .Where(x => x.TenantId == _currentUser.TenantId
                        && x.FacultyId == request.FacultyId
                        && x.SectionId == request.SectionId
                        && x.IsCurrent)
            .FirstOrDefaultAsync(cancellationToken);
        if (existing is not null)
        {
            existing.Role = string.IsNullOrWhiteSpace(request.Role) ? existing.Role : request.Role.Trim();
            existing.AcademicYearId = request.AcademicYearId;
            existing.UpdatedDate = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
            return (await GetFacultySectionsAsync(sectionId: request.SectionId, facultyId: request.FacultyId, cancellationToken: cancellationToken)).First();
        }

        var row = new FacultySectionAssignment
        {
            FacultyId = request.FacultyId,
            SectionId = request.SectionId,
            AcademicYearId = request.AcademicYearId,
            Role = string.IsNullOrWhiteSpace(request.Role) ? "Primary" : request.Role.Trim(),
            EffectiveFrom = request.EffectiveFrom ?? DateOnly.FromDateTime(DateTime.UtcNow),
            IsCurrent = true,
            TenantId = _currentUser.TenantId,
            CreatedDate = DateTime.UtcNow,
            CreatedBy = _currentUser.UserId > 0 ? _currentUser.UserId : null,
        };
        await _db.AddAsync(row);
        await _db.SaveChangesAsync(cancellationToken);
        return (await GetFacultySectionsAsync(sectionId: request.SectionId, facultyId: request.FacultyId, cancellationToken: cancellationToken)).First();
    }

    public async Task<IReadOnlyList<TimetableSectionDto>> GetTimetableSectionsAsync(int timetableId, CancellationToken cancellationToken = default)
    {
        var rows = await _db.TimetableSections.AsNoTracking()
            .Where(x => x.TenantId == _currentUser.TenantId && x.TimetableId == timetableId)
            .ToListAsync(cancellationToken);
        return await MapTimetableSectionsAsync(rows, cancellationToken);
    }

    public async Task<IReadOnlyList<TimetableSectionDto>> SetTimetableSectionsAsync(
        int timetableId,
        SetTimetableSectionsRequest request,
        CancellationToken cancellationToken = default)
    {
        var ttOk = await _db.SchedulingTimetables.AnyAsync(t => t.Id == timetableId && t.TenantId == _currentUser.TenantId, cancellationToken);
        if (!ttOk) throw new KeyNotFoundException("Timetable not found.");

        var existing = await _db.TimetableSections
            .Where(x => x.TenantId == _currentUser.TenantId
                        && x.TimetableId == timetableId
                        && x.TimetableEntryId == request.TimetableEntryId)
            .ToListAsync(cancellationToken);
        foreach (var e in existing)
        {
            e.IsDeleted = true;
            e.UpdatedDate = DateTime.UtcNow;
        }

        var ids = request.SectionIds.Distinct().Where(id => id > 0).ToList();
        foreach (var sectionId in ids)
        {
            var sectionOk = await _db.Sections.AnyAsync(s => s.Id == sectionId && s.TenantId == _currentUser.TenantId, cancellationToken);
            if (!sectionOk) throw new InvalidOperationException($"Invalid section {sectionId}.");
            await _db.AddAsync(new TimetableSection
            {
                TimetableId = timetableId,
                TimetableEntryId = request.TimetableEntryId,
                SectionId = sectionId,
                TenantId = _currentUser.TenantId,
                CreatedDate = DateTime.UtcNow,
                CreatedBy = _currentUser.UserId > 0 ? _currentUser.UserId : null,
            });
        }

        await _db.SaveChangesAsync(cancellationToken);
        return await GetTimetableSectionsAsync(timetableId, cancellationToken);
    }

    public async Task<AutoAllocateSectionsResult> AutoAllocateAsync(AutoAllocateSectionsRequest request, CancellationToken cancellationToken = default)
    {
        var strategy = string.IsNullOrWhiteSpace(request.Strategy) ? "Alphabetical" : request.Strategy.Trim();
        var sections = await _db.Sections
            .Where(s => s.TenantId == _currentUser.TenantId
                        && s.AcademicYearId == request.AcademicYearId
                        && s.CourseId == request.CourseId
                        && s.GroupId == request.GroupId
                        && s.SemesterId == request.SemesterId
                        && s.Status == "Active")
            .OrderBy(s => s.DisplayOrder)
            .ThenBy(s => s.SectionCode)
            .ToListAsync(cancellationToken);

        if (sections.Count == 0)
            return new AutoAllocateSectionsResult { Strategy = strategy, Messages = ["No active sections for scope."], SkippedCount = 0 };

        var assignedStudentIds = await _db.StudentSections.AsNoTracking()
            .Where(x => x.TenantId == _currentUser.TenantId && x.IsCurrent)
            .Select(x => x.StudentId)
            .ToListAsync(cancellationToken);

        var students = await _db.Students.AsNoTracking()
            .Where(s => s.TenantId == _currentUser.TenantId
                        && s.CourseId == request.CourseId
                        && s.GroupId == request.GroupId
                        && s.SemesterId == request.SemesterId
                        && !assignedStudentIds.Contains(s.Id))
            .ToListAsync(cancellationToken);

        students = strategy switch
        {
            "GenderBalance" => students.OrderBy(s => s.GenderId).ThenBy(s => s.Name).ToList(),
            "Merit" => students.OrderBy(s => s.StudentNumber).ToList(),
            "Random" => students.OrderBy(_ => Guid.NewGuid()).ToList(),
            "CapacityBased" => students.OrderBy(s => s.Name).ToList(),
            _ => students.OrderBy(s => s.Name).ToList(),
        };

        var counts = new Dictionary<int, int>();
        foreach (var sec in sections)
        {
            var c = await _db.StudentSections.CountAsync(x => x.SectionId == sec.Id && x.IsCurrent, cancellationToken);
            counts[sec.Id] = c;
        }

        var assigned = 0;
        var skipped = 0;
        var messages = new List<string>();
        var idx = 0;
        foreach (var student in students)
        {
            Section? target = strategy == "CapacityBased"
                ? sections
                    .Where(s => counts[s.Id] < s.MaximumStrength)
                    .OrderBy(s => (double)counts[s.Id] / Math.Max(1, s.MaximumStrength))
                    .FirstOrDefault()
                : sections[idx % sections.Count];

            if (target is null || counts[target.Id] >= target.MaximumStrength)
            {
                skipped++;
                continue;
            }

            await AssignStudentAsync(new AssignStudentSectionRequest
            {
                StudentId = student.Id,
                SectionId = target.Id,
            }, cancellationToken);
            counts[target.Id]++;
            assigned++;
            idx++;
        }

        messages.Add($"Assigned {assigned} students using {strategy}.");
        if (skipped > 0) messages.Add($"{skipped} students skipped (capacity).");
        return new AutoAllocateSectionsResult
        {
            AssignedCount = assigned,
            SkippedCount = skipped,
            Strategy = strategy,
            Messages = messages,
        };
    }

    public Task<IReadOnlyList<SectionDto>> GetSectionsForDashboardAsync(CancellationToken cancellationToken = default)
        => GetSectionsAsync(cancellationToken: cancellationToken);

    public async Task<IReadOnlyList<SectionStatisticsDto>> GetSectionStatisticsAsync(
        int? academicYearId = null,
        int? semesterId = null,
        CancellationToken cancellationToken = default)
    {
        var sections = await GetSectionsAsync(academicYearId, semesterId: semesterId, cancellationToken: cancellationToken);
        var result = new List<SectionStatisticsDto>();
        foreach (var s in sections)
        {
            var faculty = await _db.FacultySectionAssignments.CountAsync(
                x => x.SectionId == s.Id && x.IsCurrent && x.TenantId == _currentUser.TenantId, cancellationToken);
            result.Add(new SectionStatisticsDto
            {
                SectionId = s.Id,
                SectionCode = s.SectionCode,
                SectionName = s.SectionName,
                MaximumStrength = s.MaximumStrength,
                StudentCount = s.CurrentStrength,
                FacultyCount = faculty,
                RemainingCapacity = s.RemainingCapacity,
                UtilizationPercent = s.MaximumStrength <= 0 ? 0 : Math.Round(100.0 * s.CurrentStrength / s.MaximumStrength, 1),
            });
        }
        return result;
    }

    public Task<IReadOnlyList<FacultySectionDto>> GetFacultyPerSectionAsync(int sectionId, CancellationToken cancellationToken = default)
        => GetFacultySectionsAsync(sectionId: sectionId, cancellationToken: cancellationToken);

    public Task<IReadOnlyList<StudentSectionDto>> GetStudentsPerSectionAsync(int sectionId, CancellationToken cancellationToken = default)
        => GetStudentSectionsAsync(sectionId: sectionId, cancellationToken: cancellationToken);

    public async Task<IReadOnlyList<TimetableSectionDto>> GetCombinedSessionsAsync(int? timetableId = null, CancellationToken cancellationToken = default)
    {
        var q = _db.TimetableSections.AsNoTracking().Where(x => x.TenantId == _currentUser.TenantId);
        if (timetableId is > 0) q = q.Where(x => x.TimetableId == timetableId);
        var rows = await q.ToListAsync(cancellationToken);
        var grouped = rows.GroupBy(x => new { x.TimetableId, x.TimetableEntryId })
            .Where(g => g.Select(x => x.SectionId).Distinct().Count() > 1)
            .SelectMany(g => g)
            .ToList();
        return await MapTimetableSectionsAsync(grouped, cancellationToken);
    }

    public async Task<IReadOnlyList<SectionReportRowDto>> GetReportAsync(string kind, CancellationToken cancellationToken = default)
    {
        var k = (kind ?? "").Trim().ToLowerInvariant();
        var stats = await GetSectionStatisticsAsync(cancellationToken: cancellationToken);
        return k switch
        {
            "students-by-section" => stats.Select(s => new SectionReportRowDto
            {
                ReportKind = "StudentsBySection",
                SectionId = s.SectionId,
                SectionCode = s.SectionCode,
                SectionName = s.SectionName,
                Count = s.StudentCount,
            }).ToList(),
            "faculty-by-section" => stats.Select(s => new SectionReportRowDto
            {
                ReportKind = "FacultyBySection",
                SectionId = s.SectionId,
                SectionCode = s.SectionCode,
                SectionName = s.SectionName,
                Count = s.FacultyCount,
            }).ToList(),
            "section-capacity" => stats.Select(s => new SectionReportRowDto
            {
                ReportKind = "SectionCapacity",
                SectionId = s.SectionId,
                SectionCode = s.SectionCode,
                SectionName = s.SectionName,
                Detail = $"{s.StudentCount}/{s.MaximumStrength} ({s.UtilizationPercent}%)",
                Count = s.RemainingCapacity,
            }).ToList(),
            "section-transfers" => await (
                from ss in _db.StudentSections.AsNoTracking()
                join sec in _db.Sections.AsNoTracking() on ss.SectionId equals sec.Id
                where ss.TenantId == _currentUser.TenantId && ss.TransferReason != null
                select new SectionReportRowDto
                {
                    ReportKind = "SectionTransfers",
                    SectionId = sec.Id,
                    SectionCode = sec.SectionCode,
                    SectionName = sec.SectionName,
                    Detail = ss.TransferReason,
                    Count = 1,
                }).ToListAsync(cancellationToken),
            _ => stats.Select(s => new SectionReportRowDto
            {
                ReportKind = "SectionSummary",
                SectionId = s.SectionId,
                SectionCode = s.SectionCode,
                SectionName = s.SectionName,
                Count = s.StudentCount,
            }).ToList(),
        };
    }

    private static void ValidateCreate(CreateSectionRequest request)
    {
        if (request.AcademicYearId <= 0 || request.CourseId <= 0 || request.GroupId <= 0 || request.SemesterId <= 0)
            throw new ArgumentException("Academic year, course, group and semester are required.");
        if (string.IsNullOrWhiteSpace(request.SectionCode) || string.IsNullOrWhiteSpace(request.SectionName))
            throw new ArgumentException("Section code and name are required.");
    }

    private async Task EnsureScopeExistsAsync(int yearId, int courseId, int groupId, int semesterId, CancellationToken ct)
    {
        if (!await _db.SchedulingAcademicYears.AnyAsync(y => y.Id == yearId && y.TenantId == _currentUser.TenantId, ct))
            throw new InvalidOperationException("Invalid academic year.");
        if (!await _db.Courses.AnyAsync(c => c.Id == courseId && c.TenantId == _currentUser.TenantId, ct))
            throw new InvalidOperationException("Invalid course.");
        if (!await _db.Groups.AnyAsync(g => g.Id == groupId && g.TenantId == _currentUser.TenantId && g.CourseId == courseId, ct))
            throw new InvalidOperationException("Invalid group for course.");
        if (!await _db.Semesters.AnyAsync(s => s.Id == semesterId && s.TenantId == _currentUser.TenantId, ct))
            throw new InvalidOperationException("Invalid semester.");
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

    private async Task<IReadOnlyList<SectionDto>> MapSectionsAsync(IReadOnlyList<Section> rows, CancellationToken ct)
    {
        if (rows.Count == 0) return [];
        var ids = rows.Select(r => r.Id).ToList();
        var capacityMap = (await _capacity.GetOccupancyAsync(ids, cancellationToken: ct))
            .ToDictionary(x => x.SectionId);

        var yearIds = rows.Select(r => r.AcademicYearId).Distinct().ToList();
        var courseIds = rows.Select(r => r.CourseId).Distinct().ToList();
        var groupIds = rows.Select(r => r.GroupId).Distinct().ToList();
        var semesterIds = rows.Select(r => r.SemesterId).Distinct().ToList();

        var years = await _db.SchedulingAcademicYears.AsNoTracking().Where(y => yearIds.Contains(y.Id)).ToDictionaryAsync(y => y.Id, y => y.Name, ct);
        var courses = await _db.Courses.AsNoTracking().Where(c => courseIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.Name, ct);
        var groups = await _db.Groups.AsNoTracking().Where(g => groupIds.Contains(g.Id)).ToDictionaryAsync(g => g.Id, g => g.Name, ct);
        var semesters = await _db.Semesters.AsNoTracking().Where(s => semesterIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id, s => s.Name, ct);

        return rows.Select(r =>
        {
            capacityMap.TryGetValue(r.Id, out var cap);
            var strength = cap?.CurrentStrength ?? 0;
            return new SectionDto
            {
                Id = r.Id,
                CollegeId = r.CollegeId,
                AcademicYearId = r.AcademicYearId,
                AcademicYearName = years.GetValueOrDefault(r.AcademicYearId),
                CourseId = r.CourseId,
                CourseName = courses.GetValueOrDefault(r.CourseId),
                GroupId = r.GroupId,
                GroupName = groups.GetValueOrDefault(r.GroupId),
                SemesterId = r.SemesterId,
                SemesterName = semesters.GetValueOrDefault(r.SemesterId),
                SectionCode = r.SectionCode,
                SectionName = r.SectionName,
                DisplayOrder = r.DisplayOrder,
                MaximumStrength = r.MaximumStrength,
                Status = r.Status,
                CurrentStrength = strength,
                RemainingCapacity = cap?.AvailableSeats ?? Math.Max(0, r.MaximumStrength - strength),
                SectionTypeCode = r.SectionTypeCode,
                MinimumCapacity = r.MinimumCapacity,
                RecommendedCapacity = r.RecommendedCapacity,
                ReservedSeats = r.ReservedSeats,
                WaitingListCount = r.WaitingListCount,
                ParentSectionId = r.ParentSectionId,
                SectionGroupId = r.SectionGroupId,
                OccupancyPercent = cap?.OccupancyPercent,
                CapacityStatus = cap?.CapacityStatus,
            };
        }).ToList();
    }

    private async Task<IReadOnlyList<StudentSectionDto>> MapStudentSectionsAsync(IReadOnlyList<StudentSection> rows, CancellationToken ct)
    {
        if (rows.Count == 0) return [];
        var studentIds = rows.Select(r => r.StudentId).Distinct().ToList();
        var sectionIds = rows.Select(r => r.SectionId).Distinct().ToList();
        var students = await _db.Students.AsNoTracking().Where(s => studentIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, s => new { s.StudentNumber, s.Name }, ct);
        var sections = await _db.Sections.AsNoTracking().Where(s => sectionIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, s => new { s.SectionCode, s.SectionName }, ct);

        return rows.Select(r =>
        {
            students.TryGetValue(r.StudentId, out var st);
            sections.TryGetValue(r.SectionId, out var sec);
            return new StudentSectionDto
            {
                Id = r.Id,
                StudentId = r.StudentId,
                StudentNumber = st?.StudentNumber,
                StudentName = st?.Name,
                SectionId = r.SectionId,
                SectionCode = sec?.SectionCode,
                SectionName = sec?.SectionName,
                EffectiveFrom = r.EffectiveFrom,
                EffectiveTo = r.EffectiveTo,
                IsCurrent = r.IsCurrent,
                TransferReason = r.TransferReason,
            };
        }).ToList();
    }

    private async Task<IReadOnlyList<FacultySectionDto>> MapFacultySectionsAsync(IReadOnlyList<FacultySectionAssignment> rows, CancellationToken ct)
    {
        if (rows.Count == 0) return [];
        var facultyIds = rows.Select(r => r.FacultyId).Distinct().ToList();
        var sectionIds = rows.Select(r => r.SectionId).Distinct().ToList();
        var faculty = await _db.StaffMembers.AsNoTracking().Where(s => facultyIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, s => $"{s.FirstName} {s.LastName}".Trim(), ct);
        var sections = await _db.Sections.AsNoTracking().Where(s => sectionIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, s => new { s.SectionCode, s.SectionName }, ct);

        return rows.Select(r =>
        {
            sections.TryGetValue(r.SectionId, out var sec);
            return new FacultySectionDto
            {
                Id = r.Id,
                FacultyId = r.FacultyId,
                FacultyName = faculty.GetValueOrDefault(r.FacultyId),
                SectionId = r.SectionId,
                SectionCode = sec?.SectionCode,
                SectionName = sec?.SectionName,
                AcademicYearId = r.AcademicYearId,
                Role = r.Role,
                EffectiveFrom = r.EffectiveFrom,
                EffectiveTo = r.EffectiveTo,
                IsCurrent = r.IsCurrent,
            };
        }).ToList();
    }

    private async Task<IReadOnlyList<TimetableSectionDto>> MapTimetableSectionsAsync(IReadOnlyList<TimetableSection> rows, CancellationToken ct)
    {
        if (rows.Count == 0) return [];
        var sectionIds = rows.Select(r => r.SectionId).Distinct().ToList();
        var sections = await _db.Sections.AsNoTracking().Where(s => sectionIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, s => new { s.SectionCode, s.SectionName }, ct);
        return rows.Select(r =>
        {
            sections.TryGetValue(r.SectionId, out var sec);
            return new TimetableSectionDto
            {
                Id = r.Id,
                TimetableId = r.TimetableId,
                TimetableEntryId = r.TimetableEntryId,
                SectionId = r.SectionId,
                SectionCode = sec?.SectionCode,
                SectionName = sec?.SectionName,
            };
        }).ToList();
    }
}
