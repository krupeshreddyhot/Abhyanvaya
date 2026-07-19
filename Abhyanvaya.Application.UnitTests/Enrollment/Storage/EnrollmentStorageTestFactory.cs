using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Infrastructure.Enrollment.Storage;
using Abhyanvaya.Infrastructure.Enrollment.Storage.ArtifactTypes;
using Abhyanvaya.Infrastructure.Enrollment.Storage.Pipeline;
using Microsoft.Extensions.Logging.Abstractions;

namespace Abhyanvaya.Application.UnitTests.Enrollment.Storage;

internal static class EnrollmentStorageTestFactory
{
    internal static IEnrollmentArtifactTypeRegistry CreateRegistry() =>
        new EnrollmentArtifactTypeRegistry([
            new AlignedFaceArtifactTypeDefinition(),
            new ValidationReportArtifactTypeDefinition(),
        ]);

    internal static IEnrollmentStoragePipelineExecutor CreatePipelineExecutor(
        IEnrollmentStoragePolicy policy,
        IEnrollmentArtifactTypeRegistry registry,
        IObjectStorageProvider objectStorage,
        IChecksumService checksumService,
        IEnrollmentStorageRecordRepository repository,
        IUnitOfWork unitOfWork,
        TimeProvider clock)
    {
        var metrics = new NoOpStorageMetricsCollector();
        var steps = new IEnrollmentStorageStep[]
        {
            new ValidateInputStep(),
            new ResolvePolicyStep(policy),
            new PrepareArtifactsStep(registry),
            new ChecksumStep(checksumService, metrics),
            new CompressionStep(),
            new EncryptionStep(),
            new DuplicateDetectionStep(repository),
            new UploadStep(objectStorage, repository, metrics, clock, NullLogger<UploadStep>.Instance),
            new MetadataStep(repository, unitOfWork, NullLogger<MetadataStep>.Instance),
            new ManifestStep(),
        };

        var rollback = new RollbackStep(objectStorage, NullLogger<RollbackStep>.Instance);
        return new EnrollmentStoragePipelineExecutor(steps, rollback, metrics);
    }
}
