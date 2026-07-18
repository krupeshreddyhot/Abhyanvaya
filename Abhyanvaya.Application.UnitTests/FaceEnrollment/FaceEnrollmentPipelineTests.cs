using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.Enrollment.Validation;
using Abhyanvaya.Application.FaceEnrollment;
using Abhyanvaya.Domain.Entities;
using Abhyanvaya.Domain.Enums;
using Abhyanvaya.Infrastructure.FaceEnrollment;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;

namespace Abhyanvaya.Application.UnitTests.FaceEnrollment;

public sealed class FaceEnrollmentPipelineTests
{
    [Fact]
    public void EnrollmentProgressTracker_CalculatesProgressPercent()
    {
        var tracker = new EnrollmentProgressTracker(new InMemoryEnrollmentRepository());
        Assert.Equal(100m, tracker.CalculateProgressPercent(EnrollmentState.Completed));
        Assert.Equal(25m, tracker.CalculateProgressPercent(EnrollmentState.DetectingFace));
    }

    [Fact]
    public void EnrollmentQualityEngine_RejectsMultipleFaces()
    {
        var engine = new EnrollmentQualityEngine();
        var policy = Options.Create(new EnrollmentPolicyOptions()).Value;
        var configurable = new ConfigurableEnrollmentPolicy(Options.Create(new EnrollmentPolicyOptions()));

        var result = engine.ValidateFaceCount(new FaceDetectionResult
        {
            FaceCount = 2,
            TopConfidence = 0.9f,
            ImageWidth = 300,
            ImageHeight = 300,
        }, configurable);

        Assert.False(result.Passed);
    }

    [Fact]
    public void EnrollmentManifestGenerator_BuildsSections()
    {
        var batch = new FaceEnrollmentBatch { Id = Guid.NewGuid(), TenantId = 1, AcquisitionBatchId = Guid.NewGuid() };
        var jobs = new[]
        {
            new FaceEnrollmentJob
            {
                Id = Guid.NewGuid(),
                BatchId = batch.Id,
                AcquisitionItemId = Guid.NewGuid(),
                AcquisitionBatchId = batch.AcquisitionBatchId,
                TenantId = 1,
                StudentId = 1,
                StudentNumber = "S1",
                State = EnrollmentState.Completed,
            },
            new FaceEnrollmentJob
            {
                Id = Guid.NewGuid(),
                BatchId = batch.Id,
                AcquisitionItemId = Guid.NewGuid(),
                AcquisitionBatchId = batch.AcquisitionBatchId,
                TenantId = 1,
                StudentId = 2,
                StudentNumber = "S2",
                State = EnrollmentState.Failed,
                FailureReason = "Duplicate StudentNumber: S2",
            },
        };

        var manifest = new EnrollmentManifestGenerator().Generate(batch, jobs);
        Assert.Single(manifest.SuccessList);
        Assert.Single(manifest.DuplicateList);
    }

    [Fact]
    public async Task ArtifactUploadQueue_EnqueuesWithoutUploading()
    {
        var queue = new ArtifactUploadQueue();
        var artifact = new EnrollmentArtifact
        {
            StudentId = 1,
            PhotoReference = "photo://1",
            AlignedPhotoReference = "aligned://1",
            EmbeddingReference = "embedding://1",
            EmbeddingDimension = 512,
            EmbeddingVersion = "1.0",
            QualityScore = 0.9m,
            ManifestId = Guid.NewGuid(),
            EnrollmentVersion = "1.0",
            CreatedUtc = DateTime.UtcNow,
            Checksum = "ABC",
        };

        await queue.EnqueueAsync(artifact);
        Assert.Equal(1, queue.QueueDepth);
    }

    [Fact]
    public async Task EnrollmentBatchProcessor_CompletesSingleEnrollment()
    {
        var repository = new InMemoryEnrollmentRepository();
        var batch = await repository.CreateBatchAsync(Guid.NewGuid(), 1, new[]
        {
            new StudentPhotoAcquisitionItem
            {
                Id = Guid.NewGuid(),
                BatchId = Guid.NewGuid(),
                TenantId = 1,
                StudentId = 1,
                StudentNumber = "S1",
                CollegeCode = "C",
                PhotoBytes = CreateJpeg(1),
            },
        }, CancellationToken.None);

        var jobs = await repository.GetJobsByBatchAsync(batch.Id, CancellationToken.None);
        var processor = CreateProcessor(repository);
        var photoMap = new Dictionary<Guid, byte[]> { [jobs[0].AcquisitionItemId] = CreateJpeg(1) };
        var result = await processor.ProcessBatchAsync(batch, photoMap, CancellationToken.None);

        Assert.Equal(1, result.Statistics.Completed);
        Assert.Single(result.Manifest.SuccessList);
    }

    [Fact]
    public async Task EnrollmentBatchProcessor_ProcessesLargeBatchConcurrently()
    {
        var acquisitionBatchId = Guid.NewGuid();
        var items = Enumerable.Range(1, 200).Select(i => new StudentPhotoAcquisitionItem
        {
            Id = Guid.NewGuid(),
            BatchId = acquisitionBatchId,
            TenantId = 1,
            StudentId = i,
            StudentNumber = $"S{i}",
            CollegeCode = "C",
            PhotoBytes = CreateJpeg(i),
        }).ToList();

        var repository = new InMemoryEnrollmentRepository();
        var batch = await repository.CreateBatchAsync(acquisitionBatchId, 1, items, CancellationToken.None);
        var jobs = await repository.GetJobsByBatchAsync(batch.Id, CancellationToken.None);
        var photoMap = jobs.ToDictionary(j => j.AcquisitionItemId, j => CreateJpeg(j.StudentId));

        var processor = CreateProcessor(repository, maxParallelism: 32);
        var result = await processor.ProcessBatchAsync(batch, photoMap, CancellationToken.None);

        Assert.Equal(200, result.Statistics.Completed);
        Assert.Equal(200, result.Manifest.SuccessList.Count);
    }

    private static EnrollmentBatchProcessor CreateProcessor(InMemoryEnrollmentRepository repository, int maxParallelism = 8)
    {
        var detection = new Mock<IFaceDetectionEngine>();
        detection.Setup(d => d.DetectAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FaceDetectionResult { FaceCount = 1, TopConfidence = 0.95f, ImageWidth = 300, ImageHeight = 300 });

        var alignment = new Mock<IFaceAlignmentEngine>();
        alignment.Setup(a => a.AlignAsync(It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FaceAlignmentResult { Success = true, AlignedFaceBytes = CreateJpeg(99), ContentType = "image/jpeg" });

        var embeddingEngine = new Mock<IEmbeddingEngine>();
        embeddingEngine.Setup(e => e.ExpectedDimension).Returns(4);
        embeddingEngine.Setup(e => e.ModelVersion).Returns("1.0");
        embeddingEngine.Setup(e => e.GenerateFromAlignedFaceAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new EmbeddingEngineResult(new[] { 1f, 0f, 0f, 0f }, 1));

        var validator = new Mock<IEmbeddingValidator>();
        validator.Setup(v => v.ValidateNormalized(It.IsAny<float[]>(), It.IsAny<int?>()))
            .Returns(new EmbeddingValidationResult(true, 4));

        var normalizer = new Mock<IEmbeddingNormalizer>();
        normalizer.Setup(n => n.Normalize(It.IsAny<float[]>())).Returns<float[]>(v => v);

        var tracing = new Mock<IAITracingService>();
        tracing.Setup(t => t.CreateContext(It.IsAny<Guid?>(), It.IsAny<int?>(), It.IsAny<string>()))
            .Returns((Guid? cid, int? tid, string? pid) => new Application.AIOperations.AITraceContext
            {
                TraceId = Guid.NewGuid(),
                CorrelationId = cid ?? Guid.NewGuid(),
                CurrentSpanId = Guid.NewGuid(),
            });
        tracing.Setup(t => t.StartSpan(It.IsAny<Application.AIOperations.AITraceContext>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns((Application.AIOperations.AITraceContext ctx, string _, string __) => ctx);

        var duplicateDetector = new Mock<IEnrollmentDuplicateDetector>();
        duplicateDetector.Setup(d => d.DetectAsync(It.IsAny<Application.Enrollment.Persistence.EnrollmentDuplicateDetectionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Application.Enrollment.Persistence.EnrollmentDuplicateDetectionResult { IsDuplicate = false });

        return new EnrollmentBatchProcessor(
            detection.Object,
            alignment.Object,
            embeddingEngine.Object,
            validator.Object,
            normalizer.Object,
            new EnrollmentQualityEngine(),
            new EnrollmentDuplicateDetectorService(duplicateDetector.Object),
            new EnrollmentArtifactBuilder(),
            new EnrollmentProgressTracker(repository),
            new EnrollmentFailureHandler(repository, new EnrollmentProgressTracker(repository), NullLogger<EnrollmentFailureHandler>.Instance),
            repository,
            new EnrollmentManifestGenerator(),
            new ArtifactUploadQueue(),
            tracing.Object,
            Mock.Of<IAITelemetryService>(),
            new ConfigurableEnrollmentPolicy(Options.Create(new EnrollmentPolicyOptions())),
            Options.Create(new EnrollmentPolicyOptions { MaxParallelism = maxParallelism }),
            NullLogger<EnrollmentBatchProcessor>.Instance);
    }

    private static byte[] CreateJpeg(int seed)
    {
        using var image = new Image<Rgb24>(200 + seed, 256);
        image.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < accessor.Height; y++)
            {
                var row = accessor.GetRowSpan(y);
                foreach (ref var pixel in row)
                {
                    pixel = new Rgb24((byte)((y + seed) % 255), (byte)(seed % 255), 64);
                }
            }
        });

        using var ms = new MemoryStream();
        image.Save(ms, new JpegEncoder());
        return ms.ToArray();
    }

    private sealed class InMemoryEnrollmentRepository : IEnrollmentRepository
    {
        private readonly Dictionary<Guid, FaceEnrollmentBatch> _batches = new();
        private readonly Dictionary<Guid, FaceEnrollmentJob> _jobs = new();

        public Task<FaceEnrollmentBatch> CreateBatchAsync(Guid acquisitionBatchId, int tenantId, IReadOnlyList<StudentPhotoAcquisitionItem> items, CancellationToken cancellationToken = default)
        {
            var batchId = Guid.NewGuid();
            var batch = new FaceEnrollmentBatch
            {
                Id = batchId,
                AcquisitionBatchId = acquisitionBatchId,
                TenantId = tenantId,
                TotalItems = items.Count,
                CreatedUtc = DateTime.UtcNow,
            };
            _batches[batchId] = batch;

            foreach (var item in items)
            {
                var job = new FaceEnrollmentJob
                {
                    Id = Guid.NewGuid(),
                    BatchId = batchId,
                    AcquisitionItemId = item.Id,
                    AcquisitionBatchId = acquisitionBatchId,
                    TenantId = tenantId,
                    StudentId = item.StudentId,
                    StudentNumber = item.StudentNumber,
                    CorrelationId = Guid.NewGuid(),
                    TraceId = Guid.NewGuid(),
                    CreatedUtc = DateTime.UtcNow,
                };
                _jobs[job.Id] = job;
            }

            return Task.FromResult(batch);
        }

        public Task<FaceEnrollmentBatch?> GetBatchAsync(Guid batchId, CancellationToken cancellationToken = default)
            => Task.FromResult(_batches.GetValueOrDefault(batchId));

        public Task<FaceEnrollmentJob?> GetJobAsync(Guid enrollmentId, CancellationToken cancellationToken = default)
            => Task.FromResult(_jobs.GetValueOrDefault(enrollmentId));

        public Task UpdateJobAsync(FaceEnrollmentJob job, CancellationToken cancellationToken = default)
        {
            _jobs[job.Id] = job;
            return Task.CompletedTask;
        }

        public Task UpdateBatchAsync(FaceEnrollmentBatch batch, CancellationToken cancellationToken = default)
        {
            _batches[batch.Id] = batch;
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<FaceEnrollmentJob>> GetIncompleteJobsAsync(Guid batchId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<FaceEnrollmentJob>>(_jobs.Values.Where(j => j.BatchId == batchId && j.State != EnrollmentState.Completed).ToList());

        public Task<IReadOnlyList<FaceEnrollmentJob>> GetJobsByBatchAsync(Guid batchId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<FaceEnrollmentJob>>(_jobs.Values.Where(j => j.BatchId == batchId).ToList());
    }
}
