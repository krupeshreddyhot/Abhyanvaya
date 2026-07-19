using Abhyanvaya.Application.Common.Interfaces;

namespace Abhyanvaya.Application.Enrollment.Storage;

public static class StorageStepMetadataExtensions
{
    public static StorageStepMetadata ToMetadata(this IEnrollmentStorageStep step) =>
        new(
            step.Name,
            step.Category,
            step.Version,
            step.Order,
            step.SupportsRollback,
            step.IsOptional,
            step.FeatureFlag,
            step.Description);
}
