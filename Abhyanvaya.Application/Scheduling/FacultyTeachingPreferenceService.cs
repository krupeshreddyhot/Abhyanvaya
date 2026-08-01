using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.Common.Interfaces.Scheduling;
using Abhyanvaya.Application.DTOs.Scheduling;
using Abhyanvaya.Application.Internal;
using Abhyanvaya.Domain.Entities.Scheduling;
using Abhyanvaya.Domain.Exceptions;
using FluentValidation;

namespace Abhyanvaya.Application.Scheduling;

public sealed class FacultyTeachingPreferenceService : IFacultyTeachingPreferenceService
{
    private readonly IFacultyTeachingPreferenceRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;
    private readonly IValidator<CreateFacultyTeachingPreferenceRequest> _createValidator;
    private readonly IValidator<UpdateFacultyTeachingPreferenceRequest> _updateValidator;

    public FacultyTeachingPreferenceService(
        IFacultyTeachingPreferenceRepository repository,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUser,
        IValidator<CreateFacultyTeachingPreferenceRequest> createValidator,
        IValidator<UpdateFacultyTeachingPreferenceRequest> updateValidator)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    private int TenantId => _currentUser.TenantId;

    public async Task<IReadOnlyList<FacultyTeachingPreferenceDto>> ListAsync(int? academicYearId, int? staffId, bool? isActive, CancellationToken cancellationToken = default)
    {
        var items = await _repository.ListAsync(TenantId, academicYearId, staffId, isActive, cancellationToken);
        return items.Select(Map).ToList();
    }

    public async Task<FacultyTeachingPreferenceDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetByIdAsync(TenantId, id, cancellationToken);
        return entity is null ? null : Map(entity);
    }

    public async Task<FacultyTeachingPreferenceDto> CreateAsync(CreateFacultyTeachingPreferenceRequest request, CancellationToken cancellationToken = default)
    {
        await _createValidator.ValidateAndThrowAsync(request, cancellationToken);
        if (request.IsActive && await _repository.ActiveExistsAsync(TenantId, request.StaffId, request.AcademicYearId, null, cancellationToken))
            throw new DomainException("An active teaching preference already exists for this faculty member and academic year.");

        var entity = MapToEntity(request);
        entity.TenantId = TenantId;
        await _repository.AddAsync(entity, cancellationToken);
        await ConcurrencyExceptionHelper.SaveChangesAsync(_unitOfWork, cancellationToken);
        return Map(entity);
    }

    public async Task<FacultyTeachingPreferenceDto> UpdateAsync(UpdateFacultyTeachingPreferenceRequest request, CancellationToken cancellationToken = default)
    {
        await _updateValidator.ValidateAndThrowAsync(request, cancellationToken);
        var entity = await _repository.GetByIdAsync(TenantId, request.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"Faculty teaching preference '{request.Id}' was not found.");

        if (request.IsActive && await _repository.ActiveExistsAsync(TenantId, request.StaffId, request.AcademicYearId, request.Id, cancellationToken))
            throw new DomainException("An active teaching preference already exists for this faculty member and academic year.");

        ApplyUpdate(entity, request);
        await ConcurrencyExceptionHelper.SaveChangesAsync(_unitOfWork, cancellationToken);
        return Map(entity);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetByIdAsync(TenantId, id, cancellationToken)
            ?? throw new KeyNotFoundException($"Faculty teaching preference '{id}' was not found.");
        entity.IsDeleted = true;
        await ConcurrencyExceptionHelper.SaveChangesAsync(_unitOfWork, cancellationToken);
    }

    private static FacultyTeachingPreference MapToEntity(CreateFacultyTeachingPreferenceRequest request) => new()
    {
        StaffId = request.StaffId,
        AcademicYearId = request.AcademicYearId,
        PreferredCampusId = request.PreferredCampusId,
        PreferredBuildingId = request.PreferredBuildingId,
        PreferredFloorId = request.PreferredFloorId,
        PreferredRoomId = request.PreferredRoomId,
        PreferredSubjectId = request.PreferredSubjectId,
        PreferredDepartmentId = request.PreferredDepartmentId,
        PreferredCourseId = request.PreferredCourseId,
        PreferredGroupId = request.PreferredGroupId,
        PreferredSemesterId = request.PreferredSemesterId,
        PreferredFirstPeriod = request.PreferredFirstPeriod,
        PreferredLastPeriod = request.PreferredLastPeriod,
        PreferredWorkingDaysFlags = request.PreferredWorkingDaysFlags,
        MaximumContinuousClasses = request.MaximumContinuousClasses,
        MinimumBreakBetweenClasses = request.MinimumBreakBetweenClasses,
        PreferredTeachingMode = request.PreferredTeachingMode,
        Priority = request.Priority,
        Remarks = request.Remarks?.Trim(),
        IsActive = request.IsActive,
    };

    private static void ApplyUpdate(FacultyTeachingPreference entity, UpdateFacultyTeachingPreferenceRequest request)
    {
        entity.StaffId = request.StaffId;
        entity.AcademicYearId = request.AcademicYearId;
        entity.PreferredCampusId = request.PreferredCampusId;
        entity.PreferredBuildingId = request.PreferredBuildingId;
        entity.PreferredFloorId = request.PreferredFloorId;
        entity.PreferredRoomId = request.PreferredRoomId;
        entity.PreferredSubjectId = request.PreferredSubjectId;
        entity.PreferredDepartmentId = request.PreferredDepartmentId;
        entity.PreferredCourseId = request.PreferredCourseId;
        entity.PreferredGroupId = request.PreferredGroupId;
        entity.PreferredSemesterId = request.PreferredSemesterId;
        entity.PreferredFirstPeriod = request.PreferredFirstPeriod;
        entity.PreferredLastPeriod = request.PreferredLastPeriod;
        entity.PreferredWorkingDaysFlags = request.PreferredWorkingDaysFlags;
        entity.MaximumContinuousClasses = request.MaximumContinuousClasses;
        entity.MinimumBreakBetweenClasses = request.MinimumBreakBetweenClasses;
        entity.PreferredTeachingMode = request.PreferredTeachingMode;
        entity.Priority = request.Priority;
        entity.Remarks = request.Remarks?.Trim();
        entity.IsActive = request.IsActive;
    }

    private static FacultyTeachingPreferenceDto Map(FacultyTeachingPreference x) => new()
    {
        Id = x.Id,
        StaffId = x.StaffId,
        AcademicYearId = x.AcademicYearId,
        PreferredCampusId = x.PreferredCampusId,
        PreferredBuildingId = x.PreferredBuildingId,
        PreferredFloorId = x.PreferredFloorId,
        PreferredRoomId = x.PreferredRoomId,
        PreferredSubjectId = x.PreferredSubjectId,
        PreferredDepartmentId = x.PreferredDepartmentId,
        PreferredCourseId = x.PreferredCourseId,
        PreferredGroupId = x.PreferredGroupId,
        PreferredSemesterId = x.PreferredSemesterId,
        PreferredFirstPeriod = x.PreferredFirstPeriod,
        PreferredLastPeriod = x.PreferredLastPeriod,
        PreferredWorkingDaysFlags = x.PreferredWorkingDaysFlags,
        MaximumContinuousClasses = x.MaximumContinuousClasses,
        MinimumBreakBetweenClasses = x.MinimumBreakBetweenClasses,
        PreferredTeachingMode = x.PreferredTeachingMode,
        Priority = x.Priority,
        Remarks = x.Remarks,
        IsActive = x.IsActive,
    };
}
