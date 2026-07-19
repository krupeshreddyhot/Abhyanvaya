using Abhyanvaya.Application.Enrollment.Persistence;
using Abhyanvaya.Domain.Entities;

namespace Abhyanvaya.Application.Common.Interfaces;

/// <summary>Single repository abstraction for enrollment embedding persistence SQL.</summary>
public interface IEnrollmentPersistenceRepository
{
    Task<EnrollmentPersistenceContext?> LoadContextAsync(
        Guid batchId,
        int studentId,
        CancellationToken cancellationToken = default);

    Task<StudentFaceEmbedding?> GetEmbeddingByIdAsync(
        Guid embeddingId,
        CancellationToken cancellationToken = default);

    Task<EnrollmentPersistenceWriteOutcome> PersistEmbeddingAsync(
        EnrollmentPersistenceWriteRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record EnrollmentPersistenceContext
{
    public required StudentEnrollmentItem Item { get; init; }

    public required StudentEnrollmentBatch Batch { get; init; }

    public required Student Student { get; init; }
}
