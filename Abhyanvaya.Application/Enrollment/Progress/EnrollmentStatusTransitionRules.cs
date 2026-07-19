using Abhyanvaya.Domain.Enums;

namespace Abhyanvaya.Application.Enrollment.Progress;

/// <summary>
/// Approved item status transitions (docs/AI20_ENROLLMENT_ARCHITECTURE.md §6).
/// </summary>
public static class EnrollmentStatusTransitionRules
{
    private static readonly IReadOnlyDictionary<EnrollmentStatus, HashSet<EnrollmentStatus>> Allowed =
        new Dictionary<EnrollmentStatus, HashSet<EnrollmentStatus>>
        {
            [EnrollmentStatus.Pending] =
            [
                EnrollmentStatus.Downloading,
                EnrollmentStatus.Cancelled,
            ],
            [EnrollmentStatus.Downloading] =
            [
                EnrollmentStatus.Downloaded,
                EnrollmentStatus.RetryRequired,
                EnrollmentStatus.Failed,
                EnrollmentStatus.Cancelled,
            ],
            [EnrollmentStatus.Downloaded] =
            [
                EnrollmentStatus.Validating,
                EnrollmentStatus.RetryRequired,
                EnrollmentStatus.Failed,
                EnrollmentStatus.Cancelled,
            ],
            [EnrollmentStatus.Validating] =
            [
                EnrollmentStatus.Embedding,
                EnrollmentStatus.RetryRequired,
                EnrollmentStatus.Failed,
                EnrollmentStatus.Cancelled,
            ],
            [EnrollmentStatus.Embedding] =
            [
                EnrollmentStatus.Completed,
                EnrollmentStatus.RetryRequired,
                EnrollmentStatus.Failed,
                EnrollmentStatus.Cancelled,
            ],
            [EnrollmentStatus.RetryRequired] =
            [
                EnrollmentStatus.Pending,
                EnrollmentStatus.Downloading,
                EnrollmentStatus.Cancelled,
            ],
            [EnrollmentStatus.Failed] =
            [
                EnrollmentStatus.Pending,
                EnrollmentStatus.Cancelled,
            ],
            [EnrollmentStatus.Completed] = [],
            [EnrollmentStatus.Cancelled] = [],
        };

    public static bool IsAllowed(EnrollmentStatus from, EnrollmentStatus to) =>
        Allowed.TryGetValue(from, out var targets) && targets.Contains(to);

    public static void EnsureAllowed(EnrollmentStatus from, EnrollmentStatus to)
    {
        if (!IsAllowed(from, to))
        {
            throw new InvalidOperationException(
                $"Illegal enrollment status transition: {from} -> {to}.");
        }
    }
}
