using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.Common.Interfaces.Scheduling;
using Abhyanvaya.Application.DTOs.Scheduling;
using Abhyanvaya.Application.Internal;
using Abhyanvaya.Domain.Entities.Scheduling;
using Abhyanvaya.Domain.Exceptions;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Abhyanvaya.Application.Scheduling;

public sealed class AcademicCalendarService : IAcademicCalendarService
{
    private readonly IAcademicCalendarRepository _repository;
    private readonly IApplicationDbContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;
    private readonly IValidator<CreateAcademicYearRequest> _createYearValidator;
    private readonly IValidator<UpdateAcademicYearRequest> _updateYearValidator;
    private readonly IValidator<CreateHolidayRequest> _createHolidayValidator;
    private readonly IValidator<UpdateHolidayRequest> _updateHolidayValidator;

    public AcademicCalendarService(
        IAcademicCalendarRepository repository,
        IApplicationDbContext context,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUser,
        IValidator<CreateAcademicYearRequest> createYearValidator,
        IValidator<UpdateAcademicYearRequest> updateYearValidator,
        IValidator<CreateHolidayRequest> createHolidayValidator,
        IValidator<UpdateHolidayRequest> updateHolidayValidator)
    {
        _repository = repository;
        _context = context;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _createYearValidator = createYearValidator;
        _updateYearValidator = updateYearValidator;
        _createHolidayValidator = createHolidayValidator;
        _updateHolidayValidator = updateHolidayValidator;
    }

    private int TenantId => _currentUser.TenantId;

    public async Task<IReadOnlyList<AcademicYearDto>> ListYearsAsync(CancellationToken cancellationToken = default)
    {
        var items = await _repository.ListYearsAsync(TenantId, cancellationToken);
        return items.Select(MapYear).ToList();
    }

    public async Task<AcademicYearDto?> GetYearByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetYearByIdAsync(TenantId, id, cancellationToken);
        return entity is null ? null : MapYear(entity);
    }

    public async Task<IReadOnlyList<AcademicTermDto>> ListTermsAsync(int? academicYearId, CancellationToken cancellationToken = default)
    {
        var items = await _repository.ListTermsAsync(TenantId, academicYearId, cancellationToken);
        return items.Select(MapTerm).ToList();
    }

    public async Task<AcademicTermDto?> GetTermByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetTermByIdAsync(TenantId, id, cancellationToken);
        return entity is null ? null : MapTerm(entity);
    }

    public async Task<IReadOnlyList<WorkingDayDto>> ListWorkingDaysAsync(int academicYearId, CancellationToken cancellationToken = default)
    {
        var items = await _repository.ListWorkingDaysAsync(TenantId, academicYearId, cancellationToken);
        return items.Select(MapWorkingDay).ToList();
    }

    public async Task<IReadOnlyList<HolidayDto>> ListHolidaysAsync(int? academicYearId, CancellationToken cancellationToken = default)
    {
        var items = await _repository.ListHolidaysAsync(TenantId, academicYearId, cancellationToken);
        return items.Select(MapHoliday).ToList();
    }

    public async Task<HolidayDto?> GetHolidayByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetHolidayByIdAsync(TenantId, id, cancellationToken);
        return entity is null ? null : MapHoliday(entity);
    }

    public async Task<AcademicYearDto> CreateYearAsync(CreateAcademicYearRequest request, CancellationToken cancellationToken = default)
    {
        await _createYearValidator.ValidateAndThrowAsync(request, cancellationToken);
        if (await _repository.YearCodeExistsAsync(TenantId, request.Code, null, cancellationToken))
            throw new DomainException($"Academic year code '{request.Code}' already exists.");

        if (request.IsCurrent)
            await ClearCurrentYearAsync(cancellationToken);

        var entity = new AcademicYear
        {
            TenantId = TenantId,
            Name = request.Name.Trim(),
            Code = request.Code.Trim(),
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            IsCurrent = request.IsCurrent,
        };
        await _repository.AddYearAsync(entity, cancellationToken);
        await ConcurrencyExceptionHelper.SaveChangesAsync(_unitOfWork, cancellationToken);
        return MapYear(entity);
    }

    public async Task<AcademicYearDto> UpdateYearAsync(UpdateAcademicYearRequest request, CancellationToken cancellationToken = default)
    {
        await _updateYearValidator.ValidateAndThrowAsync(request, cancellationToken);
        var entity = await _repository.GetYearByIdAsync(TenantId, request.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"Academic year '{request.Id}' was not found.");
        if (await _repository.YearCodeExistsAsync(TenantId, request.Code, request.Id, cancellationToken))
            throw new DomainException($"Academic year code '{request.Code}' already exists.");

        if (request.IsCurrent && !entity.IsCurrent)
            await ClearCurrentYearAsync(cancellationToken);

        entity.Name = request.Name.Trim();
        entity.Code = request.Code.Trim();
        entity.StartDate = request.StartDate;
        entity.EndDate = request.EndDate;
        entity.IsCurrent = request.IsCurrent;
        await ConcurrencyExceptionHelper.SaveChangesAsync(_unitOfWork, cancellationToken);
        return MapYear(entity);
    }

    public async Task DeleteYearAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetYearByIdAsync(TenantId, id, cancellationToken)
            ?? throw new KeyNotFoundException($"Academic year '{id}' was not found.");
        entity.IsDeleted = true;
        await ConcurrencyExceptionHelper.SaveChangesAsync(_unitOfWork, cancellationToken);
    }

    public async Task SetCurrentYearAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetYearByIdAsync(TenantId, id, cancellationToken)
            ?? throw new KeyNotFoundException($"Academic year '{id}' was not found.");
        await ClearCurrentYearAsync(cancellationToken);
        entity.IsCurrent = true;
        await ConcurrencyExceptionHelper.SaveChangesAsync(_unitOfWork, cancellationToken);
    }

    public async Task<AcademicYearDto> ClonePreviousYearAsync(ClonePreviousYearRequest request, CancellationToken cancellationToken = default)
    {
        var source = await _repository.GetYearWithDetailsAsync(TenantId, request.SourceYearId, cancellationToken)
            ?? throw new KeyNotFoundException($"Source academic year '{request.SourceYearId}' was not found.");
        if (await _repository.YearCodeExistsAsync(TenantId, request.Code, null, cancellationToken))
            throw new DomainException($"Academic year code '{request.Code}' already exists.");
        if (request.EndDate <= request.StartDate)
            throw new DomainException("End date must be after start date.");

        if (request.SetAsCurrent)
            await ClearCurrentYearAsync(cancellationToken);

        var newYear = new AcademicYear
        {
            TenantId = TenantId,
            Name = request.Name.Trim(),
            Code = request.Code.Trim(),
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            IsCurrent = request.SetAsCurrent,
        };
        await _repository.AddYearAsync(newYear, cancellationToken);
        await ConcurrencyExceptionHelper.SaveChangesAsync(_unitOfWork, cancellationToken);

        var terms = source.Terms.Select(t => new AcademicTerm
        {
            TenantId = TenantId,
            AcademicYearId = newYear.Id,
            Name = t.Name,
            StartDate = AcademicYearCloneHelper.ShiftDate(t.StartDate, source.StartDate, request.StartDate),
            EndDate = AcademicYearCloneHelper.ShiftDate(t.EndDate, source.StartDate, request.StartDate),
            Sequence = t.Sequence,
        }).ToList();
        if (terms.Count > 0)
            await _repository.AddRangeAsync(terms, cancellationToken);

        var workingDays = source.WorkingDays.Select(w => new WorkingDay
        {
            TenantId = TenantId,
            AcademicYearId = newYear.Id,
            DayOfWeek = w.DayOfWeek,
            IsWorking = w.IsWorking,
        }).ToList();
        if (workingDays.Count > 0)
            await _repository.AddRangeAsync(workingDays, cancellationToken);

        var holidays = source.Holidays.Select(h => new Holiday
        {
            TenantId = TenantId,
            AcademicYearId = newYear.Id,
            Name = h.Name,
            Date = AcademicYearCloneHelper.ShiftDate(h.Date, source.StartDate, request.StartDate),
            HolidayType = h.HolidayType,
            Description = h.Description,
            HolidayTypeCatalogId = h.HolidayTypeCatalogId,
            IsWorkingDayOverride = h.IsWorkingDayOverride,
            RequiresRescheduling = h.RequiresRescheduling,
            Colour = h.Colour,
            Priority = h.Priority,
        }).ToList();
        if (holidays.Count > 0)
            await _repository.AddRangeAsync(holidays, cancellationToken);

        await ConcurrencyExceptionHelper.SaveChangesAsync(_unitOfWork, cancellationToken);
        return MapYear(newYear);
    }

    public async Task<AcademicTermDto> CreateTermAsync(CreateAcademicTermRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureYearExistsAsync(request.AcademicYearId, cancellationToken);
        if (request.EndDate <= request.StartDate)
            throw new DomainException("Term end date must be after start date.");

        var entity = new AcademicTerm
        {
            TenantId = TenantId,
            AcademicYearId = request.AcademicYearId,
            Name = request.Name.Trim(),
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            Sequence = request.Sequence,
        };
        await _repository.AddTermAsync(entity, cancellationToken);
        await ConcurrencyExceptionHelper.SaveChangesAsync(_unitOfWork, cancellationToken);
        return MapTerm(entity);
    }

    public async Task<AcademicTermDto> UpdateTermAsync(UpdateAcademicTermRequest request, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetTermByIdAsync(TenantId, request.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"Academic term '{request.Id}' was not found.");
        await EnsureYearExistsAsync(request.AcademicYearId, cancellationToken);
        if (request.EndDate <= request.StartDate)
            throw new DomainException("Term end date must be after start date.");

        entity.AcademicYearId = request.AcademicYearId;
        entity.Name = request.Name.Trim();
        entity.StartDate = request.StartDate;
        entity.EndDate = request.EndDate;
        entity.Sequence = request.Sequence;
        await ConcurrencyExceptionHelper.SaveChangesAsync(_unitOfWork, cancellationToken);
        return MapTerm(entity);
    }

    public async Task DeleteTermAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetTermByIdAsync(TenantId, id, cancellationToken)
            ?? throw new KeyNotFoundException($"Academic term '{id}' was not found.");
        entity.IsDeleted = true;
        await ConcurrencyExceptionHelper.SaveChangesAsync(_unitOfWork, cancellationToken);
    }

    public async Task<WorkingDayDto> UpsertWorkingDayAsync(UpsertWorkingDayRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureYearExistsAsync(request.AcademicYearId, cancellationToken);
        if (request.DayOfWeek > 6)
            throw new DomainException("DayOfWeek must be between 0 (Sunday) and 6 (Saturday).");

        WorkingDay entity;
        if (request.Id.HasValue)
        {
            entity = await _repository.GetWorkingDayByIdAsync(TenantId, request.Id.Value, cancellationToken)
                ?? throw new KeyNotFoundException($"Working day '{request.Id}' was not found.");
        }
        else
        {
            entity = new WorkingDay { TenantId = TenantId };
            await _repository.AddWorkingDayAsync(entity, cancellationToken);
        }

        entity.AcademicYearId = request.AcademicYearId;
        entity.DayOfWeek = request.DayOfWeek;
        entity.IsWorking = request.IsWorking;
        await ConcurrencyExceptionHelper.SaveChangesAsync(_unitOfWork, cancellationToken);
        return MapWorkingDay(entity);
    }

    public async Task DeleteWorkingDayAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetWorkingDayByIdAsync(TenantId, id, cancellationToken)
            ?? throw new KeyNotFoundException($"Working day '{id}' was not found.");
        entity.IsDeleted = true;
        await ConcurrencyExceptionHelper.SaveChangesAsync(_unitOfWork, cancellationToken);
    }

    public async Task<HolidayDto> CreateHolidayAsync(CreateHolidayRequest request, CancellationToken cancellationToken = default)
    {
        await _createHolidayValidator.ValidateAndThrowAsync(request, cancellationToken);
        await EnsureYearExistsAsync(request.AcademicYearId, cancellationToken);

        var entity = new Holiday
        {
            TenantId = TenantId,
            AcademicYearId = request.AcademicYearId,
            Name = request.Name.Trim(),
            Date = request.Date,
            HolidayType = request.HolidayType,
            Description = request.Description?.Trim(),
            HolidayTypeCatalogId = request.HolidayTypeCatalogId,
            IsWorkingDayOverride = request.IsWorkingDayOverride,
            RequiresRescheduling = request.RequiresRescheduling,
            Colour = request.Colour?.Trim(),
            Priority = request.Priority,
        };
        await _repository.AddHolidayAsync(entity, cancellationToken);
        await ConcurrencyExceptionHelper.SaveChangesAsync(_unitOfWork, cancellationToken);
        return MapHoliday(entity);
    }

    public async Task<HolidayDto> UpdateHolidayAsync(UpdateHolidayRequest request, CancellationToken cancellationToken = default)
    {
        await _updateHolidayValidator.ValidateAndThrowAsync(request, cancellationToken);
        var entity = await _repository.GetHolidayByIdAsync(TenantId, request.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"Holiday '{request.Id}' was not found.");
        await EnsureYearExistsAsync(request.AcademicYearId, cancellationToken);

        entity.AcademicYearId = request.AcademicYearId;
        entity.Name = request.Name.Trim();
        entity.Date = request.Date;
        entity.HolidayType = request.HolidayType;
        entity.Description = request.Description?.Trim();
        entity.HolidayTypeCatalogId = request.HolidayTypeCatalogId;
        entity.IsWorkingDayOverride = request.IsWorkingDayOverride;
        entity.RequiresRescheduling = request.RequiresRescheduling;
        entity.Colour = request.Colour?.Trim();
        entity.Priority = request.Priority;
        await ConcurrencyExceptionHelper.SaveChangesAsync(_unitOfWork, cancellationToken);
        return MapHoliday(entity);
    }

    public async Task DeleteHolidayAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetHolidayByIdAsync(TenantId, id, cancellationToken)
            ?? throw new KeyNotFoundException($"Holiday '{id}' was not found.");
        entity.IsDeleted = true;
        await ConcurrencyExceptionHelper.SaveChangesAsync(_unitOfWork, cancellationToken);
    }

    private async Task ClearCurrentYearAsync(CancellationToken cancellationToken)
    {
        var current = await _context.SchedulingAcademicYears
            .Where(x => x.TenantId == TenantId && x.IsCurrent)
            .ToListAsync(cancellationToken);
        foreach (var year in current)
            year.IsCurrent = false;
    }

    private async Task EnsureYearExistsAsync(int academicYearId, CancellationToken cancellationToken)
    {
        if (!await _context.SchedulingAcademicYears.AnyAsync(x => x.TenantId == TenantId && x.Id == academicYearId, cancellationToken))
            throw new KeyNotFoundException($"Academic year '{academicYearId}' was not found.");
    }

    private static AcademicYearDto MapYear(AcademicYear x) => new()
    {
        Id = x.Id, Name = x.Name, Code = x.Code, StartDate = x.StartDate, EndDate = x.EndDate, IsCurrent = x.IsCurrent,
    };

    private static AcademicTermDto MapTerm(AcademicTerm x) => new()
    {
        Id = x.Id, AcademicYearId = x.AcademicYearId, Name = x.Name, StartDate = x.StartDate, EndDate = x.EndDate, Sequence = x.Sequence,
    };

    private static WorkingDayDto MapWorkingDay(WorkingDay x) => new()
    {
        Id = x.Id, AcademicYearId = x.AcademicYearId, DayOfWeek = x.DayOfWeek, IsWorking = x.IsWorking,
    };

    private static HolidayDto MapHoliday(Holiday x) => new()
    {
        Id = x.Id,
        AcademicYearId = x.AcademicYearId,
        Name = x.Name,
        Date = x.Date,
        HolidayType = x.HolidayType,
        Description = x.Description,
        HolidayTypeCatalogId = x.HolidayTypeCatalogId,
        IsWorkingDayOverride = x.IsWorkingDayOverride,
        RequiresRescheduling = x.RequiresRescheduling,
        Colour = x.Colour,
        Priority = x.Priority,
    };
}
