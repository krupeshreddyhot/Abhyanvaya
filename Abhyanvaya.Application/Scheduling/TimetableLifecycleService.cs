using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.Common.Interfaces.Scheduling;
using Abhyanvaya.Application.DTOs.Scheduling;
using Abhyanvaya.Application.Internal;
using Abhyanvaya.Domain.Enums.Scheduling;
using Abhyanvaya.Domain.Exceptions;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Abhyanvaya.Application.Scheduling;

public sealed class TimetableLifecycleService : ITimetableLifecycleService
{
    private readonly ITimetableRepository _repository;
    private readonly IScheduleVersionRepository _versionRepository;
    private readonly IArchiveReasonRepository _archiveReasonRepository;
    private readonly IApplicationDbContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;
    private readonly ITimetableChangeHistoryService _historyService;
    private readonly ITimetableService _timetableService;
    private readonly IValidator<FreezeTimetableRequest> _freezeValidator;
    private readonly IValidator<UnlockFrozenTimetableRequest> _unlockValidator;

    public TimetableLifecycleService(
        ITimetableRepository repository,
        IScheduleVersionRepository versionRepository,
        IArchiveReasonRepository archiveReasonRepository,
        IApplicationDbContext context,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUser,
        ITimetableChangeHistoryService historyService,
        ITimetableService timetableService,
        IValidator<FreezeTimetableRequest> freezeValidator,
        IValidator<UnlockFrozenTimetableRequest> unlockValidator)
    {
        _repository = repository;
        _versionRepository = versionRepository;
        _archiveReasonRepository = archiveReasonRepository;
        _context = context;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _historyService = historyService;
        _timetableService = timetableService;
        _freezeValidator = freezeValidator;
        _unlockValidator = unlockValidator;
    }

    private int TenantId => _currentUser.TenantId;

    public async Task<TimetableDto> PublishAsync(int timetableId, PublishTimetableRequest? request, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetByIdAsync(TenantId, timetableId, cancellationToken)
            ?? throw new KeyNotFoundException($"Timetable {timetableId} not found.");
        if (entity.IsFrozen)
            throw new DomainException("Frozen timetables cannot be republished until unlocked.");

        var versionApproved = false;
        if (entity.ScheduleVersionId.HasValue)
        {
            var version = await _versionRepository.GetByIdAsync(TenantId, entity.ScheduleVersionId.Value, cancellationToken);
            versionApproved = version?.Status == ScheduleVersionStatus.Approved;
        }

        if (entity.Status != TimetableStatus.Locked && !versionApproved)
            throw new DomainException("Timetable must be locked or linked to an approved schedule version to publish.");

        var conflict = await _context.SchedulingTimetables.AnyAsync(x =>
            x.TenantId == TenantId
            && x.Id != entity.Id
            && x.AcademicYearId == entity.AcademicYearId
            && x.DepartmentId == entity.DepartmentId
            && x.Status == TimetableStatus.Published
            && !x.IsFrozen, cancellationToken);
        if (conflict)
            throw new DomainException("Another published timetable already exists for this academic year and department scope.");

        var oldStatus = entity.Status;
        entity.Status = TimetableStatus.Published;
        if (entity.ScheduleVersionId.HasValue)
        {
            var version = await _versionRepository.GetByIdAsync(TenantId, entity.ScheduleVersionId.Value, cancellationToken);
            if (version is not null)
            {
                version.Status = ScheduleVersionStatus.Published;
                version.PublishedDate = DateTime.UtcNow;
                version.PublishedBy = _currentUser.UserId;
            }
        }

        await ConcurrencyExceptionHelper.SaveChangesAsync(_unitOfWork, cancellationToken);
        await _historyService.RecordAsync(entity.Id, TimetableChangeOperation.Publish, null, new { Status = oldStatus }, new { Status = entity.Status }, request?.Reason, cancellationToken);
        return (await _timetableService.GetByIdAsync(entity.Id, cancellationToken))!;
    }

    public async Task<TimetableDto> ArchiveAsync(int timetableId, ArchiveTimetableRequest? request, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetByIdAsync(TenantId, timetableId, cancellationToken)
            ?? throw new KeyNotFoundException($"Timetable {timetableId} not found.");
        if (entity.Status != TimetableStatus.Published && entity.Status != TimetableStatus.Locked)
            throw new DomainException("Only published or locked timetables can be archived.");

        if (request?.ArchiveReasonId is > 0)
        {
            _ = await _archiveReasonRepository.GetByIdAsync(TenantId, request.ArchiveReasonId.Value, cancellationToken)
                ?? await _archiveReasonRepository.GetByIdAsync(1, request.ArchiveReasonId.Value, cancellationToken)
                ?? throw new DomainException("Archive reason not found.");
            entity.ArchiveReasonId = request.ArchiveReasonId;
            entity.ArchiveComments = request.Comments?.Trim() ?? request.Reason?.Trim();
            entity.ReferenceVersionId = request.ReferenceVersionId;
        }
        else
        {
            entity.ArchiveComments = request?.Reason?.Trim() ?? request?.Comments?.Trim();
            entity.ReferenceVersionId = request?.ReferenceVersionId;
        }

        var oldStatus = entity.Status;
        entity.Status = TimetableStatus.Archived;
        entity.ArchivedBy = _currentUser.UserId;
        entity.ArchivedDate = DateTime.UtcNow;
        entity.IsFrozen = false;
        await ConcurrencyExceptionHelper.SaveChangesAsync(_unitOfWork, cancellationToken);
        await _historyService.RecordAsync(entity.Id, TimetableChangeOperation.Archive, null,
            new { Status = oldStatus },
            new { Status = entity.Status, entity.ArchiveReasonId, entity.ArchiveComments, entity.ReferenceVersionId },
            entity.ArchiveComments, cancellationToken);
        return (await _timetableService.GetByIdAsync(entity.Id, cancellationToken))!;
    }

    public async Task<TimetableDto> FreezeAsync(int timetableId, FreezeTimetableRequest request, CancellationToken cancellationToken = default)
    {
        await _freezeValidator.ValidateAndThrowAsync(request, cancellationToken);
        var entity = await _repository.GetByIdAsync(TenantId, timetableId, cancellationToken)
            ?? throw new KeyNotFoundException($"Timetable {timetableId} not found.");
        if (entity.Status != TimetableStatus.Published)
            throw new DomainException("Only published timetables can be frozen.");
        if (entity.IsFrozen)
            throw new DomainException("Timetable is already frozen.");

        entity.IsFrozen = true;
        entity.FrozenDate = DateTime.UtcNow;
        entity.FrozenBy = _currentUser.UserId;
        entity.FreezeReason = request.Reason.Trim();
        entity.UnlockDate = null;
        entity.UnlockedBy = null;
        entity.UnlockReason = null;
        await ConcurrencyExceptionHelper.SaveChangesAsync(_unitOfWork, cancellationToken);
        await _historyService.RecordAsync(entity.Id, TimetableChangeOperation.Freeze, null,
            new { IsFrozen = false },
            new { IsFrozen = true, entity.FreezeReason },
            entity.FreezeReason, cancellationToken);
        return (await _timetableService.GetByIdAsync(entity.Id, cancellationToken))!;
    }

    public async Task<TimetableDto> UnlockFrozenAsync(int timetableId, UnlockFrozenTimetableRequest request, CancellationToken cancellationToken = default)
    {
        await _unlockValidator.ValidateAndThrowAsync(request, cancellationToken);
        var entity = await _repository.GetByIdAsync(TenantId, timetableId, cancellationToken)
            ?? throw new KeyNotFoundException($"Timetable {timetableId} not found.");
        if (!entity.IsFrozen)
            throw new DomainException("Timetable is not frozen.");

        entity.IsFrozen = false;
        entity.UnlockDate = DateTime.UtcNow;
        entity.UnlockedBy = _currentUser.UserId;
        entity.UnlockReason = request.Reason.Trim();
        if (entity.Status == TimetableStatus.Published)
        {
            // remain Published after unlock per workflow Published → Frozen → Unlocked → Published
        }

        await ConcurrencyExceptionHelper.SaveChangesAsync(_unitOfWork, cancellationToken);
        await _historyService.RecordAsync(entity.Id, TimetableChangeOperation.Unfreeze, null,
            new { IsFrozen = true },
            new { IsFrozen = false, entity.UnlockReason },
            entity.UnlockReason, cancellationToken);
        return (await _timetableService.GetByIdAsync(entity.Id, cancellationToken))!;
    }

    public async Task<IReadOnlyList<ArchiveReasonDto>> ListArchiveReasonsAsync(CancellationToken cancellationToken = default)
    {
        var items = await _archiveReasonRepository.ListActiveAsync(TenantId, cancellationToken);
        if (items.Count == 0)
            items = await _archiveReasonRepository.ListActiveAsync(1, cancellationToken);
        return items.Select(x => new ArchiveReasonDto
        {
            Id = x.Id,
            Code = x.Code,
            Name = x.Name,
            Description = x.Description,
            SortOrder = x.SortOrder
        }).ToList();
    }
}
