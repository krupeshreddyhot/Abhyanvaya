using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.Enrollment.Persistence;
using Abhyanvaya.Domain.Enums;

namespace Abhyanvaya.Infrastructure.Enrollment.Persistence;

public sealed class DefaultEnrollmentPersistencePolicy : IEnrollmentPersistencePolicy
{
    public EnrollmentPersistencePolicyDecision Evaluate(EnrollmentPersistencePolicyContext context)
    {
        if (context.CurrentStatus is EnrollmentStatus.Cancelled or EnrollmentStatus.Failed)
        {
            return new EnrollmentPersistencePolicyDecision
            {
                AllowPersist = false,
                RejectionReason = $"Enrollment item is in terminal status {context.CurrentStatus}.",
            };
        }

        if (context.ExistingEmbeddingId.HasValue &&
            string.Equals(context.ExistingEmbeddingVersion, context.RequestedEmbeddingVersion, StringComparison.Ordinal))
        {
            return new EnrollmentPersistencePolicyDecision
            {
                AllowPersist = false,
                ReturnExistingOnDuplicate = true,
                RejectionReason = "Embedding already persisted for this version.",
            };
        }

        return new EnrollmentPersistencePolicyDecision
        {
            AllowPersist = true,
            AllowOverwrite = false,
            ReturnExistingOnDuplicate = true,
            KeepHistoricalVersions = true,
            StoreFailedEmbeddings = false,
        };
    }
}
