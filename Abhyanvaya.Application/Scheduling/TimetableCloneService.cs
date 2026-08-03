using System.Text.Json;
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

public sealed class TimetableCloneService : ITimetableCloneService
{
    private readonly ITimetableCloneJobRepository _repository;
    private readonly ITimetableRepository _timetableRepository;
    private readonly IApplicationDbContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;
    private readonly ITimetableChangeHistoryService _historyService;
    private readonly IValidator<EnqueueTimetableCloneRequest> _validator;

    public TimetableCloneService(
        ITimetableCloneJobRepository repository,
        ITimetableRepository timetableRepository,
        IApplicationDbContext context,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUser,
        ITimetableChangeHistoryService historyService,
        IValidator<EnqueueTimetableCloneRequest> validator)
    {
        _repository = repository;
        _timetableRepository = timetableRepository;
        _context = context;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _historyService = historyService;
        _validator = validator;
    }

    private int TenantId => _currentUser.TenantId;

    public async Task<TimetableCloneJobDto> EnqueueAsync(EnqueueTimetableCloneRequest request, CancellationToken cancellationToken = default)
    {
        await _validator.ValidateAndThrowAsync(request, cancellationToken);
        if (await _timetableRepository.GetByIdAsync(TenantId, request.SourceTimetableId, cancellationToken) is null)
            throw new KeyNotFoundException($"Source timetable {request.SourceTimetableId} not found.");

        var job = new TimetableCloneJob
        {
            TenantId = TenantId,
            JobType = request.JobType,
            SourceTimetableId = request.SourceTimetableId,
            TargetTimetableId = request.TargetTimetableId,
            PayloadJson = JsonSerializer.Serialize(request),
            Status = TimetableCloneJobStatus.Queued,
            RequestedBy = _currentUser.UserId
        };
        await _repository.AddAsync(job, cancellationToken);
        await ConcurrencyExceptionHelper.SaveChangesAsync(_unitOfWork, cancellationToken);

        if (request.ExecuteSynchronously)
            await ExecuteJobAsync(job.Id, cancellationToken);

        return Map(await _repository.GetByIdAsync(TenantId, job.Id, cancellationToken) ?? job);
    }

    public async Task<TimetableCloneJobDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetByIdAsync(TenantId, id, cancellationToken);
        return entity is null ? null : Map(entity);
    }

    public async Task<IReadOnlyList<TimetableCloneJobDto>> ListAsync(TimetableCloneJobStatus? status, CancellationToken cancellationToken = default)
    {
        var items = await _repository.ListAsync(TenantId, status, cancellationToken);
        return items.Select(Map).ToList();
    }

    public async Task ExecuteJobAsync(int jobId, CancellationToken cancellationToken = default)
    {
        var job = await _repository.GetByIdAsync(TenantId, jobId, cancellationToken)
            ?? throw new KeyNotFoundException($"Clone job {jobId} not found.");
        if (job.Status is TimetableCloneJobStatus.Completed or TimetableCloneJobStatus.Running)
            return;

        job.Status = TimetableCloneJobStatus.Running;
        job.StartedUtc = DateTime.UtcNow;
        job.ProgressPercent = 10;
        await ConcurrencyExceptionHelper.SaveChangesAsync(_unitOfWork, cancellationToken);

        try
        {
            var request = string.IsNullOrWhiteSpace(job.PayloadJson)
                ? new EnqueueTimetableCloneRequest { JobType = job.JobType, SourceTimetableId = job.SourceTimetableId }
                : JsonSerializer.Deserialize<EnqueueTimetableCloneRequest>(job.PayloadJson)!;

            var source = await _timetableRepository.GetByIdWithEntriesAsync(TenantId, job.SourceTimetableId, cancellationToken)
                ?? throw new DomainException("Source timetable not found.");
            TimetableService.EnsureCloneable(source);

            var target = await ResolveTargetTimetableAsync(source, request, cancellationToken);
            job.TargetTimetableId = target.Id;
            job.ProgressPercent = 40;

            var filtered = await FilterEntriesAsync(source, request, cancellationToken);
            var clones = filtered.Select(e =>
            {
                var clone = TimetableService.CloneEntry(e, target.Id);
                if (request.JobType == TimetableCloneJobType.Day && request.TargetDayOfWeek.HasValue)
                    clone.DayOfWeek = request.TargetDayOfWeek.Value;
                return clone;
            }).ToList();
            if (clones.Count > 0)
                await _timetableRepository.AddEntriesAsync(clones, cancellationToken);

            job.ProgressPercent = 90;
            job.Status = TimetableCloneJobStatus.Completed;
            job.CompletedUtc = DateTime.UtcNow;
            job.Summary = $"Cloned {clones.Count} entries from timetable {source.Id} to {target.Id}.";
            await ConcurrencyExceptionHelper.SaveChangesAsync(_unitOfWork, cancellationToken);
            await _historyService.RecordAsync(target.Id, TimetableChangeOperation.Clone, null, null, new { SourceTimetableId = source.Id, EntryCount = clones.Count }, job.Summary, cancellationToken);
        }
        catch (Exception ex)
        {
            job.Status = TimetableCloneJobStatus.Failed;
            job.Error = ex.Message;
            job.CompletedUtc = DateTime.UtcNow;
            await ConcurrencyExceptionHelper.SaveChangesAsync(_unitOfWork, cancellationToken);
            throw;
        }
    }

    private async Task<Timetable> ResolveTargetTimetableAsync(Timetable source, EnqueueTimetableCloneRequest request, CancellationToken cancellationToken)
    {
        if (request.TargetTimetableId.HasValue)
        {
            var existing = await _timetableRepository.GetByIdAsync(TenantId, request.TargetTimetableId.Value, cancellationToken)
                ?? throw new KeyNotFoundException($"Target timetable {request.TargetTimetableId} not found.");
            TimetableService.EnsureDraft(existing);
            return existing;
        }

        if (request.JobType == TimetableCloneJobType.Day && request.TargetDayOfWeek.HasValue)
            return source;

        var target = new Timetable
        {
            TenantId = TenantId,
            Name = (request.TargetTimetableName ?? $"{source.Name} Clone").Trim(),
            AcademicYearId = source.AcademicYearId,
            DepartmentId = source.DepartmentId,
            TimeSlotSetId = source.TimeSlotSetId,
            ScheduleVersionId = request.TargetScheduleVersionId ?? source.ScheduleVersionId,
            Status = TimetableStatus.Draft,
            Notes = source.Notes
        };
        await _timetableRepository.AddAsync(target, cancellationToken);
        await ConcurrencyExceptionHelper.SaveChangesAsync(_unitOfWork, cancellationToken);
        return target;
    }

    private async Task<IReadOnlyList<TimetableEntry>> FilterEntriesAsync(Timetable source, EnqueueTimetableCloneRequest request, CancellationToken cancellationToken)
    {
        var entries = source.Entries.Where(e => !e.IsDeleted).ToList();
        if (entries.Count == 0)
            entries = (await _timetableRepository.ListEntriesAsync(TenantId, source.Id, cancellationToken)).ToList();

        return request.JobType switch
        {
            TimetableCloneJobType.Day when request.SourceDayOfWeek.HasValue =>
                entries.Where(e => e.DayOfWeek == request.SourceDayOfWeek.Value).ToList(),
            TimetableCloneJobType.Week => entries,
            TimetableCloneJobType.Department when request.DepartmentId.HasValue => entries.Where(e => e.DepartmentId == request.DepartmentId.Value).ToList(),
            TimetableCloneJobType.Course when request.CourseId.HasValue => entries.Where(e => e.CourseId == request.CourseId.Value).ToList(),
            TimetableCloneJobType.Group when request.GroupId.HasValue => entries.Where(e => e.GroupId == request.GroupId.Value).ToList(),
            TimetableCloneJobType.Faculty when request.StaffId.HasValue => entries.Where(e => e.StaffId == request.StaffId.Value).ToList(),
            TimetableCloneJobType.Room when request.RoomId.HasValue => entries.Where(e => e.RoomId == request.RoomId.Value).ToList(),
            TimetableCloneJobType.Semester or TimetableCloneJobType.AcademicYear => entries,
            _ => entries
        };
    }

    private static TimetableCloneJobDto Map(TimetableCloneJob entity) => new()
    {
        Id = entity.Id,
        JobType = entity.JobType,
        SourceTimetableId = entity.SourceTimetableId,
        TargetTimetableId = entity.TargetTimetableId,
        PayloadJson = entity.PayloadJson,
        Status = entity.Status,
        ProgressPercent = entity.ProgressPercent,
        Summary = entity.Summary,
        Error = entity.Error,
        RequestedBy = entity.RequestedBy,
        StartedUtc = entity.StartedUtc,
        CompletedUtc = entity.CompletedUtc
    };
}
