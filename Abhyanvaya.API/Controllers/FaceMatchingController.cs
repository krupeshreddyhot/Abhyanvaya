using Abhyanvaya.API.Common;
using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.DTOs.Recognition;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Abhyanvaya.API.Controllers;

/// <summary>Face matching engine (detected embeddings vs student embeddings). No attendance writes.</summary>
[ApiController]
[Authorize(Policy = AuthorizationPolicies.CanManageAttendance)]
[Route("api/face-matching")]
public sealed class FaceMatchingController : ControllerBase
{
    private readonly IFaceMatcher _faceMatcher;

    public FaceMatchingController(IFaceMatcher faceMatcher)
    {
        _faceMatcher = faceMatcher;
    }

    [HttpPost("match")]
    [ProducesResponseType(typeof(IReadOnlyList<FaceMatchResultDto>), StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<FaceMatchResultDto>> Match([FromBody] FaceMatchRequest request)
    {
        var results = _faceMatcher.Match(
            request.DetectedFaces,
            request.StudentEmbeddings,
            request.Options);

        return Ok(results);
    }
}

public sealed class FaceMatchRequest
{
    public IReadOnlyList<DetectedFaceMatchInput> DetectedFaces { get; set; } = [];

    public IReadOnlyList<StudentEmbeddingMatchInput> StudentEmbeddings { get; set; } = [];

    public FaceMatchOptions? Options { get; set; }
}
