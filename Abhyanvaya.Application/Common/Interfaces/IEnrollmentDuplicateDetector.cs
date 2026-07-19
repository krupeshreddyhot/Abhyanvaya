using Abhyanvaya.Application.Enrollment.Persistence;

namespace Abhyanvaya.Application.Common.Interfaces;

/// <summary>Metadata-only duplicate detection for idempotent enrollment persistence.</summary>
public interface IEnrollmentDuplicateDetector
{
    Task<EnrollmentDuplicateDetectionResult> DetectAsync(
        EnrollmentDuplicateDetectionRequest request,
        CancellationToken cancellationToken = default);
}
