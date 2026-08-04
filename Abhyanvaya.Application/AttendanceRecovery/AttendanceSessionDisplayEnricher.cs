using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.DTOs.AttendanceRecovery;
using Abhyanvaya.Domain.Entities;
using Abhyanvaya.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Abhyanvaya.Application.AttendanceRecovery;

/// <summary>
/// AI22.8.5 — batch display-name enrichment for recovery queue cards.
/// Reuses Subject→TenantSubject join pattern; does not touch AttendanceSessionResolver.
/// </summary>
public static class AttendanceSessionDisplayEnricher
{
    public sealed class NameMaps
    {
        public Dictionary<int, string> Courses { get; init; } = new();
        public Dictionary<int, string> Groups { get; init; } = new();
        public Dictionary<int, string> Semesters { get; init; } = new();
        public Dictionary<int, string> Subjects { get; init; } = new();
        public Dictionary<int, string> Staff { get; init; } = new();
    }

    public static async Task<NameMaps> LoadAsync(
        IApplicationDbContext db,
        IReadOnlyCollection<AttendanceSession> sessions,
        CancellationToken cancellationToken = default)
    {
        var courseIds = sessions.Select(s => s.CourseId).Distinct().ToList();
        var groupIds = sessions.Select(s => s.GroupId).Distinct().ToList();
        var semesterIds = sessions.Select(s => s.SemesterId).Distinct().ToList();
        var subjectIds = sessions.Select(s => s.SubjectId).Distinct().ToList();
        var staffIds = sessions.Where(s => s.StaffId.HasValue).Select(s => s.StaffId!.Value).Distinct().ToList();

        var courses = await db.Courses.AsNoTracking()
            .Where(c => courseIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.Name, cancellationToken);
        var groups = await db.Groups.AsNoTracking()
            .Where(g => groupIds.Contains(g.Id))
            .ToDictionaryAsync(g => g.Id, g => g.Name, cancellationToken);
        var semesters = await db.Semesters.AsNoTracking()
            .Where(s => semesterIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, s => s.Name, cancellationToken);
        var subjects = await (
            from s in db.Subjects.AsNoTracking()
            join ts in db.TenantSubjects.AsNoTracking() on s.TenantSubjectId equals ts.Id
            where subjectIds.Contains(s.Id)
            select new { s.Id, ts.Name }).ToDictionaryAsync(x => x.Id, x => x.Name, cancellationToken);
        var staff = await db.StaffMembers.AsNoTracking()
            .Where(s => staffIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, s => $"{s.FirstName} {s.LastName}".Trim(), cancellationToken);

        return new NameMaps
        {
            Courses = courses,
            Groups = groups,
            Semesters = semesters,
            Subjects = subjects,
            Staff = staff
        };
    }

    public static PendingAttendanceSessionDto Map(
        AttendanceSession s,
        NameMaps? names = null,
        AttendanceSessionPrioritySnapshot? priority = null,
        int? expirationHours = null)
    {
        var workflow = AttendanceWorkflowMapper.FromSession(s, hasImages: true);
        var last = s.LastActivityUtc ?? s.StartedUtc ?? s.CreatedUtc;
        var elapsed = Math.Max(0, (DateTime.UtcNow - last).TotalMinutes);
        var ageMinutes = Math.Max(0, (DateTime.UtcNow - s.CreatedUtc).TotalMinutes);
        var subjectName = names?.Subjects.GetValueOrDefault(s.SubjectId);
        var pri = priority ?? AttendanceSessionPriorityEngine.Calculate(s, workflow, expirationHours ?? 48);
        var sla = AttendanceSlaCalculator.Calculate(ageMinutes, pri.ExpectedRemainingMinutes);

        return new PendingAttendanceSessionDto
        {
            SessionId = s.Id,
            ResumeToken = RecoveryResumeToken.For(s.Id),
            Status = s.Status,
            WorkflowStatus = workflow,
            AttendanceDate = s.AttendanceDate,
            CourseId = s.CourseId,
            CourseName = names?.Courses.GetValueOrDefault(s.CourseId),
            GroupId = s.GroupId,
            GroupName = names?.Groups.GetValueOrDefault(s.GroupId),
            SemesterId = s.SemesterId,
            SemesterName = names?.Semesters.GetValueOrDefault(s.SemesterId),
            SubjectId = s.SubjectId,
            SubjectName = subjectName,
            PeriodNumber = s.PeriodNumber,
            StaffId = s.StaffId,
            StaffName = s.StaffId is int sid ? names?.Staff.GetValueOrDefault(sid) : null,
            StartedUtc = s.StartedUtc,
            LastActivityUtc = last,
            ElapsedMinutes = elapsed,
            RetryCount = s.RetryCount,
            FailureCount = s.RetryCount + (string.IsNullOrWhiteSpace(s.ProcessingError) ? 0 : 1),
            FailureReason = s.ProcessingError,
            ResumePath = AttendanceWorkflowMapper.ResumePath(s.Id, workflow),
            IsExpired = s.WorkflowExpiredUtc.HasValue || workflow == AttendanceWorkflowStatus.Expired,
            CurrentStage = AttendanceWorkflowMapper.CurrentStage(workflow),
            DisplayTitle = subjectName ?? $"Subject #{s.SubjectId}",
            ScheduledTimeLabel = FormatScheduledTime(s),
            PriorityScore = pri.PriorityScore,
            PriorityBand = pri.PriorityBand,
            AgeMinutes = ageMinutes,
            ExpectedRemainingMinutes = pri.ExpectedRemainingMinutes,
            CanResume = CanResume(workflow),
            CanRetry = CanRetry(workflow),
            CanFinalize = CanFinalize(workflow),
            CanCancel = CanCancel(workflow, s.Status),
            SlaLevel = sla.Level.ToString(),
            SlaStatus = sla.SlaStatus,
            SlaBadgeColor = sla.BadgeColor,
            ElapsedDisplay = AttendanceSlaCalculator.FormatElapsed(sla.ElapsedMinutes),
            ExpectedCompletionUtc = sla.ExpectedCompletionUtc
        };
    }

    public static string FormatScheduledTime(AttendanceSession s)
    {
        if (s.StartedUtc.HasValue)
            return s.StartedUtc.Value.ToLocalTime().ToString("HH:mm");
        if (s.PeriodNumber is > 0)
            return $"P{s.PeriodNumber}";
        return s.CreatedUtc.ToLocalTime().ToString("HH:mm");
    }

    public static string FriendlyWorkflowLabel(AttendanceWorkflowStatus workflow) => workflow switch
    {
        AttendanceWorkflowStatus.Created or AttendanceWorkflowStatus.ImagesUploaded
            => "Recognition Ready",
        AttendanceWorkflowStatus.RecognitionRunning => "Recognition Running",
        AttendanceWorkflowStatus.RecognitionCompleted => "Recognition Ready",
        AttendanceWorkflowStatus.ReviewPending or AttendanceWorkflowStatus.ReviewInProgress
            => "Review Pending",
        AttendanceWorkflowStatus.ReadyForFinalization => "Ready to Finalize",
        AttendanceWorkflowStatus.UploadFailed => "Failed Upload",
        AttendanceWorkflowStatus.RecognitionFailed => "Recognition Failed",
        AttendanceWorkflowStatus.AttendanceFinalized => "Completed",
        AttendanceWorkflowStatus.Cancelled => "Cancelled",
        AttendanceWorkflowStatus.Expired => "Expired",
        _ => workflow.ToString()
    };

    private static bool CanResume(AttendanceWorkflowStatus w) =>
        w is not (AttendanceWorkflowStatus.AttendanceFinalized
            or AttendanceWorkflowStatus.Cancelled
            or AttendanceWorkflowStatus.Expired
            or AttendanceWorkflowStatus.RecognitionRunning);

    private static bool CanRetry(AttendanceWorkflowStatus w) =>
        w is AttendanceWorkflowStatus.RecognitionFailed
            or AttendanceWorkflowStatus.UploadFailed
            or AttendanceWorkflowStatus.Expired;

    private static bool CanFinalize(AttendanceWorkflowStatus w) =>
        w is AttendanceWorkflowStatus.ReadyForFinalization
            or AttendanceWorkflowStatus.RecognitionCompleted
            or AttendanceWorkflowStatus.ReviewPending
            or AttendanceWorkflowStatus.ReviewInProgress;

    private static bool CanCancel(AttendanceWorkflowStatus w, AttendanceSessionStatus status) =>
        status is not (AttendanceSessionStatus.Approved or AttendanceSessionStatus.Completed)
        && w is not (AttendanceWorkflowStatus.AttendanceFinalized or AttendanceWorkflowStatus.Cancelled);
}
