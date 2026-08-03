using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.Common.Interfaces.Scheduling;
using Abhyanvaya.Application.DTOs.Scheduling;
using Abhyanvaya.Application.Scheduling;
using Abhyanvaya.Application.UnitTests.Scheduling.Phase2;
using Abhyanvaya.Domain.Entities.Scheduling;
using Abhyanvaya.Domain.Enums.Scheduling;
using Abhyanvaya.Domain.Exceptions;
using FluentValidation;
using FluentValidation.Results;
using Moq;

namespace Abhyanvaya.Application.UnitTests.Scheduling.Phase2A;

public sealed class TimetableApprovalTransitionTests
{
    private readonly Mock<ITimetableApprovalRepository> _approvalRepository = new();
    private readonly Mock<ITimetableApprovalCommentRepository> _commentRepository = new();
    private readonly Mock<ITimetableDecisionHistoryRepository> _decisionRepository = new();
    private readonly Mock<ITimetableRepository> _timetableRepository = new();
    private readonly Mock<IScheduleVersionRepository> _versionRepository = new();
    private readonly Mock<IApplicationDbContext> _context = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly Mock<ICurrentUserService> _currentUser = new();

    public TimetableApprovalTransitionTests()
    {
        _currentUser.Setup(x => x.TenantId).Returns(1);
        _currentUser.Setup(x => x.UserId).Returns(10);
        _unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        _context.Setup(c => c.SchedulingScheduleVersions).Returns(Array.Empty<ScheduleVersion>().AsAsyncQueryable());
        _context.Setup(c => c.SchedulingTimetables).Returns(Array.Empty<Timetable>().AsAsyncQueryable());
    }

    [Fact]
    public async Task SubmitForReview_SetsVersionUnderReview()
    {
        var timetable = new Timetable { Id = 5, TenantId = 1, ScheduleVersionId = 2, Status = TimetableStatus.Locked, AcademicYearId = 1, Name = "T" };
        var version = new ScheduleVersion { Id = 2, TenantId = 1, Status = ScheduleVersionStatus.Draft, AcademicYearId = 1, VersionName = "V1", VersionNumber = 1 };
        _timetableRepository.Setup(r => r.GetByIdAsync(1, 5, It.IsAny<CancellationToken>())).ReturnsAsync(timetable);
        _versionRepository.Setup(r => r.GetByIdAsync(1, 2, It.IsAny<CancellationToken>())).ReturnsAsync(version);
        _approvalRepository.Setup(r => r.GetPendingByTimetableAsync(1, 5, It.IsAny<CancellationToken>())).ReturnsAsync((TimetableApprovalRequest?)null);
        _approvalRepository.Setup(r => r.GetByIdAsync(1, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((int _, int id, CancellationToken _) => new TimetableApprovalRequest
            {
                Id = id,
                ScheduleVersionId = 2,
                TimetableId = 5,
                Status = TimetableApprovalRequestStatus.InReview,
                CurrentStepOrder = 1,
                Steps =
                [
                    new TimetableApprovalStep { StepOrder = 1, RoleKey = "Coordinator", Status = TimetableApprovalRequestStatus.InReview },
                    new TimetableApprovalStep { StepOrder = 2, RoleKey = "Administrator", Status = TimetableApprovalRequestStatus.Pending }
                ]
            });

        var service = CreateService();
        await service.SubmitForReviewAsync(new SubmitForReviewRequest { TimetableId = 5 });

        Assert.Equal(ScheduleVersionStatus.UnderReview, version.Status);
        Assert.Equal(TimetableStatus.Locked, timetable.Status);
    }

    [Fact]
    public async Task DecideStep_FinalApprove_SetsVersionApprovedNotPublished()
    {
        var version = new ScheduleVersion { Id = 2, TenantId = 1, Status = ScheduleVersionStatus.UnderReview, AcademicYearId = 1, VersionName = "V1", VersionNumber = 1 };
        var timetable = new Timetable { Id = 5, TenantId = 1, ScheduleVersionId = 2, Status = TimetableStatus.Locked, Name = "T" };
        var request = new TimetableApprovalRequest
        {
            Id = 9,
            TenantId = 1,
            ScheduleVersionId = 2,
            TimetableId = 5,
            Status = TimetableApprovalRequestStatus.InReview,
            CurrentStepOrder = 2,
            Steps =
            [
                new TimetableApprovalStep { StepOrder = 1, RoleKey = "Coordinator", Status = TimetableApprovalRequestStatus.Approved },
                new TimetableApprovalStep { StepOrder = 2, RoleKey = "Administrator", Status = TimetableApprovalRequestStatus.InReview }
            ]
        };

        _approvalRepository.Setup(r => r.GetByIdAsync(1, 9, It.IsAny<CancellationToken>())).ReturnsAsync(request);
        _versionRepository.Setup(r => r.GetByIdAsync(1, 2, It.IsAny<CancellationToken>())).ReturnsAsync(version);
        _timetableRepository.Setup(r => r.GetByIdAsync(1, 5, It.IsAny<CancellationToken>())).ReturnsAsync(timetable);

        var service = CreateService();
        await service.DecideStepAsync(new DecideApprovalStepRequest
        {
            RequestId = 9,
            StepOrder = 2,
            Decision = ApprovalDecision.Approved
        });

        Assert.Equal(ScheduleVersionStatus.Approved, version.Status);
        Assert.Equal(TimetableApprovalRequestStatus.Approved, request.Status);
        Assert.NotEqual(ScheduleVersionStatus.Published, version.Status);
    }

    [Fact]
    public async Task DecideStep_Reject_ReturnsVersionToDraft()
    {
        var version = new ScheduleVersion { Id = 2, TenantId = 1, Status = ScheduleVersionStatus.UnderReview, AcademicYearId = 1, VersionName = "V1", VersionNumber = 1 };
        var timetable = new Timetable { Id = 5, TenantId = 1, ScheduleVersionId = 2, Status = TimetableStatus.Locked, Name = "T" };
        var request = new TimetableApprovalRequest
        {
            Id = 9,
            TenantId = 1,
            ScheduleVersionId = 2,
            TimetableId = 5,
            Status = TimetableApprovalRequestStatus.InReview,
            CurrentStepOrder = 1,
            Steps = [new TimetableApprovalStep { StepOrder = 1, RoleKey = "Coordinator", Status = TimetableApprovalRequestStatus.InReview }]
        };

        _approvalRepository.Setup(r => r.GetByIdAsync(1, 9, It.IsAny<CancellationToken>())).ReturnsAsync(request);
        _versionRepository.Setup(r => r.GetByIdAsync(1, 2, It.IsAny<CancellationToken>())).ReturnsAsync(version);
        _timetableRepository.Setup(r => r.GetByIdAsync(1, 5, It.IsAny<CancellationToken>())).ReturnsAsync(timetable);

        var service = CreateService();
        await service.DecideStepAsync(new DecideApprovalStepRequest
        {
            RequestId = 9,
            StepOrder = 1,
            Decision = ApprovalDecision.Rejected,
            Comments = "Needs corrections"
        });

        Assert.Equal(ScheduleVersionStatus.Draft, version.Status);
        Assert.Equal(TimetableStatus.Draft, timetable.Status);
    }

    private TimetableApprovalService CreateService()
    {
        var valid = new ValidationResult();
        return new TimetableApprovalService(
            _approvalRepository.Object,
            _commentRepository.Object,
            _decisionRepository.Object,
            _timetableRepository.Object,
            _versionRepository.Object,
            _context.Object,
            _unitOfWork.Object,
            _currentUser.Object,
            Mock.Of<IValidator<SubmitForReviewRequest>>(v => v.ValidateAsync(It.IsAny<SubmitForReviewRequest>(), It.IsAny<CancellationToken>()) == Task.FromResult(valid)),
            Mock.Of<IValidator<DecideApprovalStepRequest>>(v => v.ValidateAsync(It.IsAny<DecideApprovalStepRequest>(), It.IsAny<CancellationToken>()) == Task.FromResult(valid)),
            Mock.Of<IValidator<AddApprovalCommentRequest>>(v => v.ValidateAsync(It.IsAny<AddApprovalCommentRequest>(), It.IsAny<CancellationToken>()) == Task.FromResult(valid)));
    }
}
