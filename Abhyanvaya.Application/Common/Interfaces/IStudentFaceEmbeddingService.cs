using Abhyanvaya.Application.DTOs.StudentFaceEmbedding;

namespace Abhyanvaya.Application.Common.Interfaces;

/// <summary>
/// Manages <see cref="Domain.Entities.StudentFaceEmbedding"/> rows and embedding generation requests.
/// </summary>
public interface IStudentFaceEmbeddingService
{
    Task<StudentFaceEmbeddingStatusDto> GetStatusAsync(int studentId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<StudentFaceEmbeddingDto>> ListAsync(int studentId, CancellationToken cancellationToken = default);

    Task<StudentFaceEmbeddingStatusDto> RequestGenerateAsync(int studentId, CancellationToken cancellationToken = default);

    Task<StudentFaceEmbeddingStatusDto> RequestRegenerateAsync(int studentId, CancellationToken cancellationToken = default);

    Task<StudentFaceEmbeddingDto> DeactivateAsync(int studentId, Guid embeddingId, CancellationToken cancellationToken = default);
}
