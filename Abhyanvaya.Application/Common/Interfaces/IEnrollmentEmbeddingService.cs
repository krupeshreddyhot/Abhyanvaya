using Abhyanvaya.Application.Enrollment.Embedding;

namespace Abhyanvaya.Application.Common.Interfaces;

/// <summary>
/// Sole owner of enrollment face-embedding generation (AI20.PHASE2.1.6).
/// Resolves the aligned-face artifact and produces an immutable embedding artifact — no persistence.
/// </summary>
public interface IEnrollmentEmbeddingService
{
    Task<EnrollmentEmbeddingResult> GenerateAsync(
        EnrollmentEmbeddingRequest request,
        CancellationToken cancellationToken = default);
}
