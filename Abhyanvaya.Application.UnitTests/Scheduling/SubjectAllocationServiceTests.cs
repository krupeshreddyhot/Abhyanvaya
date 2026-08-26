using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.Common.Interfaces.Scheduling;
using Abhyanvaya.Application.DTOs.Scheduling;
using Abhyanvaya.Application.Scheduling;
using Abhyanvaya.Application.UnitTests.Scheduling.Phase2;
using Abhyanvaya.Domain.Entities;
using Abhyanvaya.Domain.Entities.Scheduling;
using Abhyanvaya.Domain.Exceptions;
using FluentValidation;
using Moq;

namespace Abhyanvaya.Application.UnitTests.Scheduling;

public sealed class SubjectAllocationServiceTests
{
    private readonly Mock<ISubjectAllocationRepository> _repository = new();
    private readonly Mock<IFacultyWorkloadRepository> _workloadRepository = new();
    private readonly Mock<IDepartmentRepository> _departmentRepository = new();
    private readonly Mock<IApplicationDbContext> _db = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<ICurrentUserService> _currentUser = new();
    private readonly Mock<IValidator<CreateSubjectAllocationRequest>> _createValidator = new();
    private readonly Mock<IValidator<UpdateSubjectAllocationRequest>> _updateValidator = new();
    private readonly List<Course> _courses = [];

    public SubjectAllocationServiceTests()
    {
        _currentUser.Setup(x => x.TenantId).Returns(1);
        _createValidator.Setup(v => v.ValidateAsync(It.IsAny<CreateSubjectAllocationRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FluentValidation.Results.ValidationResult());
        _updateValidator.Setup(v => v.ValidateAsync(It.IsAny<UpdateSubjectAllocationRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FluentValidation.Results.ValidationResult());
        _unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _departmentRepository.Setup(d => d.GetByIdAsync(1, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int _, int id, CancellationToken _) => new Department { Id = id, Name = "CS", CollegeId = 1, TenantId = 1 });

        _courses.Add(new Course { Id = 30, TenantId = 1, Code = "BCOM", Name = "B.Com", DepartmentId = 5 });
        _db.Setup(d => d.Courses).Returns(_courses.AsAsyncQueryable());
    }

    private SubjectAllocationService CreateService() => new(
        _repository.Object,
        _workloadRepository.Object,
        _departmentRepository.Object,
        _db.Object,
        _unitOfWork.Object,
        _currentUser.Object,
        _createValidator.Object,
        _updateValidator.Object);

    private static CreateSubjectAllocationRequest CreateRequest(int departmentId = 5) => new()
    {
        AcademicYearId = 1,
        SubjectId = 10,
        StaffId = 20,
        CourseId = 30,
        GroupId = 40,
        SemesterId = 50,
        DepartmentId = departmentId,
        WeeklyHours = 4,
        EffectiveFrom = new DateOnly(2026, 6, 1),
    };

    [Fact]
    public async Task CreateAsync_Throws_WhenDuplicateAllocationExists()
    {
        var service = CreateService();
        var request = CreateRequest();
        _repository.Setup(r => r.DuplicateExistsAsync(1, request.AcademicYearId, request.SubjectId, request.CourseId, request.GroupId, request.SemesterId, 5, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var ex = await Assert.ThrowsAsync<DomainException>(() => service.CreateAsync(request));
        Assert.Contains("already exists", ex.Message, StringComparison.OrdinalIgnoreCase);
        _repository.Verify(r => r.AddAsync(It.IsAny<SubjectAllocation>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_Throws_WhenWeeklyHoursExceedFacultyMax()
    {
        var service = CreateService();
        var request = CreateRequest();
        _repository.Setup(r => r.DuplicateExistsAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _workloadRepository.Setup(w => w.GetByStaffIdAsync(1, request.StaffId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FacultyWorkload { MaxPeriodsPerWeek = 10 });
        _repository.Setup(r => r.SumWeeklyHoursForStaffAsync(1, request.StaffId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(8m);

        var ex = await Assert.ThrowsAsync<DomainException>(() => service.CreateAsync(request));
        Assert.Contains("exceeds faculty max", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateAsync_Succeeds_WhenValidationPasses()
    {
        var service = CreateService();
        var request = CreateRequest();
        _repository.Setup(r => r.DuplicateExistsAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _workloadRepository.Setup(w => w.GetByStaffIdAsync(1, request.StaffId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FacultyWorkload { MaxPeriodsPerWeek = 20 });
        _repository.Setup(r => r.SumWeeklyHoursForStaffAsync(1, request.StaffId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(4m);

        var result = await service.CreateAsync(request);
        Assert.Equal(request.WeeklyHours, result.WeeklyHours);
        Assert.Equal(5, result.DepartmentId);
        _repository.Verify(r => r.AddAsync(It.Is<SubjectAllocation>(a => a.DepartmentId == 5), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_Rejects_Department_Mismatch_With_Course()
    {
        var service = CreateService();
        var request = CreateRequest(departmentId: 99);

        var ex = await Assert.ThrowsAsync<DomainException>(() => service.CreateAsync(request));
        Assert.Contains("must match the Course Department", ex.Message, StringComparison.OrdinalIgnoreCase);
        _repository.Verify(r => r.AddAsync(It.IsAny<SubjectAllocation>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_Rejects_Unknown_Course()
    {
        var service = CreateService();
        var request = CreateRequest();
        // CourseId 999 not in tenant courses
        var bad = new CreateSubjectAllocationRequest
        {
            AcademicYearId = request.AcademicYearId,
            SubjectId = request.SubjectId,
            StaffId = request.StaffId,
            CourseId = 999,
            GroupId = request.GroupId,
            SemesterId = request.SemesterId,
            DepartmentId = 5,
            WeeklyHours = request.WeeklyHours,
            EffectiveFrom = request.EffectiveFrom,
        };

        var ex = await Assert.ThrowsAsync<DomainException>(() => service.CreateAsync(bad));
        Assert.Contains("Course not found", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UpdateAsync_Rejects_Department_Mismatch()
    {
        var service = CreateService();
        _repository.Setup(r => r.GetByIdAsync(1, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SubjectAllocation
            {
                Id = 1,
                TenantId = 1,
                AcademicYearId = 1,
                SubjectId = 10,
                StaffId = 20,
                CourseId = 30,
                GroupId = 40,
                SemesterId = 50,
                DepartmentId = 5,
                WeeklyHours = 4,
                EffectiveFrom = new DateOnly(2026, 6, 1),
            });

        var req = new UpdateSubjectAllocationRequest
        {
            Id = 1,
            AcademicYearId = 1,
            SubjectId = 10,
            StaffId = 20,
            CourseId = 30,
            GroupId = 40,
            SemesterId = 50,
            DepartmentId = 7,
            WeeklyHours = 4,
            EffectiveFrom = new DateOnly(2026, 6, 1),
        };

        var ex = await Assert.ThrowsAsync<DomainException>(() => service.UpdateAsync(req));
        Assert.Contains("must match the Course Department", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UpdateAsync_Aligns_Department_When_Course_Changes()
    {
        _courses.Add(new Course { Id = 31, TenantId = 1, Code = "BSC", Name = "B.Sc", DepartmentId = 8 });
        _db.Setup(d => d.Courses).Returns(_courses.AsAsyncQueryable());
        _departmentRepository.Setup(d => d.GetByIdAsync(1, 8, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Department { Id = 8, Name = "Sci", CollegeId = 1, TenantId = 1 });

        var entity = new SubjectAllocation
        {
            Id = 1,
            TenantId = 1,
            AcademicYearId = 1,
            SubjectId = 10,
            StaffId = 20,
            CourseId = 30,
            GroupId = 40,
            SemesterId = 50,
            DepartmentId = 5,
            WeeklyHours = 4,
            EffectiveFrom = new DateOnly(2026, 6, 1),
        };
        _repository.Setup(r => r.GetByIdAsync(1, 1, It.IsAny<CancellationToken>())).ReturnsAsync(entity);
        _repository.Setup(r => r.DuplicateExistsAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _workloadRepository.Setup(w => w.GetByStaffIdAsync(1, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FacultyWorkload { MaxPeriodsPerWeek = 20 });
        _repository.Setup(r => r.SumWeeklyHoursForStaffAsync(1, 20, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0m);

        var service = CreateService();
        var req = new UpdateSubjectAllocationRequest
        {
            Id = 1,
            AcademicYearId = 1,
            SubjectId = 10,
            StaffId = 20,
            CourseId = 31,
            GroupId = 40,
            SemesterId = 50,
            DepartmentId = 8,
            WeeklyHours = 4,
            EffectiveFrom = new DateOnly(2026, 6, 1),
        };

        var result = await service.UpdateAsync(req);
        Assert.Equal(8, result.DepartmentId);
        Assert.Equal(31, result.CourseId);
    }
}
