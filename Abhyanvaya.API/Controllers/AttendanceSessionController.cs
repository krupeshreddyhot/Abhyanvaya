using System.Globalization;
using Abhyanvaya.API.Common;
using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.DTOs.Attendance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Abhyanvaya.API.Controllers;

/// <summary>
/// Attendance session lifecycle endpoints for AI recognition review and finalization.
/// </summary>
[ApiController]
[Authorize(Policy = AuthorizationPolicies.CanManageAttendance)]
public sealed class AttendanceSessionController : ControllerBase
{
    private readonly IAttendanceSessionQueryService _sessionQueryService;
    private readonly IAttendanceSessionFinalizer _finalizer;
    private readonly IAttendanceSessionAnalyticsService _analyticsService;
    private readonly IClassroomPhotoService _classroomPhotoService;
    private readonly IAttendanceSessionCreator _sessionCreator;

    public AttendanceSessionController(
        IAttendanceSessionQueryService sessionQueryService,
        IAttendanceSessionFinalizer finalizer,
        IAttendanceSessionAnalyticsService analyticsService,
        IClassroomPhotoService classroomPhotoService,
        IAttendanceSessionCreator sessionCreator)
    {
        _sessionQueryService = sessionQueryService;
        _finalizer = finalizer;
        _analyticsService = analyticsService;
        _classroomPhotoService = classroomPhotoService;
        _sessionCreator = sessionCreator;
    }

    /// <summary>Creates a draft AI photo attendance session for the selected class context.</summary>
    [HttpPost("~/api/attendance-sessions")]
    [ProducesResponseType(typeof(CreatePhotoAttendanceSessionResponseDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<CreatePhotoAttendanceSessionResponseDto>> CreatePhotoAttendanceSession(
        [FromBody] CreatePhotoAttendanceSessionDto request,
        CancellationToken cancellationToken)
    {
        var (ok, error, sessionId) = await _sessionCreator.CreatePhotoAttendanceSessionAsync(
            new CreatePhotoAttendanceSessionRequest
            {
                CourseId = request.CourseId,
                GroupId = request.GroupId,
                SemesterId = request.SemesterId,
                SubjectId = request.SubjectId,
                AttendanceDate = request.AttendanceDate,
                PeriodNumber = request.PeriodNumber,
                SessionNumber = request.SessionNumber,
                TotalStudents = request.TotalStudents,
            },
            cancellationToken);

        if (!ok || sessionId == null)
        {
            return BadRequest(error);
        }

        return Ok(new CreatePhotoAttendanceSessionResponseDto { AttendanceSessionId = sessionId.Value });
    }

    /// <summary>Returns session image and status context for the teacher review screen.</summary>
    [HttpGet("~/api/attendance-sessions/{sessionId:guid}")]
    [ProducesResponseType(typeof(AttendanceSessionReviewDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<AttendanceSessionReviewDto>> GetSession(
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        var session = await _sessionQueryService.GetSessionForReviewAsync(sessionId, cancellationToken);

        if (session == null)
        {
            return NotFound($"Attendance session '{sessionId}' was not found.");
        }

        return Ok(session);
    }

    /// <summary>Returns lightweight live recognition status for dashboard polling.</summary>
    [HttpGet("~/api/attendance-sessions/{sessionId:guid}/status")]
    [ProducesResponseType(typeof(AttendanceSessionStatusDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<AttendanceSessionStatusDto>> GetSessionStatus(
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        var status = await _sessionQueryService.GetSessionStatusAsync(sessionId, cancellationToken);

        if (status == null)
        {
            return NotFound($"Attendance session '{sessionId}' was not found.");
        }

        return Ok(status);
    }

    /// <summary>Returns recognition and attendance analytics for the session.</summary>
    [HttpGet("~/api/attendance-sessions/{sessionId:guid}/analytics")]
    [ProducesResponseType(typeof(AttendanceSessionAnalyticsDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<AttendanceSessionAnalyticsDto>> GetSessionAnalytics(
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        var analytics = await _analyticsService.GetSessionAnalyticsAsync(sessionId, cancellationToken);
        return Ok(analytics);
    }

    /// <summary>Validates review completeness, builds official attendance, and approves the session atomically.</summary>
    [HttpPost("~/api/attendance-sessions/{sessionId:guid}/finalize")]
    [ProducesResponseType(typeof(AttendanceBuildSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AttendanceBuildSummaryDto>> FinalizeSession(
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        var summary = await _finalizer.FinalizeAttendanceSessionAsync(sessionId, cancellationToken);
        return Ok(summary);
    }

    /// <summary>Returns finalization readiness for the teacher review screen.</summary>
    [HttpGet("~/api/attendance-sessions/{sessionId:guid}/finalization-status")]
    [ProducesResponseType(typeof(FinalizationStatusDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<FinalizationStatusDto>> GetFinalizationStatus(
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        var status = await _sessionQueryService.GetFinalizationStatusAsync(sessionId, cancellationToken);

        if (status == null)
        {
            return NotFound($"Attendance session '{sessionId}' was not found.");
        }

        return Ok(status);
    }

    /// <summary>Returns post-finalization report metrics for the session.</summary>
    [HttpGet("~/api/attendance-sessions/{sessionId:guid}/report")]
    [ProducesResponseType(typeof(AttendanceSessionReportDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<AttendanceSessionReportDto>> GetSessionReport(
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        var report = await _sessionQueryService.GetSessionReportAsync(sessionId, cancellationToken);

        if (report == null)
        {
            return NotFound($"Attendance session '{sessionId}' was not found.");
        }

        return Ok(report);
    }

    /// <summary>Returns audit entries for session timeline views.</summary>
    [HttpGet("~/api/attendance-sessions/{sessionId:guid}/audit-entries")]
    [ProducesResponseType(typeof(IReadOnlyList<AuditEntryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AuditEntryDto>>> GetSessionAuditEntries(
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        var entries = await _sessionQueryService.GetSessionAuditEntriesAsync(sessionId, cancellationToken);
        return Ok(entries);
    }

    /// <summary>
    /// Uploads a classroom photo and queues AI face detection + matching (teacher review required).
    /// Optional form fields (AI22.7A): acquisitionMethod, captureDevice, captureTimestampUtc,
    /// orientation, latitude, longitude, blurScore. Field name <c>file</c> is unchanged.
    /// </summary>
    [HttpPost("~/api/attendance-sessions/{sessionId:guid}/classroom-photo")]
    [ProducesResponseType(typeof(ClassroomPhotoUploadResult), StatusCodes.Status200OK)]
    [RequestSizeLimit(15 * 1024 * 1024)]
    public async Task<ActionResult<ClassroomPhotoUploadResult>> UploadClassroomPhoto(
        Guid sessionId,
        IFormFile file,
        [FromForm] string? acquisitionMethod = null,
        [FromForm] string? captureDevice = null,
        [FromForm] string? captureTimestampUtc = null,
        [FromForm] short? orientation = null,
        [FromForm] double? latitude = null,
        [FromForm] double? longitude = null,
        [FromForm] double? blurScore = null,
        CancellationToken cancellationToken = default)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest("Image file is required.");
        }

        var captureContext = BuildCaptureContext(
            acquisitionMethod,
            captureDevice,
            captureTimestampUtc,
            orientation,
            latitude,
            longitude,
            blurScore);

        await using var stream = file.OpenReadStream();
        var (ok, error, result) = await _classroomPhotoService.UploadClassroomPhotoAsync(
            sessionId,
            stream,
            file.FileName,
            file.Length,
            cancellationToken,
            captureContext);

        if (!ok)
        {
            return BadRequest(error);
        }

        return Ok(result);
    }

    private static ClassroomPhotoCaptureContextDto? BuildCaptureContext(
        string? acquisitionMethod,
        string? captureDevice,
        string? captureTimestampUtc,
        short? orientation,
        double? latitude,
        double? longitude,
        double? blurScore)
    {
        var hasAny =
            !string.IsNullOrWhiteSpace(acquisitionMethod) ||
            !string.IsNullOrWhiteSpace(captureDevice) ||
            !string.IsNullOrWhiteSpace(captureTimestampUtc) ||
            orientation.HasValue ||
            latitude.HasValue ||
            longitude.HasValue ||
            blurScore.HasValue;

        if (!hasAny)
        {
            return null;
        }

        DateTime? capturedUtc = null;
        if (!string.IsNullOrWhiteSpace(captureTimestampUtc) &&
            DateTime.TryParse(
                captureTimestampUtc,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var parsed))
        {
            capturedUtc = parsed.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(parsed, DateTimeKind.Utc)
                : parsed.ToUniversalTime();
        }

        return new ClassroomPhotoCaptureContextDto
        {
            AcquisitionMethod = acquisitionMethod,
            CaptureDevice = captureDevice,
            CaptureTimestampUtc = capturedUtc,
            Orientation = orientation,
            Latitude = latitude,
            Longitude = longitude,
            BlurScore = blurScore,
        };
    }
}
