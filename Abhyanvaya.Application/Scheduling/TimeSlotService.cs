using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.Common.Interfaces.Scheduling;
using Abhyanvaya.Application.DTOs.Scheduling;
using Abhyanvaya.Application.Internal;
using Abhyanvaya.Domain.Entities.Scheduling;
using Abhyanvaya.Domain.Exceptions;

namespace Abhyanvaya.Application.Scheduling;

public sealed class TimeSlotService : ITimeSlotService
{
    private readonly ITimeSlotRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;

    public TimeSlotService(ITimeSlotRepository repository, IUnitOfWork unitOfWork, ICurrentUserService currentUser)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
    }

    private int TenantId => _currentUser.TenantId;

    public async Task<IReadOnlyList<TimeSlotSetDto>> ListSetsAsync(int? academicYearId, CancellationToken cancellationToken = default)
    {
        var items = await _repository.ListSetsAsync(TenantId, academicYearId, cancellationToken);
        return items.Select(MapSet).ToList();
    }

    public async Task<TimeSlotSetDto?> GetSetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetSetByIdAsync(TenantId, id, cancellationToken);
        return entity is null ? null : MapSet(entity);
    }

    public async Task<TimeSlotSetDto> CreateSetAsync(CreateTimeSlotSetRequest request, CancellationToken cancellationToken = default)
    {
        if (await _repository.SetCodeExistsAsync(TenantId, request.Code.Trim(), null, cancellationToken))
            throw new DomainException($"Time slot set code '{request.Code}' already exists.");

        var entity = new TimeSlotSet
        {
            TenantId = TenantId,
            Name = request.Name.Trim(),
            Code = request.Code.Trim(),
            AcademicYearId = request.AcademicYearId,
            Description = request.Description?.Trim(),
            IsDefault = request.IsDefault,
        };
        await _repository.AddSetAsync(entity, cancellationToken);
        await ConcurrencyExceptionHelper.SaveChangesAsync(_unitOfWork, cancellationToken);
        return MapSet(entity);
    }

    public async Task<TimeSlotSetDto> UpdateSetAsync(UpdateTimeSlotSetRequest request, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetSetByIdAsync(TenantId, request.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"Time slot set '{request.Id}' was not found.");
        if (await _repository.SetCodeExistsAsync(TenantId, request.Code.Trim(), request.Id, cancellationToken))
            throw new DomainException($"Time slot set code '{request.Code}' already exists.");

        entity.Name = request.Name.Trim();
        entity.Code = request.Code.Trim();
        entity.AcademicYearId = request.AcademicYearId;
        entity.Description = request.Description?.Trim();
        entity.IsDefault = request.IsDefault;
        await ConcurrencyExceptionHelper.SaveChangesAsync(_unitOfWork, cancellationToken);
        return MapSet(entity);
    }

    public async Task DeleteSetAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetSetByIdAsync(TenantId, id, cancellationToken)
            ?? throw new KeyNotFoundException($"Time slot set '{id}' was not found.");
        entity.IsDeleted = true;
        await ConcurrencyExceptionHelper.SaveChangesAsync(_unitOfWork, cancellationToken);
    }

    public async Task<TimeSlotSetDto> CloneSetAsync(CloneTimeSlotSetRequest request, CancellationToken cancellationToken = default)
    {
        var source = await _repository.GetSetWithSlotsAsync(TenantId, request.SourceSetId, cancellationToken)
            ?? throw new KeyNotFoundException($"Source time slot set '{request.SourceSetId}' was not found.");
        if (await _repository.SetCodeExistsAsync(TenantId, request.Code.Trim(), null, cancellationToken))
            throw new DomainException($"Time slot set code '{request.Code}' already exists.");

        var newSet = new TimeSlotSet
        {
            TenantId = TenantId,
            Name = request.Name.Trim(),
            Code = request.Code.Trim(),
            AcademicYearId = request.AcademicYearId ?? source.AcademicYearId,
            Description = source.Description,
            IsDefault = request.IsDefault,
        };
        await _repository.AddSetAsync(newSet, cancellationToken);
        await ConcurrencyExceptionHelper.SaveChangesAsync(_unitOfWork, cancellationToken);

        var slots = source.TimeSlots.Select(s => new TimeSlot
        {
            TenantId = TenantId,
            TimeSlotSetId = newSet.Id,
            PeriodNumber = s.PeriodNumber,
            Name = s.Name,
            StartTime = s.StartTime,
            EndTime = s.EndTime,
            DurationMinutes = s.DurationMinutes,
            DayOfWeek = s.DayOfWeek,
            SlotKind = s.SlotKind,
            SessionKind = s.SessionKind,
        }).ToList();
        if (slots.Count > 0)
            await _repository.AddRangeAsync(slots, cancellationToken);

        await ConcurrencyExceptionHelper.SaveChangesAsync(_unitOfWork, cancellationToken);
        return MapSet(newSet);
    }

    public async Task<IReadOnlyList<TimeSlotDto>> ListSlotsAsync(int timeSlotSetId, CancellationToken cancellationToken = default)
    {
        var items = await _repository.ListSlotsAsync(TenantId, timeSlotSetId, cancellationToken);
        return items.Select(MapSlot).ToList();
    }

    public async Task<TimeSlotDto?> GetSlotByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetSlotByIdAsync(TenantId, id, cancellationToken);
        return entity is null ? null : MapSlot(entity);
    }

    public async Task<TimeSlotDto> CreateSlotAsync(CreateTimeSlotRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureSetExistsAsync(request.TimeSlotSetId, cancellationToken);
        ValidateSlotTimes(request.StartTime, request.EndTime, request.DurationMinutes);
        await ValidateSlotConstraintsAsync(request.TimeSlotSetId, new TimeSlotInterval(request.DayOfWeek, request.PeriodNumber, request.StartTime, request.EndTime), null, cancellationToken);

        var entity = new TimeSlot
        {
            TenantId = TenantId,
            TimeSlotSetId = request.TimeSlotSetId,
            PeriodNumber = request.PeriodNumber,
            Name = request.Name.Trim(),
            StartTime = request.StartTime,
            EndTime = request.EndTime,
            DurationMinutes = request.DurationMinutes,
            DayOfWeek = request.DayOfWeek,
            SlotKind = request.SlotKind,
            SessionKind = request.SessionKind,
        };
        await _repository.AddSlotAsync(entity, cancellationToken);
        await ConcurrencyExceptionHelper.SaveChangesAsync(_unitOfWork, cancellationToken);
        return MapSlot(entity);
    }

    public async Task<TimeSlotDto> UpdateSlotAsync(UpdateTimeSlotRequest request, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetSlotByIdAsync(TenantId, request.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"Time slot '{request.Id}' was not found.");
        await EnsureSetExistsAsync(request.TimeSlotSetId, cancellationToken);
        ValidateSlotTimes(request.StartTime, request.EndTime, request.DurationMinutes);
        await ValidateSlotConstraintsAsync(request.TimeSlotSetId,
            new TimeSlotInterval(request.DayOfWeek, request.PeriodNumber, request.StartTime, request.EndTime, request.Id),
            request.Id, cancellationToken);

        entity.TimeSlotSetId = request.TimeSlotSetId;
        entity.PeriodNumber = request.PeriodNumber;
        entity.Name = request.Name.Trim();
        entity.StartTime = request.StartTime;
        entity.EndTime = request.EndTime;
        entity.DurationMinutes = request.DurationMinutes;
        entity.DayOfWeek = request.DayOfWeek;
        entity.SlotKind = request.SlotKind;
        entity.SessionKind = request.SessionKind;
        await ConcurrencyExceptionHelper.SaveChangesAsync(_unitOfWork, cancellationToken);
        return MapSlot(entity);
    }

    public async Task DeleteSlotAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetSlotByIdAsync(TenantId, id, cancellationToken)
            ?? throw new KeyNotFoundException($"Time slot '{id}' was not found.");
        entity.IsDeleted = true;
        await ConcurrencyExceptionHelper.SaveChangesAsync(_unitOfWork, cancellationToken);
    }

    private async Task EnsureSetExistsAsync(int setId, CancellationToken cancellationToken)
    {
        if (await _repository.GetSetByIdAsync(TenantId, setId, cancellationToken) is null)
            throw new KeyNotFoundException($"Time slot set '{setId}' was not found.");
    }

    private static void ValidateSlotTimes(TimeSpan start, TimeSpan end, int durationMinutes)
    {
        if (end <= start)
            throw new DomainException("End time must be after start time.");
        var computed = (int)(end - start).TotalMinutes;
        if (computed != durationMinutes)
            throw new DomainException("DurationMinutes must match the difference between start and end times.");
    }

    private async Task ValidateSlotConstraintsAsync(int setId, TimeSlotInterval candidate, int? excludeId, CancellationToken cancellationToken)
    {
        var existing = (await _repository.ListSlotsAsync(TenantId, setId, cancellationToken))
            .Select(s => (s.Id, new TimeSlotInterval(s.DayOfWeek, s.PeriodNumber, s.StartTime, s.EndTime)))
            .ToList();

        if (TimeSlotOverlapHelper.HasDuplicatePeriodNumber(existing, candidate with { ExcludeId = excludeId }))
            throw new DomainException("Duplicate period number within the same set and day scope.");

        if (TimeSlotOverlapHelper.HasOverlap(existing, candidate with { ExcludeId = excludeId }))
            throw new DomainException("Time slot overlaps with an existing slot in the same set and day scope.");
    }

    private static TimeSlotSetDto MapSet(TimeSlotSet x) => new()
    {
        Id = x.Id, Name = x.Name, Code = x.Code, AcademicYearId = x.AcademicYearId, Description = x.Description, IsDefault = x.IsDefault,
    };

    private static TimeSlotDto MapSlot(TimeSlot x) => new()
    {
        Id = x.Id, TimeSlotSetId = x.TimeSlotSetId, PeriodNumber = x.PeriodNumber, Name = x.Name,
        StartTime = x.StartTime, EndTime = x.EndTime, DurationMinutes = x.DurationMinutes,
        DayOfWeek = x.DayOfWeek, SlotKind = x.SlotKind, SessionKind = x.SessionKind,
    };
}
