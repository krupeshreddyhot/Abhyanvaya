using Abhyanvaya.API.Common;
using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.DTOs.StudentFaceEmbedding;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Abhyanvaya.API.Controllers;

/// <summary>
/// Face-embedding management for student photos (generation, regeneration, listing, status).
/// </summary>
[ApiController]
[Authorize(Policy = AuthorizationPolicies.CanManageStudents)]
[Route("api/student/{studentId:int}/embeddings")]
public sealed class StudentFaceEmbeddingController : ControllerBase
{
    private readonly IStudentFaceEmbeddingService _embeddingService;

    public StudentFaceEmbeddingController(IStudentFaceEmbeddingService embeddingService)
    {
        _embeddingService = embeddingService;
    }

    /// <summary>Returns embedding status summary for the student.</summary>
    [HttpGet("status")]
    [ProducesResponseType(typeof(StudentFaceEmbeddingStatusDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<StudentFaceEmbeddingStatusDto>> GetStatus(
        int studentId,
        CancellationToken cancellationToken)
    {
        var status = await _embeddingService.GetStatusAsync(studentId, cancellationToken);
        return Ok(status);
    }

    /// <summary>Lists all face embeddings for the student (history included).</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<StudentFaceEmbeddingDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<StudentFaceEmbeddingDto>>> List(
        int studentId,
        CancellationToken cancellationToken)
    {
        var embeddings = await _embeddingService.ListAsync(studentId, cancellationToken);
        return Ok(embeddings);
    }

    /// <summary>Queues face-embedding generation from the current student photo.</summary>
    [HttpPost("generate")]
    [ProducesResponseType(typeof(StudentFaceEmbeddingStatusDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<StudentFaceEmbeddingStatusDto>> Generate(
        int studentId,
        CancellationToken cancellationToken)
    {
        var status = await _embeddingService.RequestGenerateAsync(studentId, cancellationToken);
        return Ok(status);
    }

    /// <summary>Queues regeneration (deactivates current active embedding after new vector is stored).</summary>
    [HttpPost("regenerate")]
    [ProducesResponseType(typeof(StudentFaceEmbeddingStatusDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<StudentFaceEmbeddingStatusDto>> Regenerate(
        int studentId,
        CancellationToken cancellationToken)
    {
        var status = await _embeddingService.RequestRegenerateAsync(studentId, cancellationToken);
        return Ok(status);
    }

    /// <summary>Deactivates a specific embedding so it is no longer used for matching.</summary>
    [HttpPost("{embeddingId:guid}/deactivate")]
    [ProducesResponseType(typeof(StudentFaceEmbeddingDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<StudentFaceEmbeddingDto>> Deactivate(
        int studentId,
        Guid embeddingId,
        CancellationToken cancellationToken)
    {
        var embedding = await _embeddingService.DeactivateAsync(studentId, embeddingId, cancellationToken);
        return Ok(embedding);
    }
}
