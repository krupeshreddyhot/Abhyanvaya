using Abhyanvaya.Application.Common.Interfaces;

using Abhyanvaya.Application.Common.Interfaces.Scheduling;

using Abhyanvaya.Application.DTOs.Scheduling;

using Abhyanvaya.Application.Internal;

using Abhyanvaya.Domain.Entities.Scheduling;

using Abhyanvaya.Domain.Exceptions;

using FluentValidation;

namespace Abhyanvaya.Application.Scheduling;



public sealed class FacultyAvailabilityService : IFacultyAvailabilityService

{

    private readonly IFacultyAvailabilityRepository _repository;

    private readonly ITimeSlotRepository _timeSlotRepository;

    private readonly IUnitOfWork _unitOfWork;

    private readonly ICurrentUserService _currentUser;

    private readonly IValidator<CreateFacultyAvailabilityRequest> _createValidator;

    private readonly IValidator<UpdateFacultyAvailabilityRequest> _updateValidator;



    public FacultyAvailabilityService(

        IFacultyAvailabilityRepository repository,

        ITimeSlotRepository timeSlotRepository,

        IUnitOfWork unitOfWork,

        ICurrentUserService currentUser,

        IValidator<CreateFacultyAvailabilityRequest> createValidator,

        IValidator<UpdateFacultyAvailabilityRequest> updateValidator)

    {

        _repository = repository;

        _timeSlotRepository = timeSlotRepository;

        _unitOfWork = unitOfWork;

        _currentUser = currentUser;

        _createValidator = createValidator;

        _updateValidator = updateValidator;

    }



    private int TenantId => _currentUser.TenantId;



    public async Task<IReadOnlyList<FacultyAvailabilityDto>> ListAsync(int? academicYearId, int? staffId, CancellationToken cancellationToken = default)

    {

        var items = await _repository.ListAsync(TenantId, academicYearId, staffId, cancellationToken);

        return items.Select(Map).ToList();

    }



    public async Task<FacultyAvailabilityDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default)

    {

        var entity = await _repository.GetByIdAsync(TenantId, id, cancellationToken);

        return entity is null ? null : Map(entity);

    }



    public async Task<FacultyAvailabilityDto> CreateAsync(CreateFacultyAvailabilityRequest request, CancellationToken cancellationToken = default)

    {

        await _createValidator.ValidateAndThrowAsync(request, cancellationToken);

        await EnsureNoOverlapAsync(request.StaffId, request.AcademicYearId, request.StartDate, request.EndDate,

            request.StartSlotId, request.EndSlotId, null, cancellationToken);



        var entity = MapToEntity(request);

        entity.TenantId = TenantId;

        await _repository.AddAsync(entity, cancellationToken);

        await ConcurrencyExceptionHelper.SaveChangesAsync(_unitOfWork, cancellationToken);

        return Map(entity);

    }



    public async Task<FacultyAvailabilityDto> UpdateAsync(UpdateFacultyAvailabilityRequest request, CancellationToken cancellationToken = default)

    {

        await _updateValidator.ValidateAndThrowAsync(request, cancellationToken);

        var entity = await _repository.GetByIdAsync(TenantId, request.Id, cancellationToken)

            ?? throw new KeyNotFoundException($"Faculty availability '{request.Id}' was not found.");



        await EnsureNoOverlapAsync(request.StaffId, request.AcademicYearId, request.StartDate, request.EndDate,

            request.StartSlotId, request.EndSlotId, request.Id, cancellationToken);



        ApplyRequest(entity, request);

        await ConcurrencyExceptionHelper.SaveChangesAsync(_unitOfWork, cancellationToken);

        return Map(entity);

    }



    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)

    {

        var entity = await _repository.GetByIdAsync(TenantId, id, cancellationToken)

            ?? throw new KeyNotFoundException($"Faculty availability '{id}' was not found.");

        entity.IsDeleted = true;

        await ConcurrencyExceptionHelper.SaveChangesAsync(_unitOfWork, cancellationToken);

    }



    internal async Task EnsureNoOverlapAsync(int staffId, int academicYearId, DateOnly startDate, DateOnly endDate,

        int? startSlotId, int? endSlotId, int? excludeId, CancellationToken cancellationToken)

    {

        var candidates = await _repository.GetOverlappingAsync(TenantId, staffId, academicYearId, startDate, endDate,

            startSlotId, endSlotId, excludeId, cancellationToken);

        if (candidates.Count == 0)

            return;



        var (startTime, endTime) = await ResolveSlotTimesAsync(startSlotId, endSlotId, cancellationToken);



        foreach (var existing in candidates)

        {

            var (existingStart, existingEnd) = await ResolveSlotTimesAsync(existing.StartSlotId, existing.EndSlotId, cancellationToken);

            if (AvailabilityOverlapHelper.HasOverlap(

                    startDate, endDate, startSlotId, endSlotId, startTime, endTime,

                    existing.StartDate, existing.EndDate, existing.StartSlotId, existing.EndSlotId, existingStart, existingEnd))

                throw new DomainException("Faculty availability overlaps with an existing record for the same staff and academic year.");

        }

    }



    private async Task<(TimeSpan? Start, TimeSpan? End)> ResolveSlotTimesAsync(int? startSlotId, int? endSlotId, CancellationToken cancellationToken)

    {

        TimeSpan? start = null;

        TimeSpan? end = null;

        if (startSlotId.HasValue)

        {

            var slot = await _timeSlotRepository.GetSlotByIdAsync(TenantId, startSlotId.Value, cancellationToken);

            start = slot?.StartTime;

        }

        if (endSlotId.HasValue)

        {

            var slot = await _timeSlotRepository.GetSlotByIdAsync(TenantId, endSlotId.Value, cancellationToken);

            end = slot?.EndTime;

        }

        else if (start.HasValue)

            end = start;

        return (start, end);

    }



    private static FacultyAvailability MapToEntity(CreateFacultyAvailabilityRequest request) => new()

    {

        StaffId = request.StaffId,

        AcademicYearId = request.AcademicYearId,

        AvailabilityType = request.AvailabilityType,

        StartDate = request.StartDate,

        EndDate = request.EndDate,

        StartSlotId = request.StartSlotId,

        EndSlotId = request.EndSlotId,

        Reason = request.Reason?.Trim(),

        Remarks = request.Remarks?.Trim(),

    };



    private static void ApplyRequest(FacultyAvailability entity, UpdateFacultyAvailabilityRequest request)

    {

        entity.StaffId = request.StaffId;

        entity.AcademicYearId = request.AcademicYearId;

        entity.AvailabilityType = request.AvailabilityType;

        entity.StartDate = request.StartDate;

        entity.EndDate = request.EndDate;

        entity.StartSlotId = request.StartSlotId;

        entity.EndSlotId = request.EndSlotId;

        entity.Reason = request.Reason?.Trim();

        entity.Remarks = request.Remarks?.Trim();

    }



    private static FacultyAvailabilityDto Map(FacultyAvailability x) => new()

    {

        Id = x.Id,

        StaffId = x.StaffId,

        AcademicYearId = x.AcademicYearId,

        AvailabilityType = x.AvailabilityType,

        StartDate = x.StartDate,

        EndDate = x.EndDate,

        StartSlotId = x.StartSlotId,

        EndSlotId = x.EndSlotId,

        Reason = x.Reason,

        Remarks = x.Remarks,

    };

}

