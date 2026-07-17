using Abhyanvaya.Application.Enrollment.Persistence;

namespace Abhyanvaya.Application.Common.Interfaces;

/// <summary>
/// Sole owner of persisting completed enrollment embedding artifacts (AI20.PHASE2.1.7).
/// </summary>
public interface IEnrollmentResultWriter
{
    Task<EnrollmentPersistenceResult> PersistEmbeddingAsync(
        EnrollmentPersistenceRequest request,
        CancellationToken cancellationToken = default);
}
