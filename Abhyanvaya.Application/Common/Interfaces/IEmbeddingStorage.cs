using Abhyanvaya.Application.DTOs.StudentFaceEmbedding;

namespace Abhyanvaya.Application.Common.Interfaces;

/// <summary>
/// Persists <see cref="Domain.Entities.StudentFaceEmbedding"/> rows during the embedding pipeline.
/// </summary>
public interface IEmbeddingStorage
{
    Task MarkPendingAsync(StudentPhotoUploadedMessage message, CancellationToken cancellationToken = default);

    Task<Guid> MarkProcessingAsync(StudentPhotoUploadedMessage message, CancellationToken cancellationToken = default);

    Task<StudentFaceEmbeddingDto> StoreCompletedAsync(
        StudentPhotoUploadedMessage message,
        Guid embeddingId,
        float[] normalizedVector,
        EmbeddingGenerationResult result,
        long photoVersion,
        CancellationToken cancellationToken = default);

    Task RecordFailureAsync(
        StudentPhotoUploadedMessage message,
        Guid embeddingId,
        string failureReason,
        CancellationToken cancellationToken = default);

    Task ResetRetryCountAsync(Guid embeddingId, CancellationToken cancellationToken = default);
}
