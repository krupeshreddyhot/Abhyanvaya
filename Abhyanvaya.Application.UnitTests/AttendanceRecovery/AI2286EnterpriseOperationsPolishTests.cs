using Abhyanvaya.Application.AttendanceRecovery;
using Abhyanvaya.Application.DTOs.AttendanceRecovery;
using Abhyanvaya.Application.Scheduling.Conflicts;
using Abhyanvaya.Domain.Entities;
using Abhyanvaya.Domain.Enums;

namespace Abhyanvaya.Application.UnitTests.AttendanceRecovery;

/// <summary>AI22.8.6 — SLA, timeline mapping, bulk safety, resolver guard.</summary>
public class AI2286EnterpriseOperationsPolishTests
{
    [Theory]
    [InlineData(5, "Green", "On Track")]
    [InlineData(20, "Yellow", "Watch")]
    [InlineData(45, "Orange", "At Risk")]
    [InlineData(90, "Red", "Breach")]
    public void SlaCalculator_Bands(double minutes, string level, string status)
    {
        var snap = AttendanceSlaCalculator.Calculate(minutes, expectedRemainingMinutes: 10);
        Assert.Equal(level, snap.Level.ToString());
        Assert.Equal(status, snap.SlaStatus);
        Assert.True(snap.ExpectedCompletionUtc > DateTime.UtcNow.AddMinutes(5));
    }

    [Fact]
    public void DisplayEnricher_Includes_Sla_Fields()
    {
        var session = new AttendanceSession
        {
            Id = Guid.NewGuid(),
            TenantId = 1,
            CourseId = 1,
            GroupId = 1,
            SemesterId = 1,
            SubjectId = 1,
            AttendanceDate = DateTime.UtcNow.Date,
            CreatedUtc = DateTime.UtcNow.AddMinutes(-50),
            LastActivityUtc = DateTime.UtcNow.AddMinutes(-40)
        };
        session.MoveToPending();
        session.MoveToProcessing();
        session.MoveToFailed();

        var dto = AttendanceSessionDisplayEnricher.Map(session);
        Assert.False(string.IsNullOrWhiteSpace(dto.SlaLevel));
        Assert.False(string.IsNullOrWhiteSpace(dto.SlaStatus));
        Assert.False(string.IsNullOrWhiteSpace(dto.ElapsedDisplay));
        Assert.NotNull(dto.ExpectedCompletionUtc);
        Assert.Equal("Orange", dto.SlaLevel);
    }

    [Fact]
    public void BulkOperationKinds_Do_Not_Include_AutoFinalize()
    {
        var names = Enum.GetNames<AttendanceBulkOperationKind>();
        Assert.DoesNotContain(names, n => n.Contains("Finalize", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(AttendanceBulkOperationKind.RetryFailedRecognition, Enum.GetValues<AttendanceBulkOperationKind>());
        Assert.Contains(AttendanceBulkOperationKind.NotifyFaculty, Enum.GetValues<AttendanceBulkOperationKind>());
    }

    [Fact]
    public void BulkResult_Safety_Flags()
    {
        var result = new BulkOperationResultDto
        {
            OperationId = Guid.NewGuid(),
            Operation = nameof(AttendanceBulkOperationKind.MarkReviewed),
            RequestedCount = 1,
            SucceededCount = 1
        };
        Assert.True(result.NeverAutoFinalizes);
        Assert.True(result.NeverRetriesSuccessful);
    }

    [Fact]
    public void DepartmentSummary_Reuses_Catalog_Flag()
    {
        var dto = new DepartmentOperationsDashboardDto();
        Assert.True(dto.ReusesCatalogDepartment);
    }

    [Fact]
    public void TimelineDto_Reuses_Retry_History_Flag()
    {
        var dto = new SessionTimelineDto { SessionId = Guid.NewGuid() };
        Assert.True(dto.ReusesRetryHistory);
    }

    [Fact]
    public void AttendanceSessionResolver_Type_Still_Present_Unchanged_Contract()
    {
        // Guard: polish layer must not remove the sole Legacy vs Timetable selector type.
        var type = typeof(AttendanceSessionResolver);
        Assert.Equal("Abhyanvaya.Application.Scheduling.Conflicts", type.Namespace);
        Assert.Contains(type.GetInterfaces(), i => i.Name.Contains("AttendanceSessionResolver", StringComparison.Ordinal));
    }

    [Fact]
    public void NotificationCodes_Cover_Ops_Events()
    {
        Assert.Equal("SlaBreach", AttendanceOpsNotificationCodes.SlaBreach);
        Assert.Equal("BulkOperationCompleted", AttendanceOpsNotificationCodes.BulkOperationCompleted);
        Assert.Equal("FacultyReminder", AttendanceOpsNotificationCodes.FacultyReminder);
    }

    [Fact]
    public void FormatElapsed_Hours()
    {
        Assert.Equal("45m", AttendanceSlaCalculator.FormatElapsed(45));
        Assert.Equal("1h 30m", AttendanceSlaCalculator.FormatElapsed(90));
        Assert.Equal("2h 5m", AttendanceSlaCalculator.FormatElapsed(125));
    }
}
