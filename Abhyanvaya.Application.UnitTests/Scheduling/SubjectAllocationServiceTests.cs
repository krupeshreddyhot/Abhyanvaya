using Abhyanvaya.Application.Common.Interfaces;

using Abhyanvaya.Application.Common.Interfaces.Scheduling;

using Abhyanvaya.Application.DTOs.Scheduling;

using Abhyanvaya.Application.Scheduling;

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

    private readonly Mock<IUnitOfWork> _unitOfWork = new();

    private readonly Mock<ICurrentUserService> _currentUser = new();

    private readonly Mock<IValidator<CreateSubjectAllocationRequest>> _createValidator = new();

    private readonly Mock<IValidator<UpdateSubjectAllocationRequest>> _updateValidator = new();



    public SubjectAllocationServiceTests()

    {

        _currentUser.Setup(x => x.TenantId).Returns(1);

        _createValidator.Setup(v => v.ValidateAsync(It.IsAny<CreateSubjectAllocationRequest>(), It.IsAny<CancellationToken>()))

            .ReturnsAsync(new FluentValidation.Results.ValidationResult());

        _updateValidator.Setup(v => v.ValidateAsync(It.IsAny<UpdateSubjectAllocationRequest>(), It.IsAny<CancellationToken>()))

            .ReturnsAsync(new FluentValidation.Results.ValidationResult());

        _unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        _departmentRepository.Setup(d => d.GetByIdAsync(1, It.IsAny<int>(), It.IsAny<CancellationToken>()))

            .ReturnsAsync(new Department { Id = 5, Name = "CS", CollegeId = 1 });

    }



    private SubjectAllocationService CreateService() => new(

        _repository.Object,

        _workloadRepository.Object,

        _departmentRepository.Object,

        _unitOfWork.Object,

        _currentUser.Object,

        _createValidator.Object,

        _updateValidator.Object);



    private static CreateSubjectAllocationRequest CreateRequest() => new()

    {

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



    [Fact]

    public async Task CreateAsync_Throws_WhenDuplicateAllocationExists()

    {

        var service = CreateService();

        var request = CreateRequest();

        _repository.Setup(r => r.DuplicateExistsAsync(1, request.AcademicYearId, request.SubjectId, request.CourseId, request.GroupId, request.SemesterId, request.DepartmentId, null, It.IsAny<CancellationToken>()))

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

        _repository.Verify(r => r.AddAsync(It.IsAny<SubjectAllocation>(), It.IsAny<CancellationToken>()), Times.Once);

    }

}

