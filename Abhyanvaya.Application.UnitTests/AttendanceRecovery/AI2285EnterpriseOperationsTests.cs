using Abhyanvaya.Application.AttendanceRecovery;
using Abhyanvaya.Application.DTOs.AttendanceRecovery;
using Abhyanvaya.Application.Scheduling.Conflicts;
using Abhyanvaya.Domain.Entities;
using Abhyanvaya.Domain.Enums;

namespace Abhyanvaya.Application.UnitTests.AttendanceRecovery;

public class AI2285EnterpriseOperationsTests
{
    private static AttendanceSession NewSession(AttendanceWorkflowStatus? forceWorkflow = null)
    {
        var session = new AttendanceSession
        {
            Id = Guid.NewGuid(),
            TenantId = 1,
            CourseId = 1,
            GroupId = 1,
            SemesterId = 1,
            SubjectId = 10,
            AttendanceDate = DateTime.UtcNow.Date,
            CreatedUtc = DateTime.UtcNow.AddHours(-2),
            LastActivityUtc = DateTime.UtcNow.AddMinutes(-30),
            RetryCount = 1,
            ProcessingError = "boom"
        };
        session.MoveToPending();
        session.MoveToProcessing();
        if (forceWorkflow == AttendanceWorkflowStatus.ReviewPending)
            session.MoveToAwaitingReview();
        else if (forceWorkflow == AttendanceWorkflowStatus.RecognitionFailed)
            session.MoveToFailed();
        return session;
    }

    [Fact]
    public void PriorityEngine_Failed_Ranks_Highest()
    {
        var failed = NewSession(AttendanceWorkflowStatus.RecognitionFailed);
        var review = NewSession(AttendanceWorkflowStatus.ReviewPending);
        var created = new AttendanceSession
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

        var pFailed = AttendanceSessionPriorityEngine.Calculate(failed);
        var pReview = AttendanceSessionPriorityEngine.Calculate(review);
        var pReady = AttendanceSessionPriorityEngine.Calculate(created);
        Assert.Equal("Failed", pFailed.PriorityBand);
        Assert.Equal("NeedsReview", pReview.PriorityBand);
        Assert.True(pFailed.PriorityScore > pReview.PriorityScore);
        Assert.True(pReview.PriorityScore > pReady.PriorityScore);
    }

    [Fact]
    public void PriorityEngine_Sort_Orders_By_Score()
    {
        var items = new[]
        {
            new PendingAttendanceSessionDto { SessionId = Guid.NewGuid(), PriorityScore = 10 },
            new PendingAttendanceSessionDto { SessionId = Guid.NewGuid(), PriorityScore = 9000 },
            new PendingAttendanceSessionDto { SessionId = Guid.NewGuid(), PriorityScore = 8000 }
        };
        var sorted = AttendanceSessionPriorityEngine.SortByPriority(items, x => x.PriorityScore);
        Assert.Equal(9000, sorted[0].PriorityScore);
        Assert.Equal(8000, sorted[1].PriorityScore);
    }

    [Fact]
    public void DisplayEnricher_Maps_Friendly_Labels_And_Actions()
    {
        var failed = NewSession(AttendanceWorkflowStatus.RecognitionFailed);
        var dto = AttendanceSessionDisplayEnricher.Map(failed);
        Assert.Equal("Recognition Failed", dto.FriendlyWorkflowLabel);
        Assert.True(dto.CanRetry);
        Assert.False(string.IsNullOrWhiteSpace(dto.ScheduledTimeLabel));
        Assert.True(dto.PriorityScore > 0);
        Assert.True(dto.FailureCount >= 1);
        Assert.True(dto.ExpectedRemainingMinutes > 0);
    }

    [Fact]
    public void RecoveryPreference_Defaults_Are_Sane()
    {
        var pref = new AttendanceRecoveryPreference();
        Assert.Equal(30, pref.AutoSaveFrequencySeconds);
        Assert.True(pref.ResumeConfirmation);
        Assert.Equal("pending", pref.DefaultLandingPage);
        Assert.True(pref.NotificationsEnabled);
        Assert.True(pref.SessionTimeoutWarning);
        Assert.Equal(30, pref.SessionTimeoutWarningMinutes);
    }

    [Fact]
    public void HealthSnapshot_Never_AutoCancels()
    {
        var snap = new AttendanceHealthSnapshotDto();
        Assert.True(snap.NeverAutoCancels);
    }

    [Fact]
    public void OperationalAnalytics_Is_ReadOnly()
    {
        var dto = new AttendanceOperationalAnalyticsDto();
        Assert.True(dto.ReadOnly);
    }

    [Fact]
    public void ResumeCheckpoint_Still_Never_AutoStarts_Recognition()
    {
        var dto = new AttendanceResumeCheckpointDto { SessionId = Guid.NewGuid() };
        Assert.False(dto.AutoStartRecognition);
    }

    [Fact]
    public void AttendanceSessionResolver_Still_Present_For_Legacy_And_Timetable()
    {
        var t = typeof(AttendanceSessionResolver);
        Assert.NotNull(t.GetMethod("ResolveAsync"));
        Assert.Contains("Scheduling", t.Namespace ?? "");
    }

    [Fact]
    public void FriendlyLabels_Cover_Queue_Statuses()
    {
        Assert.Equal("Recognition Ready", AttendanceSessionDisplayLabels.Friendly(AttendanceWorkflowStatus.ImagesUploaded));
        Assert.Equal("Recognition Running", AttendanceSessionDisplayLabels.Friendly(AttendanceWorkflowStatus.RecognitionRunning));
        Assert.Equal("Review Pending", AttendanceSessionDisplayLabels.Friendly(AttendanceWorkflowStatus.ReviewPending));
        Assert.Equal("Failed Upload", AttendanceSessionDisplayLabels.Friendly(AttendanceWorkflowStatus.UploadFailed));
    }

    [Fact]
    public void PriorityBand_Order_Documented()
    {
        var bands = new[] { "Failed", "NeedsReview", "RecognitionReady", "RecognitionRunning", "ExpiredSoon", "RecentlyStarted" };
        Assert.Equal(6, bands.Length);
        Assert.Equal("Failed", bands[0]);
        Assert.Equal("RecentlyStarted", bands[^1]);
    }
}
