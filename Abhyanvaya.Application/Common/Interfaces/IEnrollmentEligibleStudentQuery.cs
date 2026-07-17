using Abhyanvaya.Application.Enrollment;

namespace Abhyanvaya.Application.Common.Interfaces;

/// <summary>Read-only student discovery for enrollment batch creation (no photo loading).</summary>
public interface IEnrollmentEligibleStudentQuery
{
    Task<IReadOnlyList<EnrollmentEligibleStudent>> GetEligibleStudentsAsync(
        EnrollmentStudentDiscoveryCriteria criteria,
        CancellationToken cancellationToken = default);

    Task<bool> AdmissionBatchHasStudentsAsync(
        int tenantId,
        int admissionBatch,
        CancellationToken cancellationToken = default);
}
