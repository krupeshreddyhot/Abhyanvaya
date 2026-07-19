using Abhyanvaya.Application.Enrollment.Persistence;

namespace Abhyanvaya.Application.Common.Interfaces;

/// <summary>Centralizes enrollment embedding persistence policy decisions.</summary>
public interface IEnrollmentPersistencePolicy
{
    EnrollmentPersistencePolicyDecision Evaluate(EnrollmentPersistencePolicyContext context);
}
