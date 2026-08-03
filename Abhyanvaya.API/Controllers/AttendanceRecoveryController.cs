using Abhyanvaya.API.Common;
using Abhyanvaya.Application.AttendanceRecovery;
using Abhyanvaya.Application.DTOs.AttendanceRecovery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Abhyanvaya.API.Controllers;

/// <summary>
/// AI22.8 / AI22.8.5 — faculty attendance recovery APIs. Composes existing AttendanceSession workflow;
/// never creates duplicate sessions. Attendance APIs remain unchanged.
/// </summary>
[ApiController]
[Route("api/attendance-recovery")]
[Authorize(Policy = AuthorizationPolicies.CanManageAttendance)]
public sealed class AttendanceRecoveryController : ControllerBase
{
    private readonly IPendingAttendanceService _pending;
    private readonly IPendingSessionQueueService _queue;
    private readonly IAttendanceResumeService _resume;
    private readonly IAttendanceRetryService _retry;
    private readonly IAttendanceRecoverySearchService _search;
    private readonly IAttendanceExpirationService _expiration;
    private readonly IAttendanceRecoveryPreferenceService _preferences;
    private readonly IFacultyRecoveryCenterService _recoveryCenter;
    private readonly IFacultyWorkspaceRecoverySummaryService _workspaceSummary;

    public AttendanceRecoveryController(
        IPendingAttendanceService pending,
        IPendingSessionQueueService queue,
        IAttendanceResumeService resume,
        IAttendanceRetryService retry,
        IAttendanceRecoverySearchService search,
        IAttendanceExpirationService expiration,
        IAttendanceRecoveryPreferenceService preferences,
        IFacultyRecoveryCenterService recoveryCenter,
        IFacultyWorkspaceRecoverySummaryService workspaceSummary)
    {
        _pending = pending;
        _queue = queue;
        _resume = resume;
        _retry = retry;
        _search = search;
        _expiration = expiration;
        _preferences = preferences;
        _recoveryCenter = recoveryCenter;
        _workspaceSummary = workspaceSummary;
    }

    [HttpGet("pending")]
    public async Task<ActionResult<PendingAttendanceBucketDto>> GetPending(CancellationToken cancellationToken)
        => Ok(await _pending.GetPendingAsync(cancellationToken));

    /// <summary>AI22.8.5.1 — centralized pending queue with filters/priority sort.</summary>
    [HttpGet("queue")]
    public async Task<ActionResult<PendingSessionQueueDto>> GetQueue(
        [FromQuery] PendingSessionQueueRequest request,
        CancellationToken cancellationToken)
        => Ok(await _queue.GetQueueAsync(request, cancellationToken));

    [HttpPost("sessions/{sessionId:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid sessionId, CancellationToken cancellationToken)
    {
        await _queue.CancelSessionAsync(sessionId, cancellationToken);
        return NoContent();
    }

    [HttpGet("recovery-center")]
    public async Task<ActionResult<FacultyRecoveryCenterDto>> RecoveryCenter(
        [FromQuery] string? query,
        CancellationToken cancellationToken)
        => Ok(await _recoveryCenter.GetAsync(query, cancellationToken));

    [HttpGet("workspace-summary")]
    public async Task<ActionResult<FacultyWorkspaceRecoverySummaryDto>> WorkspaceSummary(CancellationToken cancellationToken)
        => Ok(await _workspaceSummary.GetAsync(cancellationToken));

    [HttpGet("preferences")]
    public async Task<ActionResult<AttendanceRecoveryPreferenceDto>> GetPreferences(CancellationToken cancellationToken)
        => Ok(await _preferences.GetAsync(cancellationToken));

    [HttpPut("preferences")]
    public async Task<ActionResult<AttendanceRecoveryPreferenceDto>> UpsertPreferences(
        [FromBody] UpsertAttendanceRecoveryPreferenceRequest request,
        CancellationToken cancellationToken)
        => Ok(await _preferences.UpsertAsync(request, cancellationToken));

    [HttpGet("sessions/{sessionId:guid}/resume")]
    public async Task<ActionResult<AttendanceResumeCheckpointDto>> GetResume(Guid sessionId, CancellationToken cancellationToken)
        => Ok(await _resume.GetResumeAsync(sessionId, cancellationToken));

    [HttpPut("sessions/{sessionId:guid}/checkpoint")]
    public async Task<ActionResult<AttendanceResumeCheckpointDto>> SaveCheckpoint(
        Guid sessionId,
        [FromBody] SaveResumeCheckpointRequest request,
        CancellationToken cancellationToken)
        => Ok(await _resume.SaveCheckpointAsync(sessionId, request, cancellationToken));

    [HttpPost("sessions/{sessionId:guid}/retry")]
    public async Task<ActionResult<AttendanceRetryResultDto>> Retry(
        Guid sessionId,
        [FromBody] AttendanceRetryRequest request,
        CancellationToken cancellationToken)
        => Ok(await _retry.RetryAsync(sessionId, request, cancellationToken));

    [HttpGet("sessions/{sessionId:guid}/retry-history")]
    public async Task<ActionResult<IReadOnlyList<AttendanceRetryHistoryDto>>> RetryHistory(
        Guid sessionId,
        CancellationToken cancellationToken)
        => Ok(await _retry.GetHistoryAsync(sessionId, cancellationToken));

    [HttpGet("search")]
    public async Task<ActionResult<IReadOnlyList<PendingAttendanceSessionDto>>> Search(
        [FromQuery] AttendanceRecoverySearchRequest request,
        CancellationToken cancellationToken)
        => Ok(await _search.SearchAsync(request, cancellationToken));

    [HttpGet("auto-resume")]
    public async Task<ActionResult<AutoResumePromptDto>> AutoResume(CancellationToken cancellationToken)
        => Ok(await _resume.GetAutoResumePromptAsync(cancellationToken));

    [HttpPost("auto-resume/decision")]
    public async Task<IActionResult> AutoResumeDecision(
        [FromBody] AutoResumeDecisionRequest request,
        CancellationToken cancellationToken)
    {
        await _resume.DecideAutoResumeAsync(request, cancellationToken);
        return NoContent();
    }

    [HttpGet("expiration-options")]
    public ActionResult<ExpirationOptionsDto> ExpirationOptions() => Ok(_expiration.GetOptions());
}
