using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.Enrollment.Storage;
using Abhyanvaya.Application.Enrollment.Validation;
using Abhyanvaya.Infrastructure.Enrollment.Storage;
using Abhyanvaya.Infrastructure.Enrollment.Storage.Pipeline;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Abhyanvaya.Application.UnitTests.Enrollment.Storage;

public sealed class EnrollmentStoragePipelineExecutorTests
{
    [Fact]
    public void DependencyInjection_ResolvesPipelineExecutorAsInterface()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IStorageMetricsCollector, NoOpStorageMetricsCollector>();
        services.AddSingleton(Mock.Of<IObjectStorageProvider>());
        services.AddScoped<IEnrollmentStorageStep, ValidateInputStep>();
        services.AddScoped<RollbackStep>();
        services.AddScoped<EnrollmentStoragePipelineExecutor>();
        services.AddScoped<IEnrollmentStoragePipelineExecutor>(sp =>
            sp.GetRequiredService<EnrollmentStoragePipelineExecutor>());

        using var provider = services.BuildServiceProvider();
        var executor = provider.GetRequiredService<IEnrollmentStoragePipelineExecutor>();

        Assert.IsType<EnrollmentStoragePipelineExecutor>(executor);
    }

    [Fact]
    public async Task StoreAsync_DelegatesToPipelineExecutor()
    {
        var pipeline = new Mock<IEnrollmentStoragePipelineExecutor>();
        var request = CreateMinimalRequest();
        var primaryRecord = new Domain.Entities.EnrollmentStorageRecord
        {
            Id = Guid.NewGuid(),
            StorageGroupId = Guid.NewGuid(),
            TenantId = request.TenantId,
            CollegeId = request.CollegeId,
            AcademicYear = request.AcademicYear,
            StudentId = request.StudentId,
            BatchId = request.BatchId,
            ItemId = request.ItemId,
            ArtifactType = EnrollmentArtifactTypeNames.AlignedFace,
            ObjectKey = "tenant/student/AlignedFace/v1.bin",
            StorageProvider = "Local",
            Checksum = "abc",
            ContentType = "application/octet-stream",
            FileSize = 8,
            ImageWidth = 112,
            ImageHeight = 112,
            ArtifactVersion = 1,
            StorageVersion = EnrollmentStorageVersions.StorageSchemaVersion,
            PipelineVersion = request.PipelineVersion,
            ValidationVersion = EnrollmentStorageVersions.ValidationSchemaVersion,
            CorrelationId = request.Artifact.CorrelationId,
            IsPrimary = true,
            CreatedUtc = DateTime.UtcNow,
        };

        pipeline.Setup(p => p.ExecuteAsync(It.IsAny<EnrollmentStoragePipelineContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((EnrollmentStoragePipelineContext ctx, CancellationToken _) =>
            {
                ctx.PrimaryRecord = primaryRecord;
                ctx.Manifest = new EnrollmentStorageManifest
                {
                    ManifestId = ctx.ManifestId,
                    StorageGroupId = ctx.StorageGroupId,
                    Entries = [],
                    CreatedUtc = ctx.CreatedUtc,
                    ManifestVersion = EnrollmentStorageVersions.CurrentManifestVersion,
                    SchemaVersion = EnrollmentStorageVersions.ManifestSchemaVersion,
                    PipelineVersion = request.PipelineVersion,
                    ValidationVersion = EnrollmentStorageVersions.ValidationSchemaVersion,
                    StorageVersion = EnrollmentStorageVersions.StorageSchemaVersion,
                    ArtifactVersion = 1,
                    CorrelationId = request.Artifact.CorrelationId,
                };
                return ctx;
            });

        var service = new EnrollmentStorageService(
            pipeline.Object,
            Mock.Of<IEnrollmentStorageRecordRepository>(),
            TimeProvider.System,
            NullLogger<EnrollmentStorageService>.Instance);

        var result = await service.StoreAsync(request);

        Assert.True(result.Success);
        pipeline.Verify(
            p => p.ExecuteAsync(It.IsAny<EnrollmentStoragePipelineContext>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static EnrollmentStorageRequest CreateMinimalRequest() =>
        new()
        {
            TenantId = 1,
            CollegeId = 10,
            AcademicYear = 2026,
            StudentId = 42,
            BatchId = Guid.NewGuid(),
            ItemId = Guid.NewGuid(),
            PipelineVersion = 1,
            ExecutionTraceId = Guid.NewGuid(),
            ValidationProfile = ValidationProfileKind.Default,
            Artifact = new EnrollmentValidationArtifact
            {
                Report = new ValidationReport
                {
                    OverallResult = ValidationOverallResult.Passed,
                    FaceCount = 1,
                    RuleResults = [],
                    ValidationFailures = [],
                    Warnings = [],
                    FaceWidth = 112,
                    FaceHeight = 112,
                },
                AlignedFaceImage = [0x01, 0x02, 0x03, 0x04],
                TimestampUtc = DateTimeOffset.UtcNow,
                CorrelationId = Guid.NewGuid(),
            },
        };
}
