using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.Common.Interfaces.Scheduling;
using Abhyanvaya.Application.DTOs.Scheduling;
using Abhyanvaya.Application.Internal;
using Abhyanvaya.Domain.Entities.Scheduling;
using Abhyanvaya.Domain.Exceptions;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Abhyanvaya.Application.Scheduling;

public sealed class RoomFeatureService : IRoomFeatureService
{
    private static readonly (string Code, string Name, string Category, int SortOrder)[] DefaultFeatures =
    [
        ("Projector", "Projector", "Equipment", 1),
        ("SmartBoard", "Smart Board", "AV", 2),
        ("AICamera", "AI Camera", "Equipment", 3),
        ("WiFi", "WiFi", "Equipment", 4),
        ("AirConditioning", "Air Conditioning", "Equipment", 5),
        ("Recording", "Recording", "AV", 6),
        ("Microphone", "Microphone", "AV", 7),
        ("ComputerLab", "Computer Lab", "Lab", 8),
        ("PhysicsLab", "Physics Lab", "Lab", 9),
        ("ChemistryLab", "Chemistry Lab", "Lab", 10),
        ("CommerceLab", "Commerce Lab", "Lab", 11),
        ("ElectronicsLab", "Electronics Lab", "Lab", 12),
        ("Seminar", "Seminar", "Other", 13),
        ("Accessibility", "Accessibility", "Accessibility", 14),
        ("WheelchairAccess", "Wheelchair Access", "Accessibility", 15),
        ("DualDisplay", "Dual Display", "AV", 16),
        ("InteractivePanel", "Interactive Panel", "AV", 17),
    ];

    private readonly IRoomFeatureRepository _repository;
    private readonly IApplicationDbContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;
    private readonly IValidator<CreateRoomFeatureRequest> _createValidator;
    private readonly IValidator<UpdateRoomFeatureRequest> _updateValidator;
    private readonly IValidator<AssignRoomFeatureRequest> _assignValidator;
    private readonly IValidator<CloneRoomFeatureAssignmentsRequest> _cloneValidator;

    public RoomFeatureService(
        IRoomFeatureRepository repository,
        IApplicationDbContext context,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUser,
        IValidator<CreateRoomFeatureRequest> createValidator,
        IValidator<UpdateRoomFeatureRequest> updateValidator,
        IValidator<AssignRoomFeatureRequest> assignValidator,
        IValidator<CloneRoomFeatureAssignmentsRequest> cloneValidator)
    {
        _repository = repository;
        _context = context;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _assignValidator = assignValidator;
        _cloneValidator = cloneValidator;
    }

    private int TenantId => _currentUser.TenantId;

    public async Task<IReadOnlyList<RoomFeatureDto>> ListFeaturesAsync(string? category, bool? isActive, CancellationToken cancellationToken = default)
    {
        await EnsureDefaultsAsync(cancellationToken);
        var items = await _repository.ListFeaturesAsync(TenantId, category, isActive, cancellationToken);
        return items.Select(MapFeature).ToList();
    }

    public async Task<RoomFeatureDto?> GetFeatureByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetFeatureByIdAsync(TenantId, id, cancellationToken);
        return entity is null ? null : MapFeature(entity);
    }

    public async Task<RoomFeatureDto> CreateFeatureAsync(CreateRoomFeatureRequest request, CancellationToken cancellationToken = default)
    {
        await _createValidator.ValidateAndThrowAsync(request, cancellationToken);
        if (await _repository.FeatureCodeExistsAsync(TenantId, request.Code.Trim(), null, cancellationToken))
            throw new DomainException($"Room feature code '{request.Code}' already exists.");

        var entity = new RoomFeature
        {
            TenantId = TenantId,
            Code = request.Code.Trim(),
            Name = request.Name.Trim(),
            Category = request.Category.Trim(),
            SortOrder = request.SortOrder,
            IsActive = request.IsActive,
        };
        await _repository.AddFeatureAsync(entity, cancellationToken);
        await ConcurrencyExceptionHelper.SaveChangesAsync(_unitOfWork, cancellationToken);
        return MapFeature(entity);
    }

    public async Task<RoomFeatureDto> UpdateFeatureAsync(UpdateRoomFeatureRequest request, CancellationToken cancellationToken = default)
    {
        await _updateValidator.ValidateAndThrowAsync(request, cancellationToken);
        var entity = await _repository.GetFeatureByIdAsync(TenantId, request.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"Room feature '{request.Id}' was not found.");

        if (await _repository.FeatureCodeExistsAsync(TenantId, request.Code.Trim(), request.Id, cancellationToken))
            throw new DomainException($"Room feature code '{request.Code}' already exists.");

        entity.Code = request.Code.Trim();
        entity.Name = request.Name.Trim();
        entity.Category = request.Category.Trim();
        entity.SortOrder = request.SortOrder;
        entity.IsActive = request.IsActive;
        await ConcurrencyExceptionHelper.SaveChangesAsync(_unitOfWork, cancellationToken);
        return MapFeature(entity);
    }

    public async Task DeleteFeatureAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetFeatureByIdAsync(TenantId, id, cancellationToken)
            ?? throw new KeyNotFoundException($"Room feature '{id}' was not found.");
        entity.IsDeleted = true;
        await ConcurrencyExceptionHelper.SaveChangesAsync(_unitOfWork, cancellationToken);
    }

    public async Task EnsureDefaultsAsync(CancellationToken cancellationToken = default)
    {
        var existing = await _repository.ListFeaturesAsync(TenantId, null, null, cancellationToken);
        var existingCodes = existing.Select(x => x.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var toAdd = DefaultFeatures
            .Where(d => !existingCodes.Contains(d.Code))
            .Select(d => new RoomFeature
            {
                TenantId = TenantId,
                Code = d.Code,
                Name = d.Name,
                Category = d.Category,
                SortOrder = d.SortOrder,
                IsActive = true,
            })
            .ToList();

        if (toAdd.Count == 0)
            return;

        await _repository.AddFeaturesAsync(toAdd, cancellationToken);
        await ConcurrencyExceptionHelper.SaveChangesAsync(_unitOfWork, cancellationToken);
    }

    public async Task<IReadOnlyList<RoomFeatureAssignmentDto>> ListAssignmentsByRoomAsync(int roomId, CancellationToken cancellationToken = default)
    {
        await EnsureRoomExistsAsync(roomId, cancellationToken);
        var items = await _repository.ListAssignmentsByRoomAsync(TenantId, roomId, cancellationToken);
        return items.Select(MapAssignment).ToList();
    }

    public async Task<RoomFeatureAssignmentDto> AssignFeatureAsync(int roomId, AssignRoomFeatureRequest request, CancellationToken cancellationToken = default)
    {
        await _assignValidator.ValidateAndThrowAsync(request, cancellationToken);
        await EnsureRoomExistsAsync(roomId, cancellationToken);
        var feature = await _repository.GetFeatureByIdAsync(TenantId, request.RoomFeatureId, cancellationToken)
            ?? throw new KeyNotFoundException($"Room feature '{request.RoomFeatureId}' was not found.");

        if (await _repository.AssignmentExistsAsync(TenantId, roomId, request.RoomFeatureId, cancellationToken))
            throw new DomainException("This feature is already assigned to the room.");

        var entity = new RoomFeatureAssignment
        {
            TenantId = TenantId,
            RoomId = roomId,
            RoomFeatureId = request.RoomFeatureId,
        };
        await _repository.AddAssignmentAsync(entity, cancellationToken);
        await ConcurrencyExceptionHelper.SaveChangesAsync(_unitOfWork, cancellationToken);
        entity.RoomFeature = feature;
        return MapAssignment(entity);
    }

    public async Task UnassignFeatureAsync(int roomId, int roomFeatureId, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetAssignmentAsync(TenantId, roomId, roomFeatureId, cancellationToken)
            ?? throw new KeyNotFoundException($"Feature assignment for room '{roomId}' and feature '{roomFeatureId}' was not found.");
        entity.IsDeleted = true;
        await ConcurrencyExceptionHelper.SaveChangesAsync(_unitOfWork, cancellationToken);
    }

    public async Task<IReadOnlyList<RoomFeatureAssignmentDto>> CloneAssignmentsAsync(CloneRoomFeatureAssignmentsRequest request, CancellationToken cancellationToken = default)
    {
        await _cloneValidator.ValidateAndThrowAsync(request, cancellationToken);
        await EnsureRoomExistsAsync(request.FromRoomId, cancellationToken);
        await EnsureRoomExistsAsync(request.ToRoomId, cancellationToken);

        var source = await _repository.ListAssignmentsByRoomAsync(TenantId, request.FromRoomId, cancellationToken);
        var targetExisting = (await _repository.ListAssignmentsByRoomAsync(TenantId, request.ToRoomId, cancellationToken))
            .Select(x => x.RoomFeatureId)
            .ToHashSet();

        var toAdd = source
            .Where(x => !targetExisting.Contains(x.RoomFeatureId))
            .Select(x => new RoomFeatureAssignment
            {
                TenantId = TenantId,
                RoomId = request.ToRoomId,
                RoomFeatureId = x.RoomFeatureId,
            })
            .ToList();

        if (toAdd.Count > 0)
        {
            await _repository.AddAssignmentsAsync(toAdd, cancellationToken);
            await ConcurrencyExceptionHelper.SaveChangesAsync(_unitOfWork, cancellationToken);
        }

        var result = await _repository.ListAssignmentsByRoomAsync(TenantId, request.ToRoomId, cancellationToken);
        return result.Select(MapAssignment).ToList();
    }

    private async Task EnsureRoomExistsAsync(int roomId, CancellationToken cancellationToken)
    {
        if (!await _context.SchedulingRooms.AnyAsync(x => x.TenantId == TenantId && x.Id == roomId, cancellationToken))
            throw new KeyNotFoundException($"Room '{roomId}' was not found.");
    }

    private static RoomFeatureDto MapFeature(RoomFeature x) => new()
    {
        Id = x.Id,
        Code = x.Code,
        Name = x.Name,
        Category = x.Category,
        SortOrder = x.SortOrder,
        IsActive = x.IsActive,
    };

    private static RoomFeatureAssignmentDto MapAssignment(RoomFeatureAssignment x) => new()
    {
        Id = x.Id,
        RoomId = x.RoomId,
        RoomFeatureId = x.RoomFeatureId,
        FeatureCode = x.RoomFeature?.Code ?? string.Empty,
        FeatureName = x.RoomFeature?.Name ?? string.Empty,
        FeatureCategory = x.RoomFeature?.Category ?? string.Empty,
    };
}
