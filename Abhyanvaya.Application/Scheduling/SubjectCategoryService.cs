using Abhyanvaya.Application.Common.Interfaces;

using Abhyanvaya.Application.Common.Interfaces.Scheduling;

using Abhyanvaya.Application.DTOs.Scheduling;

using Abhyanvaya.Application.Internal;

using Abhyanvaya.Domain.Entities;

using Abhyanvaya.Domain.Entities.Scheduling;

using Abhyanvaya.Domain.Exceptions;

using FluentValidation;

using Microsoft.EntityFrameworkCore;



namespace Abhyanvaya.Application.Scheduling;



public sealed class SubjectCategoryService : ISubjectCategoryService

{

    private static readonly (string Code, string Name, int SortOrder)[] DefaultCategories =

    [

        ("Theory", "Theory", 1),

        ("Laboratory", "Laboratory", 2),

        ("Tutorial", "Tutorial", 3),

        ("Seminar", "Seminar", 4),

        ("Workshop", "Workshop", 5),

        ("Project", "Project", 6),

        ("Internship", "Internship", 7),

        ("FieldWork", "Field Work", 8),

        ("Elective", "Elective", 9),

        ("Language", "Language", 10),

        ("SkillDevelopment", "Skill Development", 11),

    ];



    private readonly ISubjectCategoryRepository _repository;

    private readonly IApplicationDbContext _context;

    private readonly IUnitOfWork _unitOfWork;

    private readonly ICurrentUserService _currentUser;

    private readonly IValidator<CreateSubjectCategoryRequest> _createValidator;

    private readonly IValidator<UpdateSubjectCategoryRequest> _updateValidator;

    private readonly IValidator<UpdateSubjectSchedulingCategoryRequest> _updateSubjectValidator;



    public SubjectCategoryService(

        ISubjectCategoryRepository repository,

        IApplicationDbContext context,

        IUnitOfWork unitOfWork,

        ICurrentUserService currentUser,

        IValidator<CreateSubjectCategoryRequest> createValidator,

        IValidator<UpdateSubjectCategoryRequest> updateValidator,

        IValidator<UpdateSubjectSchedulingCategoryRequest> updateSubjectValidator)

    {

        _repository = repository;

        _context = context;

        _unitOfWork = unitOfWork;

        _currentUser = currentUser;

        _createValidator = createValidator;

        _updateValidator = updateValidator;

        _updateSubjectValidator = updateSubjectValidator;

    }



    private int TenantId => _currentUser.TenantId;



    public async Task<IReadOnlyList<SubjectCategoryDto>> ListAsync(bool? isActive, CancellationToken cancellationToken = default)

    {

        await EnsureDefaultsAsync(cancellationToken);

        var items = await _repository.ListAsync(TenantId, isActive, cancellationToken);

        return items.Select(Map).ToList();

    }



    public async Task<SubjectCategoryDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default)

    {

        var entity = await _repository.GetByIdAsync(TenantId, id, cancellationToken);

        return entity is null ? null : Map(entity);

    }



    public async Task<SubjectCategoryDto> CreateAsync(CreateSubjectCategoryRequest request, CancellationToken cancellationToken = default)

    {

        await _createValidator.ValidateAndThrowAsync(request, cancellationToken);

        if (await _repository.CodeExistsAsync(TenantId, request.Code.Trim(), null, cancellationToken))

            throw new DomainException($"Subject category code '{request.Code}' already exists.");



        var entity = new SubjectCategory

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



    public async Task<SubjectCategoryDto> UpdateAsync(UpdateSubjectCategoryRequest request, CancellationToken cancellationToken = default)

    {

        await _updateValidator.ValidateAndThrowAsync(request, cancellationToken);

        var entity = await _repository.GetByIdAsync(TenantId, request.Id, cancellationToken)

            ?? throw new KeyNotFoundException($"Subject category '{request.Id}' was not found.");



        if (await _repository.CodeExistsAsync(TenantId, request.Code.Trim(), request.Id, cancellationToken))

            throw new DomainException($"Subject category code '{request.Code}' already exists.");



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

            ?? throw new KeyNotFoundException($"Subject category '{id}' was not found.");

        entity.IsDeleted = true;

        await ConcurrencyExceptionHelper.SaveChangesAsync(_unitOfWork, cancellationToken);

    }



    public async Task EnsureDefaultsAsync(CancellationToken cancellationToken = default)

    {

        var existing = await _repository.ListAsync(TenantId, null, cancellationToken);

        var existingCodes = existing.Select(x => x.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var toAdd = DefaultCategories

            .Where(d => !existingCodes.Contains(d.Code))

            .Select(d => new SubjectCategory

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



    public async Task UpdateSubjectCategoryFieldsAsync(UpdateSubjectSchedulingCategoryRequest request, CancellationToken cancellationToken = default)

    {

        await _updateSubjectValidator.ValidateAndThrowAsync(request, cancellationToken);



        var subject = await _context.Subjects

            .FirstOrDefaultAsync(x => x.TenantId == TenantId && x.Id == request.SubjectId, cancellationToken)

            ?? throw new KeyNotFoundException($"Subject '{request.SubjectId}' was not found.");



        var category = await _repository.GetByIdAsync(TenantId, request.SubjectCategoryId, cancellationToken)

            ?? throw new KeyNotFoundException($"Subject category '{request.SubjectCategoryId}' was not found.");



        if (!SubjectCategoryValidationHelper.ValidateRoomTypeForCategory(category.Code, request.RequiresRoomType, out var error))

            throw new DomainException(error!);



        subject.SubjectCategoryId = request.SubjectCategoryId;

        subject.RequiresRoomType = request.RequiresRoomType;

        subject.DefaultDurationMinutes = request.DefaultDurationMinutes;

        subject.RequiresLabEquipment = request.RequiresLabEquipment;

        await ConcurrencyExceptionHelper.SaveChangesAsync(_unitOfWork, cancellationToken);

    }



    private static SubjectCategoryDto Map(SubjectCategory x) => new()

    {

        Id = x.Id,

        Code = x.Code,

        Name = x.Name,

        SortOrder = x.SortOrder,

        IsActive = x.IsActive,

    };

}

