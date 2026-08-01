using Abhyanvaya.Domain.Entities;
using Abhyanvaya.Domain.Enums;
using Abhyanvaya.Domain.Exceptions;

namespace Abhyanvaya.Application.UnitTests;

/// <summary>
/// Guards multi-image / retry recognition against invalid session status transitions
/// (e.g. Processing → Processing when overlapping queue jobs start).
/// </summary>
public sealed class AttendanceSessionStateMachineTests
{
    [Fact]
    public void MoveToProcessing_WhenAlreadyProcessing_IsIdempotent()
    {
        var session = CreatePhotoSession();
        session.MoveToPending();
        session.MoveToProcessing();

        session.MoveToProcessing();

        Assert.Equal(AttendanceSessionStatus.Processing, session.Status);
    }

    [Fact]
    public void MoveToProcessing_FromFailed_AllowsRetry()
    {
        var session = CreatePhotoSession();
        session.MoveToPending();
        session.MoveToProcessing();
        session.ProcessingError = "previous failure";
        session.MoveToFailed();

        session.MoveToProcessing();

        Assert.Equal(AttendanceSessionStatus.Processing, session.Status);
        Assert.Null(session.ProcessingError);
    }

    [Fact]
    public void MoveToProcessing_FromAwaitingReview_AllowsReprocess()
    {
        var session = CreatePhotoSession();
        session.MoveToPending();
        session.MoveToProcessing();
        session.MoveToAwaitingReview();

        session.MoveToProcessing();

        Assert.Equal(AttendanceSessionStatus.Processing, session.Status);
    }

    [Fact]
    public void MoveToAwaitingReview_WhenAlreadyAwaitingReview_IsIdempotent()
    {
        var session = CreatePhotoSession();
        session.MoveToPending();
        session.MoveToProcessing();
        session.MoveToAwaitingReview();

        session.MoveToAwaitingReview();

        Assert.Equal(AttendanceSessionStatus.AwaitingReview, session.Status);
    }

    [Fact]
    public void MoveToFailed_WhenAlreadyFailed_IsIdempotent()
    {
        var session = CreatePhotoSession();
        session.MoveToPending();
        session.MoveToProcessing();
        session.MoveToFailed();

        session.MoveToFailed();

        Assert.Equal(AttendanceSessionStatus.Failed, session.Status);
    }

    [Fact]
    public void MoveToProcessing_FromApproved_StillThrows()
    {
        var session = CreatePhotoSession();
        session.Approve(1, DateTime.UtcNow);

        Assert.Throws<DomainException>(() => session.MoveToProcessing());
    }

    private static AttendanceSession CreatePhotoSession() =>
        AttendanceSession.CreateForPhotoAttendance(
            tenantId: 1,
            facultyId: 1,
            courseId: 1,
            groupId: 1,
            semesterId: 1,
            subjectId: 1,
            attendanceDate: DateTime.UtcNow.Date,
            periodNumber: 1);
}
