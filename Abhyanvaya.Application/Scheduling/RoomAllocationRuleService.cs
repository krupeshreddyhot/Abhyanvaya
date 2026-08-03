using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.Common.Interfaces.Scheduling;
using Abhyanvaya.Application.DTOs.Scheduling;
using Abhyanvaya.Application.Internal;
using Abhyanvaya.Domain.Entities.Scheduling;

namespace Abhyanvaya.Application.Scheduling;

public sealed class RoomAllocationRuleService : IRoomAllocationRuleService
{
    private readonly IRoomAllocationRuleRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;

    public RoomAllocationRuleService(
        IRoomAllocationRuleRepository repository,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUser)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
    }

    private int TenantId => _currentUser.TenantId;

    public async Task<IReadOnlyList<RoomAllocationRuleDto>> ListAsync(int? academicYearId, CancellationToken cancellationToken = default)
    {
        var items = await _repository.ListAsync(TenantId, academicYearId, cancellationToken);
        return items.Select(Map).ToList();
    }

    public async Task<RoomAllocationRuleDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetByIdAsync(TenantId, id, cancellationToken);
        return entity is null ? null : Map(entity);
    }

    public async Task<RoomAllocationRuleDto> CreateAsync(CreateRoomAllocationRuleRequest request, CancellationToken cancellationToken = default)
    {
        var entity = MapToEntity(request);
        entity.TenantId = TenantId;
        await _repository.AddAsync(entity, cancellationToken);
        await ConcurrencyExceptionHelper.SaveChangesAsync(_unitOfWork, cancellationToken);
        return Map(entity);
    }

    public async Task<RoomAllocationRuleDto> UpdateAsync(UpdateRoomAllocationRuleRequest request, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetByIdAsync(TenantId, request.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"Room allocation rule '{request.Id}' was not found.");
        ApplyRequest(entity, request);
        await ConcurrencyExceptionHelper.SaveChangesAsync(_unitOfWork, cancellationToken);
        return Map(entity);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetByIdAsync(TenantId, id, cancellationToken)
            ?? throw new KeyNotFoundException($"Room allocation rule '{id}' was not found.");
        entity.IsDeleted = true;
        await ConcurrencyExceptionHelper.SaveChangesAsync(_unitOfWork, cancellationToken);
    }

    private static RoomAllocationRule MapToEntity(CreateRoomAllocationRuleRequest request) => new()
    {
        Name = request.Name.Trim(),
        AcademicYearId = request.AcademicYearId,
        RoomType = request.RoomType,
        MinCapacity = request.MinCapacity,
        MaxCapacity = request.MaxCapacity,
        DepartmentId = request.DepartmentId,
        CourseId = request.CourseId,
        RequireComputerLab = request.RequireComputerLab,
        RequireScienceLab = request.RequireScienceLab,
        RequireCommerceLab = request.RequireCommerceLab,
        RequireAiCamera = request.RequireAiCamera,
        RequireProjector = request.RequireProjector,
        RequireSmartBoard = request.RequireSmartBoard,
        PreferredRoomId = request.PreferredRoomId,
        Priority = request.Priority,
        Notes = request.Notes?.Trim(),
    };

    private static void ApplyRequest(RoomAllocationRule entity, UpdateRoomAllocationRuleRequest request)
    {
        entity.Name = request.Name.Trim();
        entity.AcademicYearId = request.AcademicYearId;
        entity.RoomType = request.RoomType;
        entity.MinCapacity = request.MinCapacity;
        entity.MaxCapacity = request.MaxCapacity;
        entity.DepartmentId = request.DepartmentId;
        entity.CourseId = request.CourseId;
        entity.RequireComputerLab = request.RequireComputerLab;
        entity.RequireScienceLab = request.RequireScienceLab;
        entity.RequireCommerceLab = request.RequireCommerceLab;
        entity.RequireAiCamera = request.RequireAiCamera;
        entity.RequireProjector = request.RequireProjector;
        entity.RequireSmartBoard = request.RequireSmartBoard;
        entity.PreferredRoomId = request.PreferredRoomId;
        entity.Priority = request.Priority;
        entity.Notes = request.Notes?.Trim();
    }

    private static RoomAllocationRuleDto Map(RoomAllocationRule x) => new()
    {
        Id = x.Id,
        Name = x.Name,
        AcademicYearId = x.AcademicYearId,
        RoomType = x.RoomType,
        MinCapacity = x.MinCapacity,
        MaxCapacity = x.MaxCapacity,
        DepartmentId = x.DepartmentId,
        CourseId = x.CourseId,
        RequireComputerLab = x.RequireComputerLab,
        RequireScienceLab = x.RequireScienceLab,
        RequireCommerceLab = x.RequireCommerceLab,
        RequireAiCamera = x.RequireAiCamera,
        RequireProjector = x.RequireProjector,
        RequireSmartBoard = x.RequireSmartBoard,
        PreferredRoomId = x.PreferredRoomId,
        Priority = x.Priority,
        Notes = x.Notes,
    };
}
