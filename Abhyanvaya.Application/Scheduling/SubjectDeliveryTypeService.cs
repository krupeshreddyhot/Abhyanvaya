using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.Common.Interfaces.Scheduling;
using Abhyanvaya.Application.DTOs.Scheduling;
using Abhyanvaya.Application.Internal;
using Abhyanvaya.Domain.Entities.Scheduling;
using Abhyanvaya.Domain.Exceptions;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Abhyanvaya.Application.Scheduling;

public sealed class SubjectDeliveryTypeService : ISubjectDeliveryTypeService
{
    private static readonly (string Code, string Name, int SortOrder)[] DefaultTypes =
    [
        ("Theory", "Theory", 1),
        ("Laboratory", "Laboratory", 2),
        ("Tutorial", "Tutorial", 3),
        ("Workshop", "Workshop", 4),
        ("Seminar", "Seminar", 5),
        ("Project", "Project", 6),
        ("Internship", "Internship", 7),
        ("FieldWork", "Field Work", 8),
        ("Online", "Online", 9),
        ("Hybrid", "Hybrid", 10),
        ("Blended", "Blended", 11),
        ("SelfStudy", "Self Study", 12),
    ];

    private readonly ISubjectDeliveryTypeRepository _repository;
    private readonly IRoomFeatureRepository _roomFeatureRepository;
    private readonly IApplicationDbContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;
    private readonly IValidator<CreateSubjectDeliveryTypeRequest> _createValidator;
    private readonly IValidator<UpdateSubjectDeliveryTypeRequest> _updateValidator;
    private readonly IValidator<UpdateSubjectDeliveryFieldsRequest> _updateSubjectValidator;

    public SubjectDeliveryTypeService(
        ISubjectDeliveryTypeRepository repository,
        IRoomFeatureRepository roomFeatureRepository,
        IApplicationDbContext context,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUser,
        IValidator<CreateSubjectDeliveryTypeRequest> createValidator,
        IValidator<UpdateSubjectDeliveryTypeRequest> updateValidator,
        IValidator<UpdateSubjectDeliveryFieldsRequest> updateSubjectValidator)
    {
        _repository = repository;
        _roomFeatureRepository = roomFeatureRepository;
        _context = context;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _updateSubjectValidator = updateSubjectValidator;
    }

    private int TenantId => _currentUser.TenantId;

    public async Task<IReadOnlyList<SubjectDeliveryTypeDto>> ListAsync(bool? isActive, CancellationToken cancellationToken = default)
    {
        await EnsureDefaultsAsync(cancellationToken);
        var items = await _repository.ListAsync(TenantId, isActive, cancellationToken);
        return items.Select(Map).ToList();
    }

    public async Task<SubjectDeliveryTypeDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetByIdAsync(TenantId, id, cancellationToken);
        return entity is null ? null : Map(entity);
    }

    public async Task<SubjectDeliveryTypeDto> CreateAsync(CreateSubjectDeliveryTypeRequest request, CancellationToken cancellationToken = default)
    {
        await _createValidator.ValidateAndThrowAsync(request, cancellationToken);
        if (await _repository.CodeExistsAsync(TenantId, request.Code.Trim(), null, cancellationToken))
            throw new DomainException($"Subject delivery type code '{request.Code}' already exists.");

        var entity = new SubjectDeliveryType
        {
            TenantId = TenantId,
            Code = request.Code.Trim(),
            Name = request.Name.Trim(),
            SortOrder = request.SortOrder,
            IsActive = request.IsActive,
        };
        await _repository.AddAsync(entity, cancellationToken);
        await ConcurrencyExceptionHelper.SaveChangesAsync(_unitOfWork, cancellationToken);
        return Map(entity);
    }

    public async Task<SubjectDeliveryTypeDto> UpdateAsync(UpdateSubjectDeliveryTypeRequest request, CancellationToken cancellationToken = default)
    {
        await _updateValidator.ValidateAndThrowAsync(request, cancellationToken);
        var entity = await _repository.GetByIdAsync(TenantId, request.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"Subject delivery type '{request.Id}' was not found.");

        if (await _repository.CodeExistsAsync(TenantId, request.Code.Trim(), request.Id, cancellationToken))
            throw new DomainException($"Subject delivery type code '{request.Code}' already exists.");

        entity.Code = request.Code.Trim();
        entity.Name = request.Name.Trim();
        entity.SortOrder = request.SortOrder;
        entity.IsActive = request.IsActive;
        await ConcurrencyExceptionHelper.SaveChangesAsync(_unitOfWork, cancellationToken);
        return Map(entity);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetByIdAsync(TenantId, id, cancellationToken)
            ?? throw new KeyNotFoundException($"Subject delivery type '{id}' was not found.");
        entity.IsDeleted = true;
        await ConcurrencyExceptionHelper.SaveChangesAsync(_unitOfWork, cancellationToken);
    }

    public async Task EnsureDefaultsAsync(CancellationToken cancellationToken = default)
    {
        var existing = await _repository.ListAsync(TenantId, null, cancellationToken);
        var existingCodes = existing.Select(x => x.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var toAdd = DefaultTypes
            .Where(d => !existingCodes.Contains(d.Code))
            .Select(d => new SubjectDeliveryType
            {
                TenantId = TenantId,
                Code = d.Code,
                Name = d.Name,
                SortOrder = d.SortOrder,
                IsActive = true,
            })
            .ToList();

        if (toAdd.Count == 0)
            return;

        await _repository.AddRangeAsync(toAdd, cancellationToken);
        await ConcurrencyExceptionHelper.SaveChangesAsync(_unitOfWork, cancellationToken);
    }

    public async Task UpdateSubjectDeliveryFieldsAsync(UpdateSubjectDeliveryFieldsRequest request, CancellationToken cancellationToken = default)
    {
        await _updateSubjectValidator.ValidateAndThrowAsync(request, cancellationToken);

        var subject = await _context.Subjects
            .FirstOrDefaultAsync(x => x.TenantId == TenantId && x.Id == request.SubjectId, cancellationToken)
            ?? throw new KeyNotFoundException($"Subject '{request.SubjectId}' was not found.");

        var deliveryType = await _repository.GetByIdAsync(TenantId, request.DeliveryTypeId, cancellationToken)
            ?? throw new KeyNotFoundException($"Subject delivery type '{request.DeliveryTypeId}' was not found.");

        if (request.PreferredRoomFeatureId.HasValue)
        {
            var feature = await _roomFeatureRepository.GetFeatureByIdAsync(TenantId, request.PreferredRoomFeatureId.Value, cancellationToken)
                ?? throw new KeyNotFoundException($"Room feature '{request.PreferredRoomFeatureId}' was not found.");
            subject.PreferredRoomFeatureId = feature.Id;
        }
        else
        {
            subject.PreferredRoomFeatureId = null;
        }

        if (!SubjectDeliveryValidationHelper.ValidateRoomTypeForDelivery(deliveryType.Code, request.RequiresRoomType, out var error))
            throw new DomainException(error!);

        subject.DeliveryTypeId = request.DeliveryTypeId;
        subject.RequiresAttendance = request.RequiresAttendance;
        subject.ExpectedCapacity = request.ExpectedCapacity;
        subject.RequiresRoomType = request.RequiresRoomType;
        await ConcurrencyExceptionHelper.SaveChangesAsync(_unitOfWork, cancellationToken);
    }

    private static SubjectDeliveryTypeDto Map(SubjectDeliveryType x) => new()
    {
        Id = x.Id,
        Code = x.Code,
        Name = x.Name,
        SortOrder = x.SortOrder,
        IsActive = x.IsActive,
    };
}
