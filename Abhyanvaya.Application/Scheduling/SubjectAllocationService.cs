using Abhyanvaya.Application.Academic;
using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.Common.Interfaces.Scheduling;
using Abhyanvaya.Application.DTOs.Scheduling;
using Abhyanvaya.Application.Internal;
using Abhyanvaya.Domain.Entities.Scheduling;
using Abhyanvaya.Domain.Exceptions;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using SubjectAllocationEntity = Abhyanvaya.Domain.Entities.Scheduling.SubjectAllocation;

namespace Abhyanvaya.Application.Scheduling;

public sealed class SubjectAllocationService : ISubjectAllocationService
{
    private readonly ISubjectAllocationRepository _repository;
    private readonly IFacultyWorkloadRepository _workloadRepository;
    private readonly IDepartmentRepository _departmentRepository;
    private readonly IApplicationDbContext _db;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;
    private readonly IValidator<CreateSubjectAllocationRequest> _createValidator;
    private readonly IValidator<UpdateSubjectAllocationRequest> _updateValidator;

    public SubjectAllocationService(
        ISubjectAllocationRepository repository,
        IFacultyWorkloadRepository workloadRepository,
        IDepartmentRepository departmentRepository,
        IApplicationDbContext db,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUser,
        IValidator<CreateSubjectAllocationRequest> createValidator,
        IValidator<UpdateSubjectAllocationRequest> updateValidator)
    {
        _repository = repository;
        _workloadRepository = workloadRepository;
        _departmentRepository = departmentRepository;
        _db = db;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    private int TenantId => _currentUser.TenantId;

    public async Task<IReadOnlyList<SubjectAllocationDto>> ListAsync(
        int? academicYearId, int? staffId, int? departmentId, CancellationToken cancellationToken = default)
    {
        var items = await _repository.ListAsync(TenantId, academicYearId, staffId, departmentId, cancellationToken);
        return items.Select(Map).ToList();
    }

    public async Task<SubjectAllocationDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await _repository.GetByIdAsync(TenantId, id, cancellationToken);
        return entity is null ? null : Map(entity);
    }

    public async Task<SubjectAllocationDto> CreateAsync(
        CreateSubjectAllocationRequest request, CancellationToken cancellationToken = default)
    {
        await _createValidator.ValidateAndThrowAsync(request, cancellationToken);

        var alignedDepartmentId = await ResolveAlignedDepartmentIdAsync(
            request.CourseId, request.DepartmentId, cancellationToken);

        await ValidateBusinessRulesAsync(
            request.AcademicYearId, request.SubjectId, request.CourseId, request.GroupId,
            request.SemesterId, alignedDepartmentId, request.StaffId, request.WeeklyHours,
            null, cancellationToken);

        var entity = MapToEntity(request);
        entity.DepartmentId = alignedDepartmentId;
        entity.TenantId = TenantId;
        await _repository.AddAsync(entity, cancellationToken);
        await ConcurrencyExceptionHelper.SaveChangesAsync(_unitOfWork, cancellationToken);
        return Map(entity);
    }

    public async Task<SubjectAllocationDto> UpdateAsync(
        UpdateSubjectAllocationRequest request, CancellationToken cancellationToken = default)
    {
        await _updateValidator.ValidateAndThrowAsync(request, cancellationToken);
        var entity = await _repository.GetByIdAsync(TenantId, request.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"Subject allocation '{request.Id}' was not found.");

        var alignedDepartmentId = await ResolveAlignedDepartmentIdAsync(
            request.CourseId, request.DepartmentId, cancellationToken);

        await ValidateBusinessRulesAsync(
            request.AcademicYearId, request.SubjectId, request.CourseId, request.GroupId,
            request.SemesterId, alignedDepartmentId, request.StaffId, request.WeeklyHours,
            request.Id, cancellationToken);

        ApplyRequest(entity, request);
        entity.DepartmentId = alignedDepartmentId;
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

    private async Task<int> ResolveAlignedDepartmentIdAsync(
        int courseId, int requestedDepartmentId, CancellationToken cancellationToken)
    {
        // Fail-closed: other tenants' courses are invisible.
        var courseDepartmentId = await _db.Courses.AsNoTracking()
            .Where(c => c.Id == courseId && c.TenantId == TenantId)
            .Select(c => (int?)c.DepartmentId)
            .FirstOrDefaultAsync(cancellationToken);

        var decision = SubjectAllocationCourseDepartmentRules.Evaluate(
            requestedDepartmentId,
            courseDepartmentId,
            courseFound: courseDepartmentId is > 0);

        if (!decision.Accepted)
            throw new DomainException(decision.Error ?? "Invalid Course Department for SubjectAllocation.");

        return decision.AlignedDepartmentId;
    }

    internal async Task ValidateBusinessRulesAsync(
        int academicYearId, int subjectId, int courseId, int groupId, int semesterId, int departmentId,
        int staffId, decimal weeklyHours, int? excludeId, CancellationToken cancellationToken)
    {
        if (weeklyHours <= 0)
            throw new DomainException("Weekly hours must be greater than zero.");

        if (await _departmentRepository.GetByIdAsync(TenantId, departmentId, cancellationToken) is null)
            throw new DomainException($"Department '{departmentId}' was not found.");

        if (await _repository.DuplicateExistsAsync(
                TenantId, academicYearId, subjectId, courseId, groupId, semesterId, departmentId, excludeId, cancellationToken))
            throw new DomainException(
                "A subject allocation already exists for this subject, course, group, semester, department, and academic year.");

        var semester = await _db.Semesters.AsNoTracking()
            .Where(s => s.Id == semesterId && s.TenantId == TenantId && !s.IsDeleted)
            .Select(s => new { s.Id, s.GroupId, s.CourseId, s.IsHistoricalArchive })
            .FirstOrDefaultAsync(cancellationToken);
        if (semester is null)
            throw new DomainException($"Semester '{semesterId}' was not found.");
        if (semester.IsHistoricalArchive)
            throw new DomainException(OperationalSemesterRules.HistoricalRejectedMessage);
        if (semester.GroupId is null || semester.GroupId.Value != groupId)
            throw new DomainException("Semester must be Group-specific and match the selected Group.");
        if (semester.CourseId != courseId)
            throw new DomainException("Semester does not belong to the selected Course.");

        var workload = await _workloadRepository.GetByStaffIdAsync(TenantId, staffId, cancellationToken);
        if (workload?.MaxPeriodsPerWeek > 0)
        {
            var total = await _repository.SumWeeklyHoursForStaffAsync(TenantId, staffId, excludeId, cancellationToken);
            if (total + weeklyHours > workload.MaxPeriodsPerWeek)
                throw new DomainException(
                    $"Total weekly hours ({total + weeklyHours}) exceeds faculty max periods per week ({workload.MaxPeriodsPerWeek}).");
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
