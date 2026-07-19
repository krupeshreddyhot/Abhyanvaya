using Abhyanvaya.API.Common;
using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.DTOs.AttendanceRecognition;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Abhyanvaya.API.Controllers;

/// <summary>
/// Teacher review commands for provisional AI recognition rows.
/// </summary>
[ApiController]
[Authorize(Policy = AuthorizationPolicies.CanManageAttendance)]
public sealed class AttendanceRecognitionController : ControllerBase
{
    private readonly IAttendanceRecognitionReviewService _reviewService;
    private readonly ITenantContextService _tenantContextService;

    public AttendanceRecognitionController(
        IAttendanceRecognitionReviewService reviewService,
        ITenantContextService tenantContextService)
    {
        _reviewService = reviewService;
        _tenantContextService = tenantContextService;
    }

    [HttpGet("~/api/attendance-sessions/{sessionId:guid}/recognitions")]
    [ProducesResponseType(typeof(IReadOnlyList<AttendanceRecognitionReviewDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<AttendanceRecognitionReviewDto>>> GetRecognitionsForSession(
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        if (this.RequireTenantContext(_tenantContextService, out _) is { } contextError)
        {
            return contextError;
        }

        var results = await _reviewService.GetRecognitionsForSessionAsync(sessionId, cancellationToken);
        return Ok(results);
    }

    [HttpGet("~/api/attendance-sessions/{sessionId:guid}/recognition-summary")]
    [ProducesResponseType(typeof(RecognitionSummaryDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<RecognitionSummaryDto>> GetRecognitionSummary(
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        var summary = await _reviewService.GetRecognitionSummaryAsync(sessionId, cancellationToken);
        return Ok(summary);
    }

    [HttpPost("~/api/attendance-recognition/review")]
    [ProducesResponseType(typeof(AttendanceRecognitionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AttendanceRecognitionDto>> ReviewRecognition(
        [FromBody] AttendanceRecognitionReviewRequest request,
        CancellationToken cancellationToken)
    {
        if (request.RecognitionId == Guid.Empty)
        {
            return BadRequest("RecognitionId is required.");
        }

        var result = await _reviewService.ReviewRecognitionAsync(request, cancellationToken);
        return Ok(result);
    }

    [HttpPost("~/api/attendance-recognition/review-batch")]
    [ProducesResponseType(typeof(IReadOnlyList<AttendanceRecognitionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<IReadOnlyList<AttendanceRecognitionDto>>> ReviewBatch(
        [FromBody] AttendanceRecognitionBatchReviewRequest request,
        CancellationToken cancellationToken)
    {
        if (request.AttendanceSessionId == Guid.Empty)
        {
            return BadRequest("AttendanceSessionId is required.");
        }

        if (request.Reviews == null || request.Reviews.Count == 0)
        {
            return BadRequest("At least one review item is required.");
        }

        var results = await _reviewService.ReviewBatchAsync(request, cancellationToken);
        return Ok(results);
    }

    [HttpDelete("~/api/attendance-recognition/{id:guid}/reset")]
    [ProducesResponseType(typeof(AttendanceRecognitionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AttendanceRecognitionDto>> ResetRecognition(
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await _reviewService.ResetRecognitionAsync(id, cancellationToken);
        return Ok(result);
    }

    [HttpGet("~/api/attendance-recognition/{recognitionId:guid}/review-history")]
    [ProducesResponseType(typeof(IReadOnlyList<AttendanceRecognitionReviewHistoryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AttendanceRecognitionReviewHistoryDto>>> GetReviewHistoryForRecognition(
        Guid recognitionId,
        CancellationToken cancellationToken)
    {
        var results = await _reviewService.GetReviewHistoryForRecognitionAsync(recognitionId, cancellationToken);
        return Ok(results);
    }

    [HttpGet("~/api/attendance-sessions/{sessionId:guid}/recognition-review-history")]
    [ProducesResponseType(typeof(IReadOnlyList<AttendanceRecognitionReviewHistoryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AttendanceRecognitionReviewHistoryDto>>> GetReviewHistoryForSession(
        Guid sessionId,
        CancellationToken cancellationToken)
    {
        var results = await _reviewService.GetReviewHistoryForSessionAsync(sessionId, cancellationToken);
        return Ok(results);
    }
}
