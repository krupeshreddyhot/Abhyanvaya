using Abhyanvaya.Application.Academic;
using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.Common.Interfaces.Scheduling;
using Abhyanvaya.Application.DTOs.Scheduling;
using Abhyanvaya.Application.Internal;
using Abhyanvaya.Domain.Entities.Scheduling;
using Abhyanvaya.Domain.Enums.Scheduling;
using Abhyanvaya.Domain.Exceptions;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Abhyanvaya.Application.Scheduling;

public sealed class TimetableService : ITimetableService
{
    private static readonly string[] DayNames = ["Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday"];

    private readonly ITimetableRepository _repository;
    private readonly ISubjectAllocationRepository _allocationRepository;
    private readonly ITimeSlotRepository _timeSlotRepository;
    private readonly IApplicationDbContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;
    private readonly IValidator<CreateTimetableRequest> _createTimetableValidator;
    private readonly IValidator<UpdateTimetableRequest> _updateTimetableValidator;
    private readonly IValidator<CreateTimetableEntryRequest> _createEntryValidator;
    private readonly IValidator<UpdateTimetableEntryRequest> _updateEntryValidator;
    private readonly IValidator<BulkPasteEntriesRequest> _bulkValidator;
    private readonly IValidator<MoveTimetableEntryRequest> _moveValidator;
    private readonly IValidator<CopyTimetableEntryRequest> _copyValidator;
    private readonly ITimetableChangeHistoryService? _historyService;

    public TimetableService(
        ITimetableRepository repository,
        ISubjectAllocationRepository allocationRepository,
        ITimeSlotRepository timeSlotRepository,
        IApplicationDbContext context,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUser,
        IValidator<CreateTimetableRequest> createTimetableValidator,
        IValidator<UpdateTimetableRequest> updateTimetableValidator,
        IValidator<CreateTimetableEntryRequest> createEntryValidator,
        IValidator<UpdateTimetableEntryRequest> updateEntryValidator,
        IValidator<BulkPasteEntriesRequest> bulkValidator,
        IValidator<MoveTimetableEntryRequest> moveValidator,
        IValidator<CopyTimetableEntryRequest> copyValidator,
        ITimetableChangeHistoryService? historyService = null)
    {
        _repository = repository;
        _allocationRepository = allocationRepository;
        _timeSlotRepository = timeSlotRepository;
        _context = context;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _createTimetableValidator = createTimetableValidator;
        _updateTimetableValidator = updateTimetableValidator;
        _createEntryValidator = createEntryValidator;
        _updateEntryValidator = updateEntryValidator;
        _bulkValidator = bulkValidator;
        _moveValidator = moveValidator;
        _copyValidator = copyValidator;
        _historyService = historyService;
    }

    private int TenantId => _currentUser.TenantId;

    public async Task<IReadOnlyList<TimetableDto>> ListTimetablesAsync(int? academicYearId, TimetableStatus? status, int? departmentId, bool includeArchived = false, CancellationToken cancellationToken = default)
    {
        var items = await _repository.ListAsync(TenantId, academicYearId, status, departmentId, includeArchived, cancellationToken);
        var dtos = new List<TimetableDto>();
        foreach (var item in items)
            dtos.Add(await MapTimetableAsync(item, cancellationToken));
        return dtos;
    }

    public async Task<TimetableDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetByIdAsync(TenantId, id, cancellationToken);
        return entity is null ? null : await MapTimetableAsync(entity, cancellationToken);
    }

    public async Task<TimetableGridDto?> GetGridAsync(int timetableId, CancellationToken cancellationToken = default)
    {
        var timetable = await _repository.GetByIdAsync(TenantId, timetableId, cancellationToken);
        if (timetable is null) return null;

        var entries = await _repository.ListEntriesAsync(TenantId, timetableId, cancellationToken);
        var timeSlots = timetable.TimeSlotSetId.HasValue
            ? await _timeSlotRepository.ListSlotsAsync(TenantId, timetable.TimeSlotSetId.Value, cancellationToken)
            : Array.Empty<TimeSlot>();

        return new TimetableGridDto
        {
            Timetable = await MapTimetableAsync(timetable, cancellationToken),
            Entries = await MapEntriesAsync(entries, cancellationToken),
            TimeSlots = timeSlots.Select(MapTimeSlot).ToList()
        };
    }

    public async Task<TimetableProjectionDto?> GetFacultyProjectionAsync(int timetableId, int staffId, CancellationToken cancellationToken = default)
        => await GetProjectionAsync(timetableId, () => _repository.ListEntriesByStaffAsync(TenantId, timetableId, staffId, cancellationToken), cancellationToken);

    public async Task<TimetableProjectionDto?> GetStudentProjectionAsync(int timetableId, int courseId, int groupId, int semesterId, CancellationToken cancellationToken = default)
        => await GetProjectionAsync(timetableId, () => _repository.ListEntriesByStudentAsync(TenantId, timetableId, courseId, groupId, semesterId, cancellationToken), cancellationToken);

    public async Task<TimetableProjectionDto?> GetRoomProjectionAsync(int timetableId, int roomId, CancellationToken cancellationToken = default)
        => await GetProjectionAsync(timetableId, () => _repository.ListEntriesByRoomAsync(TenantId, timetableId, roomId, cancellationToken), cancellationToken);

    public async Task<TimetableProjectionDto?> GetDepartmentProjectionAsync(int timetableId, int departmentId, CancellationToken cancellationToken = default)
        => await GetProjectionAsync(timetableId, () => _repository.ListEntriesByDepartmentAsync(TenantId, timetableId, departmentId, cancellationToken), cancellationToken);

    public async Task<TimetableDashboardDto> GetDashboardAsync(int? academicYearId, CancellationToken cancellationToken = default)
    {
        var timetables = _context.SchedulingTimetables.Where(x => x.TenantId == TenantId);
        if (academicYearId.HasValue)
            timetables = timetables.Where(x => x.AcademicYearId == academicYearId.Value);

        var timetableIds = timetables.Select(x => x.Id);
        var entries = _context.SchedulingTimetableEntries.Where(x => x.TenantId == TenantId && timetableIds.Contains(x.TimetableId));

        var draftCount = await timetables.CountAsync(x => x.Status == TimetableStatus.Draft, cancellationToken);
        var lockedCount = await timetables.CountAsync(x => x.Status == TimetableStatus.Locked, cancellationToken);
        var scheduledPeriodCount = await entries.CountAsync(cancellationToken);
        var departmentsWithTimetable = await timetables.Where(x => x.DepartmentId.HasValue).Select(x => x.DepartmentId!.Value).Distinct().CountAsync(cancellationToken);
        var facultyScheduledCount = await entries.Select(x => x.StaffId).Distinct().CountAsync(cancellationToken);
        var roomsScheduledCount = await entries.Select(x => x.RoomId).Distinct().CountAsync(cancellationToken);

        var dailyDistribution = await entries
            .GroupBy(x => x.DayOfWeek)
            .Select(g => new NamedCountDto { Name = DayNames[g.Key], Count = g.Count() })
            .OrderBy(x => Array.IndexOf(DayNames, x.Name))
            .ToListAsync(cancellationToken);

        var facultyLoad = await entries
            .GroupBy(x => x.StaffId)
            .Select(g => new { StaffId = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Take(10)
            .ToListAsync(cancellationToken);

        var roomUsage = await entries
            .GroupBy(x => x.RoomId)
            .Select(g => new { RoomId = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Take(10)
            .ToListAsync(cancellationToken);

        var staffNames = await _context.StaffMembers
            .Where(s => facultyLoad.Select(f => f.StaffId).Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, s => $"{s.FirstName} {s.LastName}".Trim(), cancellationToken);

        var roomNames = await _context.SchedulingRooms
            .Where(r => roomUsage.Select(u => u.RoomId).Contains(r.Id))
            .ToDictionaryAsync(r => r.Id, r => r.Name, cancellationToken);

        return new TimetableDashboardDto
        {
            DraftTimetableCount = draftCount,
            LockedCount = lockedCount,
            ScheduledPeriodCount = scheduledPeriodCount,
            DepartmentsWithTimetable = departmentsWithTimetable,
            FacultyScheduledCount = facultyScheduledCount,
            RoomsScheduledCount = roomsScheduledCount,
            DailyDistribution = dailyDistribution,
            FacultyLoad = facultyLoad.Select(f => new NamedCountDto
            {
                Name = staffNames.GetValueOrDefault(f.StaffId, $"Staff #{f.StaffId}"),
                Count = f.Count
            }).ToList(),
            RoomUsage = roomUsage.Select(r => new NamedCountDto
            {
                Name = roomNames.GetValueOrDefault(r.RoomId, $"Room #{r.RoomId}"),
                Count = r.Count
            }).ToList()
        };
    }

    public async Task<TimetableDto> CreateTimetableAsync(CreateTimetableRequest request, CancellationToken cancellationToken = default)
    {
        await _createTimetableValidator.ValidateAndThrowAsync(request, cancellationToken);
        await ValidateTimetableRefsAsync(request.AcademicYearId, request.DepartmentId, request.TimeSlotSetId, cancellationToken);
        if (!string.IsNullOrWhiteSpace(request.Code) && await _repository.CodeExistsAsync(TenantId, request.AcademicYearId, request.Code, null, cancellationToken))
            throw new DomainException($"Timetable code '{request.Code}' already exists for this academic year.");

        var entity = new Timetable
        {
            Name = request.Name.Trim(),
            Code = string.IsNullOrWhiteSpace(request.Code) ? null : request.Code.Trim(),
            AcademicYearId = request.AcademicYearId,
            DepartmentId = request.DepartmentId,
            TimeSlotSetId = request.TimeSlotSetId,
            Notes = request.Notes?.Trim(),
            Status = TimetableStatus.Draft
        };

        await _repository.AddAsync(entity, cancellationToken);
        await ConcurrencyExceptionHelper.SaveChangesAsync(_unitOfWork, cancellationToken);
        await RecordHistoryAsync(entity.Id, TimetableChangeOperation.Create, null, null, new { entity.Name, entity.Status }, null, cancellationToken);
        return await MapTimetableAsync(entity, cancellationToken);
    }

    public async Task<TimetableDto> UpdateTimetableAsync(UpdateTimetableRequest request, CancellationToken cancellationToken = default)
    {
        await _updateTimetableValidator.ValidateAndThrowAsync(request, cancellationToken);
        var entity = await RequireTimetableAsync(request.Id, cancellationToken);
        EnsureDraft(entity);

        await ValidateTimetableRefsAsync(request.AcademicYearId, request.DepartmentId, request.TimeSlotSetId, cancellationToken);
        if (!string.IsNullOrWhiteSpace(request.Code) && await _repository.CodeExistsAsync(TenantId, request.AcademicYearId, request.Code, request.Id, cancellationToken))
            throw new DomainException($"Timetable code '{request.Code}' already exists for this academic year.");

        entity.Name = request.Name.Trim();
        entity.Code = string.IsNullOrWhiteSpace(request.Code) ? null : request.Code.Trim();
        entity.AcademicYearId = request.AcademicYearId;
        entity.DepartmentId = request.DepartmentId;
        entity.TimeSlotSetId = request.TimeSlotSetId;
        entity.Notes = request.Notes?.Trim();

        await ConcurrencyExceptionHelper.SaveChangesAsync(_unitOfWork, cancellationToken);
        await RecordHistoryAsync(entity.Id, TimetableChangeOperation.Update, null, null, new { entity.Name, entity.Status }, null, cancellationToken);
        return await MapTimetableAsync(entity, cancellationToken);
    }

    public async Task DeleteTimetableAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await RequireTimetableAsync(id, cancellationToken);
        EnsureDraft(entity);
        entity.IsDeleted = true;
        await ConcurrencyExceptionHelper.SaveChangesAsync(_unitOfWork, cancellationToken);
        await RecordHistoryAsync(entity.Id, TimetableChangeOperation.Delete, null, new { entity.Name }, null, null, cancellationToken);
    }

    public async Task<TimetableDto> LockAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await RequireTimetableAsync(id, cancellationToken);
        if (entity.Status != TimetableStatus.Draft)
            throw new DomainException("Only draft timetables can be locked.");
        entity.Status = TimetableStatus.Locked;
        await ConcurrencyExceptionHelper.SaveChangesAsync(_unitOfWork, cancellationToken);
        await RecordHistoryAsync(entity.Id, TimetableChangeOperation.Lock, null, new { Status = TimetableStatus.Draft }, new { Status = entity.Status }, null, cancellationToken);
        return await MapTimetableAsync(entity, cancellationToken);
    }

    public async Task<TimetableDto> UnlockAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await RequireTimetableAsync(id, cancellationToken);
        if (entity.Status != TimetableStatus.Locked)
            throw new DomainException("Only locked timetables can be unlocked.");
        entity.Status = TimetableStatus.Draft;
        await ConcurrencyExceptionHelper.SaveChangesAsync(_unitOfWork, cancellationToken);
        await RecordHistoryAsync(entity.Id, TimetableChangeOperation.Unlock, null, new { Status = TimetableStatus.Locked }, new { Status = entity.Status }, null, cancellationToken);
        return await MapTimetableAsync(entity, cancellationToken);
    }

    public async Task<TimetableEntryDto> CreateEntryAsync(int timetableId, CreateTimetableEntryRequest request, CancellationToken cancellationToken = default)
    {
        await _createEntryValidator.ValidateAndThrowAsync(request, cancellationToken);
        var timetable = await RequireTimetableAsync(timetableId, cancellationToken);
        EnsureDraft(timetable);

        var allocation = await RequireAllocationAsync(request.SubjectAllocationId, cancellationToken);
        await EnsureTimeSlotInSetAsync(timetable.TimeSlotSetId, request.TimeSlotId, cancellationToken);

        var roomId = await ResolveRoomIdAsync(request.RoomId, allocation, cancellationToken);
        var courseDepartmentId = await ResolveCourseDepartmentIdAsync(allocation.CourseId, cancellationToken);
        var entry = new TimetableEntry { TimetableId = timetableId };
        ApplyAllocationDenormalization(entry, allocation, roomId, courseDepartmentId);
        entry.DayOfWeek = request.DayOfWeek;
        entry.TimeSlotId = request.TimeSlotId;
        entry.Remarks = request.Remarks?.Trim();

        await EnsureProposedTeachingGroupCompatibleAsync(entry, cancellationToken);

        await _repository.AddEntryAsync(entry, cancellationToken);
        await ConcurrencyExceptionHelper.SaveChangesAsync(_unitOfWork, cancellationToken);
        await RecordHistoryAsync(timetableId, TimetableChangeOperation.Create, entry.Id, null, new { entry.DayOfWeek, entry.TimeSlotId, entry.StaffId, entry.RoomId }, null, cancellationToken);
        return (await MapEntriesAsync([entry], cancellationToken)).Single();
    }

    public async Task<TimetableEntryDto> UpdateEntryAsync(int entryId, UpdateTimetableEntryRequest request, CancellationToken cancellationToken = default)
    {
        if (entryId != request.Id) throw new DomainException("Entry id mismatch.");
        await _updateEntryValidator.ValidateAndThrowAsync(request, cancellationToken);

        var entry = await RequireEntryAsync(entryId, cancellationToken);
        var timetable = await RequireTimetableAsync(entry.TimetableId, cancellationToken);
        EnsureDraft(timetable);

        var allocation = await RequireAllocationAsync(request.SubjectAllocationId, cancellationToken);
        await EnsureTimeSlotInSetAsync(timetable.TimeSlotSetId, request.TimeSlotId, cancellationToken);

        var roomId = await ResolveRoomIdAsync(request.RoomId, allocation, cancellationToken);
        var courseDepartmentId = await ResolveCourseDepartmentIdAsync(allocation.CourseId, cancellationToken);
        ApplyAllocationDenormalization(entry, allocation, roomId, courseDepartmentId);
        entry.DayOfWeek = request.DayOfWeek;
        entry.TimeSlotId = request.TimeSlotId;
        entry.Remarks = request.Remarks?.Trim();

        // AI-SCHED-TG.4 Prompt 4 — proposed state must remain TG-compatible (never silent clear/replace).
        await EnsureProposedTeachingGroupCompatibleAsync(entry, cancellationToken);

        await ConcurrencyExceptionHelper.SaveChangesAsync(_unitOfWork, cancellationToken);
        await RecordHistoryAsync(timetable.Id, TimetableChangeOperation.Update, entry.Id, null, new { entry.DayOfWeek, entry.TimeSlotId, entry.StaffId, entry.RoomId }, null, cancellationToken);
        return (await MapEntriesAsync([entry], cancellationToken)).Single();
    }

    public async Task DeleteEntryAsync(int entryId, CancellationToken cancellationToken = default)
    {
        var entry = await RequireEntryAsync(entryId, cancellationToken);
        var timetable = await RequireTimetableAsync(entry.TimetableId, cancellationToken);
        EnsureDraft(timetable);
        entry.IsDeleted = true;
        await ConcurrencyExceptionHelper.SaveChangesAsync(_unitOfWork, cancellationToken);
        await RecordHistoryAsync(timetable.Id, TimetableChangeOperation.Delete, entry.Id, new { entry.DayOfWeek, entry.TimeSlotId }, null, null, cancellationToken);
    }

    public async Task<TimetableEntryDto> MoveEntryAsync(int entryId, MoveTimetableEntryRequest request, CancellationToken cancellationToken = default)
    {
        await _moveValidator.ValidateAndThrowAsync(request, cancellationToken);
        var entry = await RequireEntryAsync(entryId, cancellationToken);
        var timetable = await RequireTimetableAsync(entry.TimetableId, cancellationToken);
        EnsureDraft(timetable);
        await EnsureTimeSlotInSetAsync(timetable.TimeSlotSetId, request.TimeSlotId, cancellationToken);

        entry.DayOfWeek = request.DayOfWeek;
        entry.TimeSlotId = request.TimeSlotId;
        if (request.RoomId.HasValue)
        {
            await EnsureRoomExistsAsync(request.RoomId.Value, cancellationToken);
            entry.RoomId = request.RoomId.Value;
        }

        await EnsureProposedTeachingGroupCompatibleAsync(entry, cancellationToken);

        await ConcurrencyExceptionHelper.SaveChangesAsync(_unitOfWork, cancellationToken);
        await RecordHistoryAsync(timetable.Id, TimetableChangeOperation.Move, entry.Id, null, new { entry.DayOfWeek, entry.TimeSlotId, entry.RoomId }, null, cancellationToken);
        return (await MapEntriesAsync([entry], cancellationToken)).Single();
    }

    public async Task<TimetableEntryDto> CopyEntryAsync(int entryId, CopyTimetableEntryRequest request, CancellationToken cancellationToken = default)
    {
        await _copyValidator.ValidateAndThrowAsync(request, cancellationToken);
        var source = await RequireEntryAsync(entryId, cancellationToken);
        var timetable = await RequireTimetableAsync(source.TimetableId, cancellationToken);
        EnsureDraft(timetable);
        await EnsureTimeSlotInSetAsync(timetable.TimeSlotSetId, request.TargetTimeSlotId, cancellationToken);

        var roomId = request.RoomId ?? source.RoomId;
        if (request.RoomId.HasValue)
            await EnsureRoomExistsAsync(request.RoomId.Value, cancellationToken);

        var copy = CloneEntry(source, timetable.Id);
        copy.DayOfWeek = request.TargetDayOfWeek;
        copy.TimeSlotId = request.TargetTimeSlotId;
        copy.RoomId = roomId;
        await RealignDepartmentFromCourseAsync(copy, cancellationToken);

        await EnsureProposedTeachingGroupCompatibleAsync(copy, cancellationToken);

        await _repository.AddEntryAsync(copy, cancellationToken);
        await ConcurrencyExceptionHelper.SaveChangesAsync(_unitOfWork, cancellationToken);
        await RecordHistoryAsync(timetable.Id, TimetableChangeOperation.Copy, copy.Id, new { SourceEntryId = source.Id }, new { copy.DayOfWeek, copy.TimeSlotId }, null, cancellationToken);
        return (await MapEntriesAsync([copy], cancellationToken)).Single();
    }

    public async Task<TimetableEntryDto> DuplicateEntryAsync(int entryId, CancellationToken cancellationToken = default)
    {
        var source = await RequireEntryAsync(entryId, cancellationToken);
        var timetable = await RequireTimetableAsync(source.TimetableId, cancellationToken);
        EnsureDraft(timetable);

        var duplicate = CloneEntry(source, timetable.Id);
        await RealignDepartmentFromCourseAsync(duplicate, cancellationToken);
        await EnsureProposedTeachingGroupCompatibleAsync(duplicate, cancellationToken);
        await _repository.AddEntryAsync(duplicate, cancellationToken);
        await ConcurrencyExceptionHelper.SaveChangesAsync(_unitOfWork, cancellationToken);
        await RecordHistoryAsync(timetable.Id, TimetableChangeOperation.Copy, duplicate.Id, new { SourceEntryId = source.Id }, new { duplicate.DayOfWeek, duplicate.TimeSlotId }, null, cancellationToken);
        return (await MapEntriesAsync([duplicate], cancellationToken)).Single();
    }

    public async Task<IReadOnlyList<TimetableEntryDto>> BulkUpsertEntriesAsync(int timetableId, BulkPasteEntriesRequest request, CancellationToken cancellationToken = default)
    {
        await _bulkValidator.ValidateAndThrowAsync(request, cancellationToken);
        var timetable = await RequireTimetableAsync(timetableId, cancellationToken);
        EnsureDraft(timetable);

        var results = new List<TimetableEntry>();
        foreach (var item in request.Entries)
        {
            var allocation = await RequireAllocationAsync(item.SubjectAllocationId, cancellationToken);
            await EnsureTimeSlotInSetAsync(timetable.TimeSlotSetId, item.TimeSlotId, cancellationToken);
            var roomId = await ResolveRoomIdAsync(item.RoomId, allocation, cancellationToken);

            var courseDepartmentId = await ResolveCourseDepartmentIdAsync(allocation.CourseId, cancellationToken);
            if (item.Id.HasValue)
            {
                var existing = await RequireEntryAsync(item.Id.Value, cancellationToken);
                if (existing.TimetableId != timetableId)
                    throw new DomainException("Entry does not belong to this timetable.");
                ApplyAllocationDenormalization(existing, allocation, roomId, courseDepartmentId);
                existing.DayOfWeek = item.DayOfWeek;
                existing.TimeSlotId = item.TimeSlotId;
                existing.Remarks = item.Remarks?.Trim();
                await EnsureProposedTeachingGroupCompatibleAsync(existing, cancellationToken);
                results.Add(existing);
            }
            else
            {
                var entry = new TimetableEntry { TimetableId = timetableId };
                ApplyAllocationDenormalization(entry, allocation, roomId, courseDepartmentId);
                entry.DayOfWeek = item.DayOfWeek;
                entry.TimeSlotId = item.TimeSlotId;
                entry.Remarks = item.Remarks?.Trim();
                await EnsureProposedTeachingGroupCompatibleAsync(entry, cancellationToken);
                await _repository.AddEntryAsync(entry, cancellationToken);
                results.Add(entry);
            }
        }

        await ConcurrencyExceptionHelper.SaveChangesAsync(_unitOfWork, cancellationToken);
        return await MapEntriesAsync(results, cancellationToken);
    }

    public static void EnsureDraft(Timetable timetable)
    {
        if (timetable.IsFrozen)
            throw new DomainException("Frozen timetables are read-only. An Academic Admin must unlock before editing.");
        if (timetable.Status is TimetableStatus.Published or TimetableStatus.Archived)
            throw new DomainException("Published or archived timetables are read-only.");
        if (timetable.Status != TimetableStatus.Draft)
            throw new DomainException("Timetable must be in Draft status to modify entries or metadata.");
    }

    public static void EnsureCloneable(Timetable timetable)
    {
        if (timetable.Status == TimetableStatus.Archived)
            throw new DomainException("Archived timetables cannot be cloned.");
    }

    /// <summary>
    /// AI-SCHED-CATALOG/TIMETABLE P1-3 Prompt 4 —
    /// Copies scheduling denorm from SubjectAllocation; DepartmentId must be Course.DepartmentId (Catalog SSOT).
    /// </summary>
    public static void ApplyAllocationDenormalization(
        TimetableEntry entry,
        SubjectAllocation allocation,
        int roomId,
        int courseDepartmentId,
        int? requestedEntryDepartmentId = null)
    {
        var decision = TimetableEntryCourseDepartmentRules.Evaluate(
            allocation.DepartmentId,
            courseDepartmentId,
            courseFound: courseDepartmentId > 0,
            requestedEntryDepartmentId);

        if (!decision.Accepted)
            throw new DomainException(decision.Error ?? "Invalid Course Department for TimetableEntry.");

        entry.SubjectAllocationId = allocation.Id;
        entry.StaffId = allocation.StaffId;
        entry.SubjectId = allocation.SubjectId;
        entry.CourseId = allocation.CourseId;
        entry.GroupId = allocation.GroupId;
        entry.SemesterId = allocation.SemesterId;
        entry.DepartmentId = decision.AlignedDepartmentId;
        entry.RoomId = roomId;
    }

    /// <summary>
    /// Re-derives TimetableEntry.DepartmentId from Course via SubjectAllocation (clone/copy/version paths).
    /// </summary>
    public static async Task RealignDepartmentFromCourseAsync(
        IApplicationDbContext db,
        int tenantId,
        TimetableEntry entry,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(entry);

        var allocation = await db.SchedulingSubjectAllocations.AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.Id == entry.SubjectAllocationId && x.TenantId == tenantId,
                cancellationToken)
            ?? throw new KeyNotFoundException($"Subject allocation {entry.SubjectAllocationId} not found.");

        var courseDepartmentId = await db.Courses.AsNoTracking()
            .Where(c => c.Id == allocation.CourseId && c.TenantId == tenantId)
            .Select(c => (int?)c.DepartmentId)
            .FirstOrDefaultAsync(cancellationToken);

        var decision = TimetableEntryCourseDepartmentRules.Evaluate(
            allocation.DepartmentId,
            courseDepartmentId,
            courseFound: courseDepartmentId is > 0);

        if (!decision.Accepted)
            throw new DomainException(decision.Error ?? "Invalid Course Department for TimetableEntry.");

        entry.DepartmentId = decision.AlignedDepartmentId;
        entry.CourseId = allocation.CourseId;
    }

    private async Task RealignDepartmentFromCourseAsync(TimetableEntry entry, CancellationToken cancellationToken)
        => await RealignDepartmentFromCourseAsync(_context, TenantId, entry, cancellationToken);

    private async Task<int> ResolveCourseDepartmentIdAsync(int courseId, CancellationToken cancellationToken)
    {
        var courseDepartmentId = await _context.Courses.AsNoTracking()
            .Where(c => c.Id == courseId && c.TenantId == TenantId)
            .Select(c => (int?)c.DepartmentId)
            .FirstOrDefaultAsync(cancellationToken);

        if (courseDepartmentId is null or <= 0)
            throw new DomainException("Course not found.");

        return courseDepartmentId.Value;
    }

    /// <summary>
    /// AI-SCHED-TG.4 Prompt 4 — Persist only if TeachingGroupId is null or compatible with the proposed entry state.
    /// Never clears, replaces, or infers a TeachingGroup.
    /// </summary>
    public async Task EnsureProposedTeachingGroupCompatibleAsync(
        TimetableEntry entry,
        CancellationToken cancellationToken = default)
        => await EnsureProposedTeachingGroupCompatibleAsync(_context, entry, cancellationToken);

    /// <summary>
    /// Shared invariant for TimetableService, clone, and schedule-version entry materialization.
    /// </summary>
    public static async Task EnsureProposedTeachingGroupCompatibleAsync(
        IApplicationDbContext db,
        TimetableEntry entry,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(entry);

        if (entry.TeachingGroupId is not int teachingGroupId)
            return;

        var teachingGroup = await db.SchedulingTeachingGroups
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == teachingGroupId, cancellationToken);

        if (teachingGroup is null)
            throw new DomainException(TeachingGroupRules.TimetableEntryTeachingGroupIncompatibleMessage);

        TeachingGroupRules.EnsureCompatibleWithTimetableEntry(teachingGroup, entry);
    }

    internal static TimetableEntry CloneEntry(TimetableEntry source, int timetableId) => new()
    {
        TenantId = source.TenantId,
        TimetableId = timetableId,
        DayOfWeek = source.DayOfWeek,
        TimeSlotId = source.TimeSlotId,
        SubjectAllocationId = source.SubjectAllocationId,
        TeachingGroupId = source.TeachingGroupId,
        StaffId = source.StaffId,
        RoomId = source.RoomId,
        DepartmentId = source.DepartmentId,
        CourseId = source.CourseId,
        GroupId = source.GroupId,
        SemesterId = source.SemesterId,
        SubjectId = source.SubjectId,
        Remarks = source.Remarks
    };

    private async Task<TimetableProjectionDto?> GetProjectionAsync(int timetableId, Func<Task<IReadOnlyList<TimetableEntry>>> loadEntries, CancellationToken cancellationToken)
    {
        var timetable = await _repository.GetByIdAsync(TenantId, timetableId, cancellationToken);
        if (timetable is null) return null;
        var entries = await loadEntries();
        return new TimetableProjectionDto
        {
            Timetable = await MapTimetableAsync(timetable, cancellationToken),
            Entries = await MapEntriesAsync(entries, cancellationToken)
        };
    }

    private async Task<Timetable> RequireTimetableAsync(int id, CancellationToken cancellationToken)
    {
        var entity = await _repository.GetByIdAsync(TenantId, id, cancellationToken);
        return entity ?? throw new KeyNotFoundException($"Timetable {id} not found.");
    }

    private async Task<TimetableEntry> RequireEntryAsync(int entryId, CancellationToken cancellationToken)
    {
        var entity = await _repository.GetEntryByIdAsync(TenantId, entryId, cancellationToken);
        return entity ?? throw new KeyNotFoundException($"Timetable entry {entryId} not found.");
    }

    private async Task<SubjectAllocation> RequireAllocationAsync(int allocationId, CancellationToken cancellationToken)
    {
        var allocation = await _allocationRepository.GetByIdAsync(TenantId, allocationId, cancellationToken)
            ?? throw new KeyNotFoundException($"Subject allocation {allocationId} not found.");

        var historical = await _context.Semesters.AsNoTracking()
            .Where(s => s.Id == allocation.SemesterId && s.TenantId == TenantId)
            .Select(s => (bool?)s.IsHistoricalArchive)
            .FirstOrDefaultAsync(cancellationToken);
        if (historical is true)
            throw new DomainException(OperationalSemesterRules.HistoricalRejectedMessage);

        return allocation;
    }

    private async Task<int> ResolveRoomIdAsync(int? requestedRoomId, SubjectAllocation allocation, CancellationToken cancellationToken)
    {
        var roomId = requestedRoomId ?? allocation.PreferredRoomId
            ?? throw new DomainException("Room is required when subject allocation has no preferred room.");
        await EnsureRoomExistsAsync(roomId, cancellationToken);
        return roomId;
    }

    private async Task EnsureRoomExistsAsync(int roomId, CancellationToken cancellationToken)
    {
        if (!await _context.SchedulingRooms.AnyAsync(x => x.TenantId == TenantId && x.Id == roomId, cancellationToken))
            throw new KeyNotFoundException($"Room {roomId} not found.");
    }

    private async Task EnsureTimeSlotInSetAsync(int? timeSlotSetId, int timeSlotId, CancellationToken cancellationToken)
    {
        var slot = await _timeSlotRepository.GetSlotByIdAsync(TenantId, timeSlotId, cancellationToken)
            ?? throw new KeyNotFoundException($"Time slot {timeSlotId} not found.");
        if (timeSlotSetId.HasValue && slot.TimeSlotSetId != timeSlotSetId.Value)
            throw new DomainException("Time slot does not belong to the timetable's time slot set.");
    }

    private async Task ValidateTimetableRefsAsync(int academicYearId, int? departmentId, int? timeSlotSetId, CancellationToken cancellationToken)
    {
        if (!await _context.SchedulingAcademicYears.AnyAsync(x => x.TenantId == TenantId && x.Id == academicYearId, cancellationToken))
            throw new KeyNotFoundException($"Academic year {academicYearId} not found.");
        if (departmentId.HasValue && !await _context.Departments.AnyAsync(x => x.TenantId == TenantId && x.Id == departmentId.Value, cancellationToken))
            throw new KeyNotFoundException($"Department {departmentId} not found.");
        if (timeSlotSetId.HasValue && await _timeSlotRepository.GetSetByIdAsync(TenantId, timeSlotSetId.Value, cancellationToken) is null)
            throw new KeyNotFoundException($"Time slot set {timeSlotSetId} not found.");
    }

    private async Task<TimetableDto> MapTimetableAsync(Timetable entity, CancellationToken cancellationToken)
    {
        var entryCount = await _repository.CountEntriesAsync(TenantId, entity.Id, cancellationToken);
        string? academicYearName = null;
        string? departmentName = null;
        string? timeSlotSetName = null;

        if (entity.AcademicYearId > 0)
            academicYearName = await _context.SchedulingAcademicYears.Where(x => x.Id == entity.AcademicYearId).Select(x => x.Name).FirstOrDefaultAsync(cancellationToken);
        if (entity.DepartmentId.HasValue)
            departmentName = await _context.Departments.Where(x => x.Id == entity.DepartmentId.Value).Select(x => x.Name).FirstOrDefaultAsync(cancellationToken);
        if (entity.TimeSlotSetId.HasValue)
            timeSlotSetName = await _context.SchedulingTimeSlotSets.Where(x => x.Id == entity.TimeSlotSetId.Value).Select(x => x.Name).FirstOrDefaultAsync(cancellationToken);

        string? archiveReasonName = entity.ArchiveReasonId.HasValue
            ? await _context.SchedulingArchiveReasons.Where(x => x.Id == entity.ArchiveReasonId.Value).Select(x => x.Name).FirstOrDefaultAsync(cancellationToken)
            : null;

        return new TimetableDto
        {
            Id = entity.Id,
            Name = entity.Name,
            Code = entity.Code,
            AcademicYearId = entity.AcademicYearId,
            AcademicYearName = academicYearName,
            DepartmentId = entity.DepartmentId,
            DepartmentName = departmentName,
            TimeSlotSetId = entity.TimeSlotSetId,
            TimeSlotSetName = timeSlotSetName,
            ScheduleVersionId = entity.ScheduleVersionId,
            Status = entity.Status,
            Notes = entity.Notes,
            EntryCount = entryCount,
            IsFrozen = entity.IsFrozen,
            FrozenDate = entity.FrozenDate,
            FrozenBy = entity.FrozenBy,
            FreezeReason = entity.FreezeReason,
            UnlockDate = entity.UnlockDate,
            UnlockedBy = entity.UnlockedBy,
            UnlockReason = entity.UnlockReason,
            ArchiveReasonId = entity.ArchiveReasonId,
            ArchiveReasonName = archiveReasonName,
            ArchiveComments = entity.ArchiveComments,
            ArchivedBy = entity.ArchivedBy,
            ArchivedDate = entity.ArchivedDate,
            ReferenceVersionId = entity.ReferenceVersionId
        };
    }

    private async Task<IReadOnlyList<TimetableEntryDto>> MapEntriesAsync(IReadOnlyList<TimetableEntry> entries, CancellationToken cancellationToken)
    {
        if (entries.Count == 0) return [];

        var timeSlotIds = entries.Select(e => e.TimeSlotId).Distinct().ToList();
        var staffIds = entries.Select(e => e.StaffId).Distinct().ToList();
        var roomIds = entries.Select(e => e.RoomId).Distinct().ToList();
        var departmentIds = entries.Select(e => e.DepartmentId).Distinct().ToList();
        var courseIds = entries.Select(e => e.CourseId).Distinct().ToList();
        var groupIds = entries.Select(e => e.GroupId).Distinct().ToList();
        var semesterIds = entries.Select(e => e.SemesterId).Distinct().ToList();
        var subjectIds = entries.Select(e => e.SubjectId).Distinct().ToList();

        var timeSlots = await _context.SchedulingTimeSlots.Where(t => timeSlotIds.Contains(t.Id)).ToDictionaryAsync(t => t.Id, cancellationToken);
        var staffNames = await _context.StaffMembers.Where(s => staffIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id, s => $"{s.FirstName} {s.LastName}".Trim(), cancellationToken);
        var roomNames = await _context.SchedulingRooms.Where(r => roomIds.Contains(r.Id)).ToDictionaryAsync(r => r.Id, r => r.Name, cancellationToken);
        var departmentNames = await _context.Departments.Where(d => departmentIds.Contains(d.Id)).ToDictionaryAsync(d => d.Id, d => d.Name, cancellationToken);
        var courseNames = await _context.Courses.Where(c => courseIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.Name, cancellationToken);
        var groupNames = await _context.Groups.Where(g => groupIds.Contains(g.Id)).ToDictionaryAsync(g => g.Id, g => g.Name, cancellationToken);
        var semesterNames = await _context.Semesters.Where(s => semesterIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id, s => s.Name, cancellationToken);
        var subjectNames = await (
            from s in _context.Subjects
            join ts in _context.TenantSubjects on s.TenantSubjectId equals ts.Id
            where subjectIds.Contains(s.Id)
            select new { s.Id, ts.Name }).ToDictionaryAsync(x => x.Id, x => x.Name, cancellationToken);

        return entries.Select(e =>
        {
            timeSlots.TryGetValue(e.TimeSlotId, out var slot);
            return new TimetableEntryDto
            {
                Id = e.Id,
                TimetableId = e.TimetableId,
                DayOfWeek = e.DayOfWeek,
                TimeSlotId = e.TimeSlotId,
                TimeSlotName = slot?.Name,
                StartTime = slot?.StartTime,
                EndTime = slot?.EndTime,
                SubjectAllocationId = e.SubjectAllocationId,
                TeachingGroupId = e.TeachingGroupId,
                StaffId = e.StaffId,
                StaffName = staffNames.GetValueOrDefault(e.StaffId),
                RoomId = e.RoomId,
                RoomName = roomNames.GetValueOrDefault(e.RoomId),
                DepartmentId = e.DepartmentId,
                DepartmentName = departmentNames.GetValueOrDefault(e.DepartmentId),
                CourseId = e.CourseId,
                CourseName = courseNames.GetValueOrDefault(e.CourseId),
                GroupId = e.GroupId,
                GroupName = groupNames.GetValueOrDefault(e.GroupId),
                SemesterId = e.SemesterId,
                SemesterName = semesterNames.GetValueOrDefault(e.SemesterId),
                SubjectId = e.SubjectId,
                SubjectName = subjectNames.GetValueOrDefault(e.SubjectId),
                Remarks = e.Remarks
            };
        }).ToList();
    }

    private static TimeSlotDto MapTimeSlot(TimeSlot slot) => new()
    {
        Id = slot.Id,
        TimeSlotSetId = slot.TimeSlotSetId,
        PeriodNumber = slot.PeriodNumber,
        Name = slot.Name,
        StartTime = slot.StartTime,
        EndTime = slot.EndTime,
        DurationMinutes = slot.DurationMinutes,
        DayOfWeek = slot.DayOfWeek,
        SlotKind = slot.SlotKind,
        SessionKind = slot.SessionKind
    };

    private Task RecordHistoryAsync(int timetableId, TimetableChangeOperation operation, int? entryId, object? oldValue, object? newValue, string? reason, CancellationToken cancellationToken)
        => _historyService?.RecordAsync(timetableId, operation, entryId, oldValue, newValue, reason, cancellationToken) ?? Task.CompletedTask;
}
