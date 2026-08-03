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

public sealed class ScheduleVersionService : IScheduleVersionService
{
    private readonly IScheduleVersionRepository _repository;
    private readonly ITimetableRepository _timetableRepository;
    private readonly IApplicationDbContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;
    private readonly IArchiveReasonRepository _archiveReasonRepository;
    private readonly IValidator<CreateScheduleVersionRequest> _createValidator;
    private readonly IValidator<DuplicateScheduleVersionRequest> _duplicateValidator;

    public ScheduleVersionService(
        IScheduleVersionRepository repository,
        ITimetableRepository timetableRepository,
        IArchiveReasonRepository archiveReasonRepository,
        IApplicationDbContext context,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUser,
        IValidator<CreateScheduleVersionRequest> createValidator,
        IValidator<DuplicateScheduleVersionRequest> duplicateValidator)
    {
        _repository = repository;
        _timetableRepository = timetableRepository;
        _archiveReasonRepository = archiveReasonRepository;
        _context = context;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _createValidator = createValidator;
        _duplicateValidator = duplicateValidator;
    }

    private int TenantId => _currentUser.TenantId;

    public async Task<IReadOnlyList<ScheduleVersionDto>> ListAsync(int? academicYearId, int? academicTermId, ScheduleVersionStatus? status, bool includeArchived, CancellationToken cancellationToken = default)
    {
        var items = await _repository.ListAsync(TenantId, academicYearId, academicTermId, status, includeArchived, cancellationToken);
        var dtos = new List<ScheduleVersionDto>();
        foreach (var item in items)
            dtos.Add(await MapAsync(item, cancellationToken));
        return dtos;
    }

    public async Task<ScheduleVersionDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetByIdAsync(TenantId, id, cancellationToken);
        return entity is null ? null : await MapAsync(entity, cancellationToken);
    }

    public async Task<IReadOnlyList<ScheduleVersionHistoryDto>> HistoryAsync(int academicYearId, int? academicTermId, CancellationToken cancellationToken = default)
    {
        var items = await _repository.ListHistoryAsync(TenantId, academicYearId, academicTermId, cancellationToken);
        return items.Select(x => new ScheduleVersionHistoryDto
        {
            VersionId = x.Id,
            VersionName = x.VersionName,
            VersionNumber = x.VersionNumber,
            Status = x.Status,
            CreatedDate = x.CreatedDate,
            CreatedBy = x.CreatedBy,
            PublishedDate = x.PublishedDate,
            ArchivedDate = x.ArchivedDate
        }).ToList();
    }

    public async Task<ScheduleVersionDto> CreateAsync(CreateScheduleVersionRequest request, CancellationToken cancellationToken = default)
    {
        await _createValidator.ValidateAndThrowAsync(request, cancellationToken);
        await EnsureAcademicRefsAsync(request.AcademicYearId, request.AcademicTermId, cancellationToken);

        var versionNumber = await _repository.GetNextVersionNumberAsync(TenantId, request.AcademicYearId, request.AcademicTermId, cancellationToken);
        var entity = new ScheduleVersion
        {
            TenantId = TenantId,
            AcademicYearId = request.AcademicYearId,
            AcademicTermId = request.AcademicTermId,
            VersionNumber = versionNumber,
            VersionName = request.VersionName.Trim(),
            Status = ScheduleVersionStatus.Draft,
            IsCurrent = false,
            Remarks = request.Remarks?.Trim()
        };

        await _repository.AddAsync(entity, cancellationToken);
        await ConcurrencyExceptionHelper.SaveChangesAsync(_unitOfWork, cancellationToken);

        if (request.CreateEmptyTimetable)
        {
            var timetable = new Timetable
            {
                TenantId = TenantId,
                Name = (request.TimetableName ?? $"{request.VersionName.Trim()} Timetable").Trim(),
                AcademicYearId = request.AcademicYearId,
                DepartmentId = request.DepartmentId,
                TimeSlotSetId = request.TimeSlotSetId,
                ScheduleVersionId = entity.Id,
                Status = TimetableStatus.Draft
            };
            await _timetableRepository.AddAsync(timetable, cancellationToken);
            await ConcurrencyExceptionHelper.SaveChangesAsync(_unitOfWork, cancellationToken);
        }

        return await MapAsync(entity, cancellationToken);
    }

    public async Task<ScheduleVersionDto> DuplicateAsync(DuplicateScheduleVersionRequest request, CancellationToken cancellationToken = default)
    {
        await _duplicateValidator.ValidateAndThrowAsync(request, cancellationToken);
        var source = await RequireVersionAsync(request.SourceVersionId, cancellationToken);
        return await CreateDuplicateFromSourceAsync(source, request.VersionName, request.Remarks, request.CloneAllTimetables, cancellationToken);
    }

    public async Task<ScheduleVersionDto> ClonePreviousVersionAsync(int academicYearId, int? academicTermId, string versionName, CancellationToken cancellationToken = default)
    {
        var previous = await _repository.ListHistoryAsync(TenantId, academicYearId, academicTermId, cancellationToken);
        var latest = previous.OrderByDescending(x => x.VersionNumber).FirstOrDefault()
            ?? throw new DomainException("No previous schedule version exists for this academic year/term.");
        return await CreateDuplicateFromSourceAsync(latest, versionName, null, cloneAllTimetables: true, cancellationToken);
    }

    public async Task<ScheduleVersionDto> MarkCurrentAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await RequireVersionAsync(id, cancellationToken);
        if (entity.Status == ScheduleVersionStatus.Archived)
            throw new DomainException("Archived schedule versions cannot be marked current.");

        await _repository.UnsetCurrentForScopeAsync(TenantId, entity.AcademicYearId, entity.AcademicTermId, entity.Id, cancellationToken);
        entity.IsCurrent = true;
        await ConcurrencyExceptionHelper.SaveChangesAsync(_unitOfWork, cancellationToken);
        return await MapAsync(entity, cancellationToken);
    }

    public async Task<ScheduleVersionDto> ArchiveAsync(int id, ArchiveScheduleVersionRequest? request = null, CancellationToken cancellationToken = default)
    {
        var entity = await RequireVersionAsync(id, cancellationToken);
        if (entity.Status == ScheduleVersionStatus.Archived)
            throw new DomainException("Schedule version is already archived.");

        if (request is not null)
        {
            _ = await _archiveReasonRepository.GetByIdAsync(TenantId, request.ArchiveReasonId, cancellationToken)
                ?? await _archiveReasonRepository.GetByIdAsync(1, request.ArchiveReasonId, cancellationToken)
                ?? throw new DomainException("Archive reason not found.");
            entity.ArchiveReasonId = request.ArchiveReasonId;
            entity.ArchiveComments = request.Comments?.Trim();
            entity.ReferenceVersionId = request.ReferenceVersionId;
        }

        entity.Status = ScheduleVersionStatus.Archived;
        entity.ArchivedDate = DateTime.UtcNow;
        entity.ArchivedBy = _currentUser.UserId;
        if (entity.IsCurrent) entity.IsCurrent = false;
        await ConcurrencyExceptionHelper.SaveChangesAsync(_unitOfWork, cancellationToken);
        return await MapAsync(entity, cancellationToken);
    }

    private async Task<ScheduleVersionDto> CreateDuplicateFromSourceAsync(ScheduleVersion source, string versionName, string? remarks, bool cloneAllTimetables, CancellationToken cancellationToken)
    {
        var versionNumber = await _repository.GetNextVersionNumberAsync(TenantId, source.AcademicYearId, source.AcademicTermId, cancellationToken);
        var entity = new ScheduleVersion
        {
            TenantId = TenantId,
            AcademicYearId = source.AcademicYearId,
            AcademicTermId = source.AcademicTermId,
            VersionNumber = versionNumber,
            VersionName = versionName.Trim(),
            Status = ScheduleVersionStatus.Draft,
            IsCurrent = false,
            ParentVersionId = source.Id,
            Remarks = remarks?.Trim()
        };
        await _repository.AddAsync(entity, cancellationToken);
        await ConcurrencyExceptionHelper.SaveChangesAsync(_unitOfWork, cancellationToken);

        var sourceTimetables = await _context.SchedulingTimetables
            .Where(x => x.TenantId == TenantId && x.ScheduleVersionId == source.Id)
            .ToListAsync(cancellationToken);
        if (sourceTimetables.Count == 0)
        {
            sourceTimetables = await _context.SchedulingTimetables
                .Where(x => x.TenantId == TenantId && x.AcademicYearId == source.AcademicYearId && x.DepartmentId != null)
                .OrderBy(x => x.Id)
                .Take(1)
                .ToListAsync(cancellationToken);
        }

        var timetablesToClone = cloneAllTimetables ? sourceTimetables : sourceTimetables.Take(1).ToList();
        foreach (var sourceTimetable in timetablesToClone)
        {
            var clone = new Timetable
            {
                TenantId = TenantId,
                Name = $"{sourceTimetable.Name} (v{versionNumber})",
                Code = null,
                AcademicYearId = sourceTimetable.AcademicYearId,
                DepartmentId = sourceTimetable.DepartmentId,
                TimeSlotSetId = sourceTimetable.TimeSlotSetId,
                ScheduleVersionId = entity.Id,
                Status = TimetableStatus.Draft,
                Notes = sourceTimetable.Notes
            };
            await _timetableRepository.AddAsync(clone, cancellationToken);
            await ConcurrencyExceptionHelper.SaveChangesAsync(_unitOfWork, cancellationToken);

            var entries = await _timetableRepository.ListEntriesAsync(TenantId, sourceTimetable.Id, cancellationToken);
            var clonedEntries = entries.Select(e => TimetableService.CloneEntry(e, clone.Id)).ToList();
            if (clonedEntries.Count > 0)
                await _timetableRepository.AddEntriesAsync(clonedEntries, cancellationToken);
        }

        await ConcurrencyExceptionHelper.SaveChangesAsync(_unitOfWork, cancellationToken);
        return await MapAsync(entity, cancellationToken);
    }

    private async Task<ScheduleVersion> RequireVersionAsync(int id, CancellationToken cancellationToken) =>
        await _repository.GetByIdAsync(TenantId, id, cancellationToken)
        ?? throw new KeyNotFoundException($"Schedule version {id} not found.");

    private async Task EnsureAcademicRefsAsync(int academicYearId, int? academicTermId, CancellationToken cancellationToken)
    {
        if (!await _context.SchedulingAcademicYears.AnyAsync(x => x.TenantId == TenantId && x.Id == academicYearId, cancellationToken))
            throw new KeyNotFoundException($"Academic year {academicYearId} not found.");
        if (academicTermId.HasValue && !await _context.SchedulingAcademicTerms.AnyAsync(x => x.TenantId == TenantId && x.Id == academicTermId.Value, cancellationToken))
            throw new KeyNotFoundException($"Academic term {academicTermId} not found.");
    }

    private async Task<ScheduleVersionDto> MapAsync(ScheduleVersion entity, CancellationToken cancellationToken)
    {
        var timetableCount = await _context.SchedulingTimetables.CountAsync(x => x.TenantId == TenantId && x.ScheduleVersionId == entity.Id, cancellationToken);
        string? academicYearName = await _context.SchedulingAcademicYears.Where(x => x.Id == entity.AcademicYearId).Select(x => x.Name).FirstOrDefaultAsync(cancellationToken);
        string? academicTermName = entity.AcademicTermId.HasValue
            ? await _context.SchedulingAcademicTerms.Where(x => x.Id == entity.AcademicTermId.Value).Select(x => x.Name).FirstOrDefaultAsync(cancellationToken)
            : null;

        string? archiveReasonName = entity.ArchiveReasonId.HasValue
            ? await _context.SchedulingArchiveReasons.Where(x => x.Id == entity.ArchiveReasonId.Value).Select(x => x.Name).FirstOrDefaultAsync(cancellationToken)
            : null;

        return new ScheduleVersionDto
        {
            Id = entity.Id,
            AcademicYearId = entity.AcademicYearId,
            AcademicYearName = academicYearName,
            AcademicTermId = entity.AcademicTermId,
            AcademicTermName = academicTermName,
            VersionNumber = entity.VersionNumber,
            VersionName = entity.VersionName,
            Status = entity.Status,
            IsCurrent = entity.IsCurrent,
            PublishedDate = entity.PublishedDate,
            PublishedBy = entity.PublishedBy,
            ArchivedDate = entity.ArchivedDate,
            ArchivedBy = entity.ArchivedBy,
            ArchiveReasonId = entity.ArchiveReasonId,
            ArchiveReasonName = archiveReasonName,
            ArchiveComments = entity.ArchiveComments,
            ReferenceVersionId = entity.ReferenceVersionId,
            ParentVersionId = entity.ParentVersionId,
            Remarks = entity.Remarks,
            TimetableCount = timetableCount
        };
    }
}
