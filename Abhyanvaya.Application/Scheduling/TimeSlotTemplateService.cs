using Abhyanvaya.Application.Common.Interfaces;

using Abhyanvaya.Application.Common.Interfaces.Scheduling;

using Abhyanvaya.Application.DTOs.Scheduling;

using Abhyanvaya.Application.Internal;

using Abhyanvaya.Domain.Entities.Scheduling;

using Abhyanvaya.Domain.Exceptions;

using FluentValidation;



namespace Abhyanvaya.Application.Scheduling;



public sealed class TimeSlotTemplateService : ITimeSlotTemplateService

{

    private readonly ITimeSlotTemplateRepository _repository;

    private readonly ITimeSlotRepository _timeSlotRepository;

    private readonly IUnitOfWork _unitOfWork;

    private readonly ICurrentUserService _currentUser;

    private readonly IValidator<CreateTimeSlotTemplateRequest> _createValidator;

    private readonly IValidator<UpdateTimeSlotTemplateRequest> _updateValidator;



    public TimeSlotTemplateService(

        ITimeSlotTemplateRepository repository,

        ITimeSlotRepository timeSlotRepository,

        IUnitOfWork unitOfWork,

        ICurrentUserService currentUser,

        IValidator<CreateTimeSlotTemplateRequest> createValidator,

        IValidator<UpdateTimeSlotTemplateRequest> updateValidator)

    {

        _repository = repository;

        _timeSlotRepository = timeSlotRepository;

        _unitOfWork = unitOfWork;

        _currentUser = currentUser;

        _createValidator = createValidator;

        _updateValidator = updateValidator;

    }



    private int TenantId => _currentUser.TenantId;



    public async Task<IReadOnlyList<TimeSlotTemplateDto>> ListAsync(CancellationToken cancellationToken = default)

    {

        var items = await _repository.ListAsync(TenantId, cancellationToken);

        var result = new List<TimeSlotTemplateDto>();

        foreach (var item in items)

        {

            var withSets = await _repository.GetWithSetsAndSlotsAsync(TenantId, item.Id, cancellationToken) ?? item;

            result.Add(MapSummary(withSets));

        }

        return result;

    }



    public async Task<TimeSlotTemplateDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default)

    {

        var entity = await _repository.GetWithSetsAndSlotsAsync(TenantId, id, cancellationToken);

        return entity is null ? null : MapSummary(entity);

    }



    public async Task<TimeSlotTemplatePreviewDto?> PreviewAsync(int id, CancellationToken cancellationToken = default)

    {

        var entity = await _repository.GetWithSetsAndSlotsAsync(TenantId, id, cancellationToken);

        if (entity is null)

            return null;



        var sets = entity.TimeSlotSets.Select(MapSet).ToList();

        var slots = entity.TimeSlotSets.SelectMany(s => s.TimeSlots).Select(MapSlot).ToList();

        return new TimeSlotTemplatePreviewDto

        {

            Id = entity.Id,

            Name = entity.Name,

            Description = entity.Description,

            TemplateType = entity.TemplateType,

            IsDefault = entity.IsDefault,

            Sets = sets,

            Slots = slots,

        };

    }



    public async Task<TimeSlotTemplateDto> CreateAsync(CreateTimeSlotTemplateRequest request, CancellationToken cancellationToken = default)

    {

        await _createValidator.ValidateAndThrowAsync(request, cancellationToken);

        if (request.IsDefault)

            await EnsureTemplateHasSlotsOrThrowAsync(null, cancellationToken);



        var entity = new TimeSlotTemplate

        {

            TenantId = TenantId,

            Name = request.Name.Trim(),

            Description = request.Description?.Trim(),

            TemplateType = request.TemplateType,

            IsDefault = request.IsDefault,

        };



        if (request.IsDefault)

            await _repository.ClearDefaultAsync(TenantId, null, cancellationToken);



        await _repository.AddAsync(entity, cancellationToken);

        await ConcurrencyExceptionHelper.SaveChangesAsync(_unitOfWork, cancellationToken);

        return MapSummary(entity);

    }



    public async Task<TimeSlotTemplateDto> UpdateAsync(UpdateTimeSlotTemplateRequest request, CancellationToken cancellationToken = default)

    {

        await _updateValidator.ValidateAndThrowAsync(request, cancellationToken);

        var entity = await _repository.GetByIdAsync(TenantId, request.Id, cancellationToken)

            ?? throw new KeyNotFoundException($"Time slot template '{request.Id}' was not found.");



        if (request.IsDefault)

            await EnsureTemplateHasSlotsOrThrowAsync(request.Id, cancellationToken);



        entity.Name = request.Name.Trim();

        entity.Description = request.Description?.Trim();

        entity.TemplateType = request.TemplateType;



        if (request.IsDefault && !entity.IsDefault)

        {

            await _repository.ClearDefaultAsync(TenantId, request.Id, cancellationToken);

            entity.IsDefault = true;

        }

        else if (!request.IsDefault)

            entity.IsDefault = false;



        await ConcurrencyExceptionHelper.SaveChangesAsync(_unitOfWork, cancellationToken);

        var withSets = await _repository.GetWithSetsAndSlotsAsync(TenantId, entity.Id, cancellationToken) ?? entity;

        return MapSummary(withSets);

    }



    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)

    {

        var entity = await _repository.GetByIdAsync(TenantId, id, cancellationToken)

            ?? throw new KeyNotFoundException($"Time slot template '{id}' was not found.");

        entity.IsDeleted = true;

        await ConcurrencyExceptionHelper.SaveChangesAsync(_unitOfWork, cancellationToken);

    }



    public async Task<TimeSlotTemplateDto> CloneAsync(CloneTimeSlotTemplateRequest request, CancellationToken cancellationToken = default)

    {

        var source = await _repository.GetWithSetsAndSlotsAsync(TenantId, request.SourceTemplateId, cancellationToken)

            ?? throw new KeyNotFoundException($"Source time slot template '{request.SourceTemplateId}' was not found.");



        if (request.IsDefault)

            await EnsureTemplateHasSlotsOrThrowAsync(source.Id, cancellationToken);



        var clone = new TimeSlotTemplate

        {

            TenantId = TenantId,

            Name = request.Name.Trim(),

            Description = request.Description?.Trim() ?? source.Description,

            TemplateType = request.TemplateType,

            IsDefault = request.IsDefault,

        };



        if (request.IsDefault)

            await _repository.ClearDefaultAsync(TenantId, null, cancellationToken);



        await _repository.AddAsync(clone, cancellationToken);

        await ConcurrencyExceptionHelper.SaveChangesAsync(_unitOfWork, cancellationToken);



        foreach (var sourceSet in source.TimeSlotSets)

        {

            var newSet = new TimeSlotSet

            {

                TenantId = TenantId,

                Name = sourceSet.Name,

                Code = $"{sourceSet.Code}_T{clone.Id}",

                AcademicYearId = sourceSet.AcademicYearId,

                Description = sourceSet.Description,

                IsDefault = false,

                TimeSlotTemplateId = clone.Id,

            };

            await _timeSlotRepository.AddSetAsync(newSet, cancellationToken);

            await ConcurrencyExceptionHelper.SaveChangesAsync(_unitOfWork, cancellationToken);



            var slots = sourceSet.TimeSlots.Select(s => new TimeSlot

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

                await _timeSlotRepository.AddRangeAsync(slots, cancellationToken);

        }



        await ConcurrencyExceptionHelper.SaveChangesAsync(_unitOfWork, cancellationToken);

        var withSets = await _repository.GetWithSetsAndSlotsAsync(TenantId, clone.Id, cancellationToken) ?? clone;

        return MapSummary(withSets);

    }



    public async Task<TimeSlotTemplateDto> SetDefaultAsync(int id, CancellationToken cancellationToken = default)

    {

        var entity = await _repository.GetByIdAsync(TenantId, id, cancellationToken)

            ?? throw new KeyNotFoundException($"Time slot template '{id}' was not found.");



        await EnsureTemplateHasSlotsOrThrowAsync(id, cancellationToken);

        await _repository.ClearDefaultAsync(TenantId, id, cancellationToken);

        entity.IsDefault = true;

        await ConcurrencyExceptionHelper.SaveChangesAsync(_unitOfWork, cancellationToken);



        var withSets = await _repository.GetWithSetsAndSlotsAsync(TenantId, id, cancellationToken) ?? entity;

        return MapSummary(withSets);

    }



    private async Task EnsureTemplateHasSlotsOrThrowAsync(int? templateId, CancellationToken cancellationToken)

    {

        if (!templateId.HasValue)

            throw new DomainException("Cannot set default on a template without at least one time slot set containing slots.");



        if (!await _repository.HasSetWithSlotsAsync(TenantId, templateId.Value, cancellationToken))

            throw new DomainException("Template must contain at least one time slot set with slots before it can be set as default.");

    }



    private static TimeSlotTemplateDto MapSummary(TimeSlotTemplate x)

    {

        var sets = x.TimeSlotSets ?? [];

        return new TimeSlotTemplateDto

        {

            Id = x.Id,

            Name = x.Name,

            Description = x.Description,

            TemplateType = x.TemplateType,

            IsDefault = x.IsDefault,

            SetCount = sets.Count,

            SlotCount = sets.Sum(s => s.TimeSlots?.Count ?? 0),

        };

    }



    private static TimeSlotSetDto MapSet(TimeSlotSet x) => new()

    {

        Id = x.Id,

        Name = x.Name,

        Code = x.Code,

        AcademicYearId = x.AcademicYearId,

        Description = x.Description,

        IsDefault = x.IsDefault,

    };



    private static TimeSlotDto MapSlot(TimeSlot x) => new()

    {

        Id = x.Id,

        TimeSlotSetId = x.TimeSlotSetId,

        PeriodNumber = x.PeriodNumber,

        Name = x.Name,

        StartTime = x.StartTime,

        EndTime = x.EndTime,

        DurationMinutes = x.DurationMinutes,

        DayOfWeek = x.DayOfWeek,

        SlotKind = x.SlotKind,

        SessionKind = x.SessionKind,

    };

}

