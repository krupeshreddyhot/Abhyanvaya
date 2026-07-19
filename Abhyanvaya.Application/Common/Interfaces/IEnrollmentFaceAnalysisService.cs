using Abhyanvaya.Application.Enrollment.Validation;

namespace Abhyanvaya.Application.Common.Interfaces;

/// <summary>
/// Detection + alignment for enrollment validation without embedding generation.
/// Wraps <see cref="InsightFaceEngine"/> (Infrastructure) — not the full recognition <c>DetectAsync</c> path.
/// </summary>
public interface IEnrollmentFaceAnalysisService
{
    string ProviderName { get; }

    string ModelName { get; }

    string PipelineVersion { get; }

    Task<EnrollmentFaceAnalysisResult> AnalyzeAsync(
        byte[] imageBytes,
        CancellationToken cancellationToken = default);
}
