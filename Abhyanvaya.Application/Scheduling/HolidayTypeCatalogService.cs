using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.Common.Interfaces.Scheduling;
using Abhyanvaya.Application.DTOs.Scheduling;
using Abhyanvaya.Application.Internal;
using Abhyanvaya.Domain.Entities.Scheduling;
using Abhyanvaya.Domain.Exceptions;
using FluentValidation;

namespace Abhyanvaya.Application.Scheduling;

public sealed class HolidayTypeCatalogService : IHolidayTypeCatalogService
{
    private static readonly (string Code, string Name, string Colour, int Priority, int SortOrder)[] DefaultTypes =
    [
        ("NationalHoliday", "National Holiday", "#FF0000", 1, 1),
        ("Festival", "Festival", "#FF6600", 2, 2),
        ("UniversityHoliday", "University Holiday", "#9900CC", 3, 3),
        ("CollegeHoliday", "College Holiday", "#0066CC", 4, 4),
        ("DepartmentHoliday", "Department Holiday", "#009999", 5, 5),
        ("Examination", "Examination", "#CC0000", 6, 6),
        ("Maintenance", "Maintenance", "#666666", 7, 7),
        ("EmergencyClosure", "Emergency Closure", "#333333", 8, 8),
        ("WeatherClosure", "Weather Closure", "#6699FF", 9, 9),
        ("OptionalHoliday", "Optional Holiday", "#FFCC00", 10, 10),
        ("TrainingDay", "Training Day", "#00CC66", 11, 11),
    ];

    private readonly IHolidayTypeCatalogRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;
    private readonly IValidator<CreateHolidayTypeCatalogRequest> _createValidator;
    private readonly IValidator<UpdateHolidayTypeCatalogRequest> _updateValidator;

    public HolidayTypeCatalogService(
        IHolidayTypeCatalogRepository repository,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUser,
        IValidator<CreateHolidayTypeCatalogRequest> createValidator,
        IValidator<UpdateHolidayTypeCatalogRequest> updateValidator)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    private int TenantId => _currentUser.TenantId;

    public async Task<IReadOnlyList<HolidayTypeCatalogDto>> ListAsync(bool? isActive, CancellationToken cancellationToken = default)
    {
        await EnsureDefaultsAsync(cancellationToken);
        var items = await _repository.ListAsync(TenantId, isActive, cancellationToken);
        return items.Select(Map).ToList();
    }

    public async Task<HolidayTypeCatalogDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetByIdAsync(TenantId, id, cancellationToken);
        return entity is null ? null : Map(entity);
    }

    public async Task<HolidayTypeCatalogDto> CreateAsync(CreateHolidayTypeCatalogRequest request, CancellationToken cancellationToken = default)
    {
        await _createValidator.ValidateAndThrowAsync(request, cancellationToken);
        if (await _repository.CodeExistsAsync(TenantId, request.Code.Trim(), null, cancellationToken))
            throw new DomainException($"Holiday type code '{request.Code}' already exists.");

        var entity = new HolidayTypeCatalog
        {
            TenantId = TenantId,
            Code = request.Code.Trim(),
            Name = request.Name.Trim(),
            Colour = request.Colour.Trim(),
            Priority = request.Priority,
            SortOrder = request.SortOrder,
            IsActive = request.IsActive,
        };
        await _repository.AddAsync(entity, cancellationToken);
        await ConcurrencyExceptionHelper.SaveChangesAsync(_unitOfWork, cancellationToken);
        return Map(entity);
    }

    public async Task<HolidayTypeCatalogDto> UpdateAsync(UpdateHolidayTypeCatalogRequest request, CancellationToken cancellationToken = default)
    {
        await _updateValidator.ValidateAndThrowAsync(request, cancellationToken);
        var entity = await _repository.GetByIdAsync(TenantId, request.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"Holiday type '{request.Id}' was not found.");

        if (await _repository.CodeExistsAsync(TenantId, request.Code.Trim(), request.Id, cancellationToken))
            throw new DomainException($"Holiday type code '{request.Code}' already exists.");

        entity.Code = request.Code.Trim();
        entity.Name = request.Name.Trim();
        entity.Colour = request.Colour.Trim();
        entity.Priority = request.Priority;
        entity.SortOrder = request.SortOrder;
        entity.IsActive = request.IsActive;
        await ConcurrencyExceptionHelper.SaveChangesAsync(_unitOfWork, cancellationToken);
        return Map(entity);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetByIdAsync(TenantId, id, cancellationToken)
            ?? throw new KeyNotFoundException($"Holiday type '{id}' was not found.");
        entity.IsDeleted = true;
        await ConcurrencyExceptionHelper.SaveChangesAsync(_unitOfWork, cancellationToken);
    }

    public async Task EnsureDefaultsAsync(CancellationToken cancellationToken = default)
    {
        var existing = await _repository.ListAsync(TenantId, null, cancellationToken);
        var existingCodes = existing.Select(x => x.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var toAdd = DefaultTypes
            .Where(d => !existingCodes.Contains(d.Code))
            .Select(d => new HolidayTypeCatalog
            {
                TenantId = TenantId,
                Code = d.Code,
                Name = d.Name,
                Colour = d.Colour,
                Priority = d.Priority,
                SortOrder = d.SortOrder,
                IsActive = true,
            })
            .ToList();

        if (toAdd.Count == 0)
            return;

        await _repository.AddRangeAsync(toAdd, cancellationToken);
        await ConcurrencyExceptionHelper.SaveChangesAsync(_unitOfWork, cancellationToken);
    }

    private static HolidayTypeCatalogDto Map(HolidayTypeCatalog x) => new()
    {
        Id = x.Id,
        Code = x.Code,
        Name = x.Name,
        Colour = x.Colour,
        Priority = x.Priority,
        SortOrder = x.SortOrder,
        IsActive = x.IsActive,
    };
}
