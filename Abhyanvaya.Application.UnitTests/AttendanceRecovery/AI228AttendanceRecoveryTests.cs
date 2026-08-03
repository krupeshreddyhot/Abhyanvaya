using Abhyanvaya.Application.AttendanceRecovery;
using Abhyanvaya.Domain.Entities;
using Abhyanvaya.Domain.Enums;

namespace Abhyanvaya.Application.UnitTests.AttendanceRecovery;

public class AI228AttendanceRecoveryTests
{
    private static AttendanceSession NewSession() => new()
    {
        Id = Guid.NewGuid(),
        TenantId = 1,
        CourseId = 1,
        GroupId = 1,
        SemesterId = 1,
        SubjectId = 1,
        AttendanceDate = DateTime.UtcNow.Date,
        CreatedUtc = DateTime.UtcNow
    };

    [Fact]
    public void WorkflowMapper_Failed_MapsToRecognitionFailed_StatusUnchanged()
    {
        var session = NewSession();
        session.MoveToPending();
        session.MoveToProcessing();
        session.MoveToFailed();
        session.ProcessingError = "x";

        Assert.Equal(AttendanceSessionStatus.Failed, session.Status);
        Assert.Equal(AttendanceWorkflowStatus.RecognitionFailed, AttendanceWorkflowMapper.FromSession(session, hasImages: true));
    }

    [Fact]
    public void WorkflowMapper_Expired_Wins()
    {
        var session = NewSession();
        session.WorkflowExpiredUtc = DateTime.UtcNow;
        Assert.Equal(AttendanceWorkflowStatus.Expired, AttendanceWorkflowMapper.FromSession(session));
    }

    [Fact]
    public void ResumePath_Always_Targets_Existing_Session_Review()
    {
        var id = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var path = AttendanceWorkflowMapper.ResumePath(id, AttendanceWorkflowStatus.ReviewPending);
        Assert.Contains(id.ToString(), path);
        Assert.StartsWith("/attendance/sessions/", path);
    }

    [Fact]
    public void ResumeCheckpoint_Never_AutoStarts_Recognition()
    {
        var dto = new Abhyanvaya.Application.DTOs.AttendanceRecovery.AttendanceResumeCheckpointDto
        {
            SessionId = Guid.NewGuid(),
            WorkflowStatus = AttendanceWorkflowStatus.ReviewPending
        };
        Assert.False(dto.AutoStartRecognition);
    }

    [Fact]
    public void RetryResult_Never_Restarts_Completed_Stages_Flag()
    {
        var dto = new Abhyanvaya.Application.DTOs.AttendanceRecovery.AttendanceRetryResultDto
        {
            SessionId = Guid.NewGuid(),
            Kind = Abhyanvaya.Application.DTOs.AttendanceRecovery.AttendanceRetryKind.RetryRecognition,
            Success = true,
            WorkflowStatus = AttendanceWorkflowStatus.RecognitionRunning,
            RetryCount = 1
        };
        Assert.False(dto.RestartedCompletedStages);
    }

    [Fact]
    public void WorkflowMapper_Processing_Is_RecognitionRunning()
    {
        var session = NewSession();
        session.MoveToPending();
        session.MoveToProcessing();
        Assert.Equal(AttendanceWorkflowStatus.RecognitionRunning, AttendanceWorkflowMapper.FromSession(session, hasImages: true));
    }

    [Fact]
    public void WorkflowMapper_AwaitingReview_Is_ReviewPending()
    {
        var session = NewSession();
        session.MoveToPending();
        session.MoveToProcessing();
        session.MoveToAwaitingReview();
        Assert.Equal(AttendanceWorkflowStatus.ReviewPending, AttendanceWorkflowMapper.FromSession(session, hasImages: true));
    }

    [Fact]
    public void WorkflowMapper_Cancelled_Is_Cancelled()
    {
        var session = NewSession();
        session.Cancel();
        Assert.Equal(AttendanceWorkflowStatus.Cancelled, AttendanceWorkflowMapper.FromSession(session));
    }

    [Fact]
    public void AttendanceSessionResolver_Contract_Still_Present()
    {
        var t = typeof(Abhyanvaya.Application.Scheduling.Conflicts.AttendanceSessionResolver);
        Assert.NotNull(t.GetMethod("ResolveAsync"));
    }

    [Fact]
    public void Lifecycle_EnsureNotExpired_Blocks_Finalization()
    {
        var session = NewSession();
        session.WorkflowStatus = AttendanceWorkflowStatus.Expired;
        session.WorkflowExpiredUtc = DateTime.UtcNow;
        var lifecycle = new AttendanceWorkflowLifecycleService(new NoOpAttendanceRecoveryNotifier());
        Assert.Throws<Abhyanvaya.Domain.Exceptions.DomainException>(() =>
            lifecycle.EnsureNotExpiredForFinalization(session));
    }

    [Fact]
    public void Lifecycle_ApplyLocal_Sets_Workflow_And_Activity()
    {
        var session = NewSession();
        session.MoveToPending();
        session.MoveToProcessing();
        var lifecycle = new AttendanceWorkflowLifecycleService(new NoOpAttendanceRecoveryNotifier());
        lifecycle.ApplyLocal(session, hasImages: true, force: AttendanceWorkflowStatus.RecognitionRunning);
        Assert.Equal(AttendanceWorkflowStatus.RecognitionRunning, session.WorkflowStatus);
        Assert.NotNull(session.LastActivityUtc);
    }
}
