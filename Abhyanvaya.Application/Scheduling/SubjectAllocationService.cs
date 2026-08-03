using Abhyanvaya.Application.Common.Interfaces;

using Abhyanvaya.Application.Common.Interfaces.Scheduling;

using Abhyanvaya.Application.DTOs.Scheduling;

using Abhyanvaya.Application.Internal;

using Abhyanvaya.Domain.Entities.Scheduling;

using Abhyanvaya.Domain.Exceptions;

using FluentValidation;

using SubjectAllocationEntity = Abhyanvaya.Domain.Entities.Scheduling.SubjectAllocation;



namespace Abhyanvaya.Application.Scheduling;



public sealed class SubjectAllocationService : ISubjectAllocationService

{

    private readonly ISubjectAllocationRepository _repository;

    private readonly IFacultyWorkloadRepository _workloadRepository;

    private readonly IDepartmentRepository _departmentRepository;

    private readonly IUnitOfWork _unitOfWork;

    private readonly ICurrentUserService _currentUser;

    private readonly IValidator<CreateSubjectAllocationRequest> _createValidator;

    private readonly IValidator<UpdateSubjectAllocationRequest> _updateValidator;



    public SubjectAllocationService(

        ISubjectAllocationRepository repository,

        IFacultyWorkloadRepository workloadRepository,

        IDepartmentRepository departmentRepository,

        IUnitOfWork unitOfWork,

        ICurrentUserService currentUser,

        IValidator<CreateSubjectAllocationRequest> createValidator,

        IValidator<UpdateSubjectAllocationRequest> updateValidator)

    {

        _repository = repository;

        _workloadRepository = workloadRepository;

        _departmentRepository = departmentRepository;

        _unitOfWork = unitOfWork;

        _currentUser = currentUser;

        _createValidator = createValidator;

        _updateValidator = updateValidator;

    }



    private int TenantId => _currentUser.TenantId;



    public async Task<IReadOnlyList<SubjectAllocationDto>> ListAsync(int? academicYearId, int? staffId, int? departmentId, CancellationToken cancellationToken = default)

    {

        var items = await _repository.ListAsync(TenantId, academicYearId, staffId, departmentId, cancellationToken);

        return items.Select(Map).ToList();

    }



    public async Task<SubjectAllocationDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default)

    {

        var entity = await _repository.GetByIdAsync(TenantId, id, cancellationToken);

        return entity is null ? null : Map(entity);

    }



    public async Task<SubjectAllocationDto> CreateAsync(CreateSubjectAllocationRequest request, CancellationToken cancellationToken = default)

    {

        await _createValidator.ValidateAndThrowAsync(request, cancellationToken);

        await ValidateBusinessRulesAsync(request.AcademicYearId, request.SubjectId, request.CourseId, request.GroupId,

            request.SemesterId, request.DepartmentId, request.StaffId, request.WeeklyHours, null, cancellationToken);



        var entity = MapToEntity(request);

        entity.TenantId = TenantId;

        await _repository.AddAsync(entity, cancellationToken);

        await ConcurrencyExceptionHelper.SaveChangesAsync(_unitOfWork, cancellationToken);

        return Map(entity);

    }



    public async Task<SubjectAllocationDto> UpdateAsync(UpdateSubjectAllocationRequest request, CancellationToken cancellationToken = default)

    {

        await _updateValidator.ValidateAndThrowAsync(request, cancellationToken);

        var entity = await _repository.GetByIdAsync(TenantId, request.Id, cancellationToken)

            ?? throw new KeyNotFoundException($"Subject allocation '{request.Id}' was not found.");



        await ValidateBusinessRulesAsync(request.AcademicYearId, request.SubjectId, request.CourseId, request.GroupId,

            request.SemesterId, request.DepartmentId, request.StaffId, request.WeeklyHours, request.Id, cancellationToken);



        ApplyRequest(entity, request);

        await ConcurrencyExceptionHelper.SaveChangesAsync(_unitOfWork, cancellationToken);

        return Map(entity);

    }



    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)

    {

        var entity = await _repository.GetByIdAsync(TenantId, id, cancellationToken)

            ?? throw new KeyNotFoundException($"Subject allocation '{id}' was not found.");

        entity.IsDeleted = true;

        await ConcurrencyExceptionHelper.SaveChangesAsync(_unitOfWork, cancellationToken);

    }



    internal async Task ValidateBusinessRulesAsync(

        int academicYearId, int subjectId, int courseId, int groupId, int semesterId, int departmentId,

        int staffId, decimal weeklyHours, int? excludeId, CancellationToken cancellationToken)

    {

        if (weeklyHours <= 0)

            throw new DomainException("Weekly hours must be greater than zero.");



        if (await _departmentRepository.GetByIdAsync(TenantId, departmentId, cancellationToken) is null)

            throw new DomainException($"Department '{departmentId}' was not found.");



        if (await _repository.DuplicateExistsAsync(TenantId, academicYearId, subjectId, courseId, groupId, semesterId, departmentId, excludeId, cancellationToken))

            throw new DomainException("A subject allocation already exists for this subject, course, group, semester, department, and academic year.");



        var workload = await _workloadRepository.GetByStaffIdAsync(TenantId, staffId, cancellationToken);

        if (workload?.MaxPeriodsPerWeek > 0)

        {

            var total = await _repository.SumWeeklyHoursForStaffAsync(TenantId, staffId, excludeId, cancellationToken);

            if (total + weeklyHours > workload.MaxPeriodsPerWeek)

                throw new DomainException($"Total weekly hours ({total + weeklyHours}) exceeds faculty max periods per week ({workload.MaxPeriodsPerWeek}).");

        }

    }



    private static SubjectAllocationEntity MapToEntity(CreateSubjectAllocationRequest request) => new()

    {

        AcademicYearId = request.AcademicYearId,

        SubjectId = request.SubjectId,

        StaffId = request.StaffId,

        CourseId = request.CourseId,

        GroupId = request.GroupId,

        SemesterId = request.SemesterId,

        DepartmentId = request.DepartmentId,

        WeeklyHours = request.WeeklyHours,

        PreferredRoomId = request.PreferredRoomId,

        LabRequired = request.LabRequired,

        AiAttendanceEnabled = request.AiAttendanceEnabled,

        AttendanceMandatory = request.AttendanceMandatory,

        EffectiveFrom = request.EffectiveFrom,

        EffectiveTo = request.EffectiveTo,

        Notes = request.Notes?.Trim(),

    };



    private static void ApplyRequest(SubjectAllocationEntity entity, UpdateSubjectAllocationRequest request)

    {

        entity.AcademicYearId = request.AcademicYearId;

        entity.SubjectId = request.SubjectId;

        entity.StaffId = request.StaffId;

        entity.CourseId = request.CourseId;

        entity.GroupId = request.GroupId;

        entity.SemesterId = request.SemesterId;

        entity.DepartmentId = request.DepartmentId;

        entity.WeeklyHours = request.WeeklyHours;

        entity.PreferredRoomId = request.PreferredRoomId;

        entity.LabRequired = request.LabRequired;

        entity.AiAttendanceEnabled = request.AiAttendanceEnabled;

        entity.AttendanceMandatory = request.AttendanceMandatory;

        entity.EffectiveFrom = request.EffectiveFrom;

        entity.EffectiveTo = request.EffectiveTo;

        entity.Notes = request.Notes?.Trim();

    }



    private static SubjectAllocationDto Map(SubjectAllocationEntity x) => new()

    {

        Id = x.Id,

        AcademicYearId = x.AcademicYearId,

        SubjectId = x.SubjectId,

        StaffId = x.StaffId,

        CourseId = x.CourseId,

        GroupId = x.GroupId,

        SemesterId = x.SemesterId,

        DepartmentId = x.DepartmentId,

        WeeklyHours = x.WeeklyHours,

        PreferredRoomId = x.PreferredRoomId,

        LabRequired = x.LabRequired,

        AiAttendanceEnabled = x.AiAttendanceEnabled,

        AttendanceMandatory = x.AttendanceMandatory,

        EffectiveFrom = x.EffectiveFrom,

        EffectiveTo = x.EffectiveTo,

        Notes = x.Notes,

    };

}

