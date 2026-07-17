using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.Enrollment.Persistence;
using Abhyanvaya.Domain.Enums;

namespace Abhyanvaya.Infrastructure.Enrollment.Persistence;

public sealed class EnrollmentDuplicateDetector : IEnrollmentDuplicateDetector
{
    private readonly IEnrollmentPersistenceRepository _repository;

    public EnrollmentDuplicateDetector(IEnrollmentPersistenceRepository repository) =>
        _repository = repository;

    public async Task<EnrollmentDuplicateDetectionResult> DetectAsync(
        EnrollmentDuplicateDetectionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.ItemStatus == EnrollmentStatus.Completed &&
            request.ExistingEmbeddingId.HasValue &&
            string.Equals(request.ItemEmbeddingVersion, request.EmbeddingModelVersion, StringComparison.Ordinal))
        {
            return new EnrollmentDuplicateDetectionResult
            {
                IsDuplicate = true,
                ExistingEmbeddingId = request.ExistingEmbeddingId,
                Reason = "Enrollment item already completed with matching embedding version.",
            };
        }

        if (request.ExistingEmbeddingId.HasValue)
        {
            var existing = await _repository.GetEmbeddingByIdAsync(request.ExistingEmbeddingId.Value, cancellationToken);
            if (existing is not null &&
                existing.StudentId == request.StudentId &&
                string.Equals(existing.EmbeddingModel, request.EmbeddingModel, StringComparison.Ordinal) &&
                string.Equals(existing.EmbeddingVersion, request.EmbeddingModelVersion, StringComparison.Ordinal))
            {
                return new EnrollmentDuplicateDetectionResult
                {
                    IsDuplicate = true,
                    ExistingEmbeddingId = existing.Id,
                    Reason = "Matching embedding already exists for student and model version.",
                };
            }
        }

        return new EnrollmentDuplicateDetectionResult { IsDuplicate = false };
    }
}
