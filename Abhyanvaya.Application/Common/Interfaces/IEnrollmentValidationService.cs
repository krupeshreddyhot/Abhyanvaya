using Abhyanvaya.Application.Enrollment.Validation;

namespace Abhyanvaya.Application.Common.Interfaces;

/// <summary>
/// Strict enrollment photo quality gate — evaluates suitability only; no storage, embedding, or DB I/O
/// (docs/AI20_PHASE2_ENGINE_CONTRACTS.md §8).
/// </summary>
public interface IEnrollmentValidationService
{
    Task<EnrollmentValidationResult> ValidateAsync(
        EnrollmentValidationRequest request,
        CancellationToken cancellationToken = default);
}
