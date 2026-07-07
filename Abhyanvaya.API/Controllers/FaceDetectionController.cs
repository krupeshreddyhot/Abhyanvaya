using Abhyanvaya.API.Common;
using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.DTOs.Recognition;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Abhyanvaya.API.Controllers;

/// <summary>Face detection endpoints (InsightFace / ONNX). Returns detections only—no attendance writes.</summary>
[ApiController]
[Authorize(Policy = AuthorizationPolicies.CanManageAttendance)]
[Route("api/face-detection")]
public sealed class FaceDetectionController : ControllerBase
{
    private readonly IFaceDetectionService _faceDetectionService;

    public FaceDetectionController(IFaceDetectionService faceDetectionService)
    {
        _faceDetectionService = faceDetectionService;
    }

    /// <summary>Detects faces, aligns them, and extracts embeddings from an uploaded image.</summary>
    [HttpPost("detect")]
    [ProducesResponseType(typeof(FaceDetectionResponse), StatusCodes.Status200OK)]
    [RequestSizeLimit(15 * 1024 * 1024)]
    public async Task<ActionResult<FaceDetectionResponse>> Detect(
        IFormFile file,
        [FromQuery] int? maxFaces,
        CancellationToken cancellationToken)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest("Image file is required.");
        }

        await using var stream = file.OpenReadStream();
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms, cancellationToken);

        var response = await _faceDetectionService.DetectAsync(
            new FaceDetectionRequest(ms.ToArray(), maxFaces),
            cancellationToken);

        return Ok(response);
    }
}
