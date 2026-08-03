using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.Common.Interfaces.Scheduling;
using Abhyanvaya.Application.DTOs.Scheduling;
using Abhyanvaya.Application.Internal;
using Abhyanvaya.Domain.Entities.Scheduling;
using Abhyanvaya.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Abhyanvaya.Application.Scheduling;

public sealed class FacultyWorkloadService : IFacultyWorkloadService
{
    private readonly IFacultyWorkloadRepository _repository;
    private readonly IApplicationDbContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;

    public FacultyWorkloadService(
        IFacultyWorkloadRepository repository,
        IApplicationDbContext context,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUser)
    {
        _repository = repository;
        _context = context;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
    }

    private int TenantId => _currentUser.TenantId;

    public async Task<FacultyWorkloadDto?> GetByStaffIdAsync(int staffId, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetByStaffIdAsync(TenantId, staffId, cancellationToken);
        return entity is null ? null : Map(entity);
    }

    public async Task<FacultyWorkloadDto> UpsertAsync(UpsertFacultyWorkloadRequest request, CancellationToken cancellationToken = default)
    {
        if (!await _context.StaffMembers.AnyAsync(s => s.Id == request.StaffId && s.TenantId == TenantId, cancellationToken))
            throw new KeyNotFoundException($"Staff '{request.StaffId}' was not found.");

        var entity = await _repository.GetByStaffIdAsync(TenantId, request.StaffId, cancellationToken);
        if (entity is null)
        {
            entity = new FacultyWorkload { TenantId = TenantId, StaffId = request.StaffId };
            await _repository.AddAsync(entity, cancellationToken);
        }

        entity.MaxPeriodsPerDay = request.MaxPeriodsPerDay;
        entity.MaxPeriodsPerWeek = request.MaxPeriodsPerWeek;
        entity.TeachingLoadHours = request.TeachingLoadHours;
        entity.LabLoadHours = request.LabLoadHours;
        entity.MentoringLoadHours = request.MentoringLoadHours;
        entity.AdministrativeLoadHours = request.AdministrativeLoadHours;
        entity.IsGuestFaculty = request.IsGuestFaculty;
        entity.IsAdjunctFaculty = request.IsAdjunctFaculty;
        entity.Notes = request.Notes?.Trim();
        await ConcurrencyExceptionHelper.SaveChangesAsync(_unitOfWork, cancellationToken);

        var reloaded = await _repository.GetByStaffIdAsync(TenantId, request.StaffId, cancellationToken)
            ?? throw new InvalidOperationException("Failed to reload faculty workload.");
        return Map(reloaded);
    }

    public async Task DeleteAsync(int staffId, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetByStaffIdAsync(TenantId, staffId, cancellationToken)
            ?? throw new KeyNotFoundException($"Faculty workload for staff '{staffId}' was not found.");
        entity.IsDeleted = true;
        await ConcurrencyExceptionHelper.SaveChangesAsync(_unitOfWork, cancellationToken);
    }

    public async Task<FacultyDayPreferenceDto> UpsertDayPreferenceAsync(UpsertFacultyDayPreferenceRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureWorkloadExistsAsync(request.FacultyWorkloadId, cancellationToken);
        if (request.DayOfWeek > 6)
            throw new DomainException("DayOfWeek must be between 0 and 6.");

        FacultyDayPreference entity;
        if (request.Id.HasValue)
        {
            entity = await _repository.GetDayPreferenceByIdAsync(TenantId, request.Id.Value, cancellationToken)
                ?? throw new KeyNotFoundException($"Day preference '{request.Id}' was not found.");
        }
        else
        {
            entity = new FacultyDayPreference { TenantId = TenantId };
            await _repository.AddDayPreferenceAsync(entity, cancellationToken);
        }

        entity.FacultyWorkloadId = request.FacultyWorkloadId;
        entity.DayOfWeek = request.DayOfWeek;
        entity.PreferenceType = request.PreferenceType;
        await ConcurrencyExceptionHelper.SaveChangesAsync(_unitOfWork, cancellationToken);
        return MapDayPref(entity);
    }

    public async Task DeleteDayPreferenceAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetDayPreferenceByIdAsync(TenantId, id, cancellationToken)
            ?? throw new KeyNotFoundException($"Day preference '{id}' was not found.");
        entity.IsDeleted = true;
        await ConcurrencyExceptionHelper.SaveChangesAsync(_unitOfWork, cancellationToken);
    }

    public async Task<FacultyTimeSlotPreferenceDto> UpsertTimeSlotPreferenceAsync(UpsertFacultyTimeSlotPreferenceRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureWorkloadExistsAsync(request.FacultyWorkloadId, cancellationToken);
        if (!await _context.SchedulingTimeSlots.AnyAsync(x => x.TenantId == TenantId && x.Id == request.TimeSlotId, cancellationToken))
            throw new KeyNotFoundException($"Time slot '{request.TimeSlotId}' was not found.");

        FacultyTimeSlotPreference entity;
        if (request.Id.HasValue)
        {
            entity = await _repository.GetTimeSlotPreferenceByIdAsync(TenantId, request.Id.Value, cancellationToken)
                ?? throw new KeyNotFoundException($"Time slot preference '{request.Id}' was not found.");
        }
        else
        {
            entity = new FacultyTimeSlotPreference { TenantId = TenantId };
            await _repository.AddTimeSlotPreferenceAsync(entity, cancellationToken);
        }

        entity.FacultyWorkloadId = request.FacultyWorkloadId;
        entity.TimeSlotId = request.TimeSlotId;
        entity.IsPreferred = request.IsPreferred;
        await ConcurrencyExceptionHelper.SaveChangesAsync(_unitOfWork, cancellationToken);
        return MapSlotPref(entity);
    }

    public async Task DeleteTimeSlotPreferenceAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetTimeSlotPreferenceByIdAsync(TenantId, id, cancellationToken)
            ?? throw new KeyNotFoundException($"Time slot preference '{id}' was not found.");
        entity.IsDeleted = true;
        await ConcurrencyExceptionHelper.SaveChangesAsync(_unitOfWork, cancellationToken);
    }

    private async Task EnsureWorkloadExistsAsync(int workloadId, CancellationToken cancellationToken)
    {
        if (await _repository.GetByIdWithPreferencesAsync(TenantId, workloadId, cancellationToken) is null)
            throw new KeyNotFoundException($"Faculty workload '{workloadId}' was not found.");
    }

    private static FacultyWorkloadDto Map(FacultyWorkload x) => new()
    {
        Id = x.Id,
        StaffId = x.StaffId,
        MaxPeriodsPerDay = x.MaxPeriodsPerDay,
        MaxPeriodsPerWeek = x.MaxPeriodsPerWeek,
        TeachingLoadHours = x.TeachingLoadHours,
        LabLoadHours = x.LabLoadHours,
        MentoringLoadHours = x.MentoringLoadHours,
        AdministrativeLoadHours = x.AdministrativeLoadHours,
        IsGuestFaculty = x.IsGuestFaculty,
        IsAdjunctFaculty = x.IsAdjunctFaculty,
        Notes = x.Notes,
        DayPreferences = x.DayPreferences.Select(MapDayPref).ToList(),
        TimeSlotPreferences = x.TimeSlotPreferences.Select(MapSlotPref).ToList(),
    };

    private static FacultyDayPreferenceDto MapDayPref(FacultyDayPreference x) => new()
    {
        Id = x.Id, FacultyWorkloadId = x.FacultyWorkloadId, DayOfWeek = x.DayOfWeek, PreferenceType = x.PreferenceType,
    };

    private static FacultyTimeSlotPreferenceDto MapSlotPref(FacultyTimeSlotPreference x) => new()
    {
        Id = x.Id, FacultyWorkloadId = x.FacultyWorkloadId, TimeSlotId = x.TimeSlotId, IsPreferred = x.IsPreferred,
    };
}
