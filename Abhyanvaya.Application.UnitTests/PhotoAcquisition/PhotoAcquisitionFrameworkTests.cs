using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.PhotoAcquisition;
using Abhyanvaya.Domain.Entities;
using Abhyanvaya.Domain.Enums;
using Abhyanvaya.Infrastructure.PhotoAcquisition;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;

namespace Abhyanvaya.Application.UnitTests.PhotoAcquisition;

public sealed class PhotoAcquisitionFrameworkTests
{
    private static byte[] CreateValidJpeg(int width = 256, int height = 256, int uniqueSeed = 0)
    {
        using var image = new Image<Rgb24>(width, height);
        image.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < accessor.Height; y++)
            {
                var row = accessor.GetRowSpan(y);
                foreach (ref var pixel in row)
                {
                    pixel = new Rgb24((byte)((y + uniqueSeed) % 255), (byte)(uniqueSeed % 255), 64);
                }
            }
        });

        using var ms = new MemoryStream();
        image.Save(ms, new JpegEncoder());
        return ms.ToArray();
    }

    [Fact]
    public void PhotoValidationService_RejectsEmptyImage()
    {
        var service = new PhotoValidationService(Options.Create(new PhotoAcquisitionOptions()));
        var result = service.Validate(Array.Empty<byte>(), "image/jpeg");

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("empty", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void PhotoValidationService_RejectsCorruptImage()
    {
        var service = new PhotoValidationService(Options.Create(new PhotoAcquisitionOptions()));
        var result = service.Validate(new byte[] { 1, 2, 3, 4 }, "image/jpeg");

        Assert.False(result.IsValid);
    }

    [Fact]
    public void PhotoValidationService_DetectsDuplicateHash()
    {
        var service = new PhotoValidationService(Options.Create(new PhotoAcquisitionOptions()));
        var bytes = CreateValidJpeg();
        var hash = PhotoValidationService.ComputeHash(bytes);

        var result = service.Validate(bytes, "image/jpeg", new HashSet<string> { hash });

        Assert.False(result.IsValid);
        Assert.True(result.IsDuplicate);
    }

    [Fact]
    public void PhotoQualityAssessmentService_ReturnsScores()
    {
        var service = new PhotoQualityAssessmentService();
        var report = service.Assess(CreateValidJpeg());

        Assert.InRange(report.BlurScore, 0m, 1m);
        Assert.InRange(report.Brightness, 0m, 1m);
        Assert.InRange(report.OverallScore, 0m, 1m);
        Assert.Equal(0.5m, report.FaceVisibilityScore);
    }

    [Fact]
    public void PhotoRetryPolicy_RetriesTransientFailures()
    {
        var policy = new PhotoRetryPolicy(Options.Create(new PhotoAcquisitionOptions { MaxRetryAttempts = 3 }));
        var result = new PhotoDownloadResult
        {
            Success = false,
            SourceReference = "ref",
            IsRetryable = true,
        };

        Assert.True(policy.ShouldRetry(1, result));
        Assert.False(policy.ShouldRetry(3, result));
    }

    [Fact]
    public void PhotoManifestGenerator_BuildsManifestSections()
    {
        var batch = new StudentPhotoAcquisitionBatch
        {
            Id = Guid.NewGuid(),
            TenantId = 1,
            ProviderName = "ExamBranch",
            AcademicYear = 2026,
        };

        var items = new[]
        {
            new StudentPhotoAcquisitionItem
            {
                Id = Guid.NewGuid(),
                BatchId = batch.Id,
                TenantId = 1,
                StudentId = 1,
                StudentNumber = "S1",
                CollegeCode = "C1",
                Status = PhotoAcquisitionItemStatus.ReadyForEnrollment,
                SourceReference = "ref1",
            },
            new StudentPhotoAcquisitionItem
            {
                Id = Guid.NewGuid(),
                BatchId = batch.Id,
                TenantId = 1,
                StudentId = 2,
                StudentNumber = "S2",
                CollegeCode = "C1",
                Status = PhotoAcquisitionItemStatus.Failed,
                SourceReference = "ref2",
            },
        };

        var manifest = new PhotoManifestGenerator().Generate(batch, items);

        Assert.Single(manifest.Entries);
        Assert.Single(manifest.FailedEntries);
    }

    [Fact]
    public async Task PhotoDownloadCoordinator_ProcessesThousandDownloadsConcurrently()
    {
        var repository = new InMemoryPhotoDownloadRepository();
        var downloader = new Mock<IStudentPhotoDownloader>();
        downloader.Setup(d => d.DownloadAsync(It.IsAny<PhotoSourceResolution>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PhotoSourceResolution source, CancellationToken _) => new PhotoDownloadResult
            {
                Success = true,
                PhotoBytes = CreateValidJpeg(width: 200 + source.Student.StudentId, height: 256, uniqueSeed: source.Student.StudentId),
                ContentType = "image/jpeg",
                SourceReference = $"ref-{source.Student.StudentId}",
            });

        var source = new Mock<IStudentPhotoSource>();
        source.Setup(s => s.ResolveAsync(It.IsAny<PhotoAcquisitionStudentMaster>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PhotoAcquisitionStudentMaster student, CancellationToken _) => new PhotoSourceResolution
            {
                ProviderName = "ExamBranch",
                Student = student,
                SourceReference = $"ref-{student.StudentId}",
            });

        var coordinator = CreateCoordinator(repository, source.Object, downloader.Object, maxConcurrency: 32);
        var students = Enumerable.Range(1, 1000)
            .Select(i => new PhotoAcquisitionStudentMaster
            {
                TenantId = 1,
                StudentId = i,
                StudentNumber = $"S{i}",
                CollegeCode = "COL",
                AcademicYear = 2026,
            })
            .ToList();

        var result = await coordinator.RunBatchAsync(new PhotoAcquisitionBatchRequest
        {
            TenantId = 1,
            ProviderName = "ExamBranch",
            AcademicYear = 2026,
            Students = students,
        });

        Assert.Equal(1000, result.ReadyForEnrollmentCount);
        Assert.Equal(1000, result.SucceededCount);
        Assert.Equal(0, result.FailedCount);
    }

    [Fact]
    public async Task PhotoDownloadCoordinator_QueuesRetryOnTimeout()
    {
        var repository = new InMemoryPhotoDownloadRepository();
        var downloader = new Mock<IStudentPhotoDownloader>();
        downloader.Setup(d => d.DownloadAsync(It.IsAny<PhotoSourceResolution>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PhotoDownloadResult
            {
                Success = false,
                SourceReference = "ref",
                FailureReason = "Download timed out.",
                IsRetryable = true,
            });

        var source = new Mock<IStudentPhotoSource>();
        source.Setup(s => s.ResolveAsync(It.IsAny<PhotoAcquisitionStudentMaster>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PhotoAcquisitionStudentMaster student, CancellationToken _) => new PhotoSourceResolution
            {
                ProviderName = "ExamBranch",
                Student = student,
                SourceReference = "ref",
            });

        var coordinator = CreateCoordinator(repository, source.Object, downloader.Object);
        var result = await coordinator.RunBatchAsync(new PhotoAcquisitionBatchRequest
        {
            TenantId = 1,
            ProviderName = "ExamBranch",
            AcademicYear = 2026,
            Students = new[]
            {
                new PhotoAcquisitionStudentMaster
                {
                    TenantId = 1,
                    StudentId = 1,
                    StudentNumber = "S1",
                    CollegeCode = "COL",
                    AcademicYear = 2026,
                },
            },
        });

        Assert.Equal(1, result.RetryQueuedCount);
    }

    [Fact]
    public async Task PhotoDownloadCoordinator_MarksCorruptFileInvalid()
    {
        var repository = new InMemoryPhotoDownloadRepository();
        var downloader = new Mock<IStudentPhotoDownloader>();
        downloader.Setup(d => d.DownloadAsync(It.IsAny<PhotoSourceResolution>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PhotoDownloadResult
            {
                Success = true,
                PhotoBytes = new byte[] { 9, 8, 7 },
                ContentType = "image/jpeg",
                SourceReference = "ref",
            });

        var source = new Mock<IStudentPhotoSource>();
        source.Setup(s => s.ResolveAsync(It.IsAny<PhotoAcquisitionStudentMaster>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PhotoAcquisitionStudentMaster student, CancellationToken _) => new PhotoSourceResolution
            {
                ProviderName = "ExamBranch",
                Student = student,
                SourceReference = "ref",
            });

        var coordinator = CreateCoordinator(repository, source.Object, downloader.Object);
        var result = await coordinator.RunBatchAsync(new PhotoAcquisitionBatchRequest
        {
            TenantId = 1,
            ProviderName = "ExamBranch",
            AcademicYear = 2026,
            Students = new[]
            {
                new PhotoAcquisitionStudentMaster
                {
                    TenantId = 1,
                    StudentId = 1,
                    StudentNumber = "S1",
                    CollegeCode = "COL",
                    AcademicYear = 2026,
                },
            },
        });

        Assert.Equal(1, result.FailedCount);
    }

    private static PhotoDownloadCoordinator CreateCoordinator(
        IPhotoDownloadRepository repository,
        IStudentPhotoSource source,
        IStudentPhotoDownloader downloader,
        int maxConcurrency = 8)
    {
        return new PhotoDownloadCoordinator(
            repository,
            source,
            downloader,
            new PhotoValidationService(Options.Create(new PhotoAcquisitionOptions())),
            new PhotoQualityAssessmentService(),
            new PhotoRetryPolicy(Options.Create(new PhotoAcquisitionOptions { MaxRetryAttempts = 3 })),
            new PhotoManifestGenerator(),
            new PhotoDownloadQueue(),
            Mock.Of<IEnrollmentJobQueue>(),
            Options.Create(new PhotoAcquisitionOptions { MaxConcurrentDownloads = maxConcurrency }),
            NullLogger<PhotoDownloadCoordinator>.Instance);
    }

    private sealed class InMemoryPhotoDownloadRepository : IPhotoDownloadRepository
    {
        private readonly Dictionary<Guid, StudentPhotoAcquisitionBatch> _batches = new();
        private readonly Dictionary<Guid, StudentPhotoAcquisitionItem> _items = new();

        public Task<StudentPhotoAcquisitionBatch> CreateBatchAsync(PhotoAcquisitionBatchRequest request, CancellationToken cancellationToken = default)
        {
            var batchId = Guid.NewGuid();
            var batch = new StudentPhotoAcquisitionBatch
            {
                Id = batchId,
                TenantId = request.TenantId,
                ProviderName = request.ProviderName,
                AcademicYear = request.AcademicYear,
                TotalItems = request.Students.Count,
                CreatedUtc = DateTime.UtcNow,
            };

            _batches[batchId] = batch;
            foreach (var student in request.Students)
            {
                var item = new StudentPhotoAcquisitionItem
                {
                    Id = Guid.NewGuid(),
                    BatchId = batchId,
                    TenantId = student.TenantId,
                    StudentId = student.StudentId,
                    StudentNumber = student.StudentNumber,
                    CollegeCode = student.CollegeCode,
                    CreatedUtc = DateTime.UtcNow,
                };
                _items[item.Id] = item;
            }

            return Task.FromResult(batch);
        }

        public Task<StudentPhotoAcquisitionBatch?> GetBatchAsync(Guid batchId, CancellationToken cancellationToken = default)
            => Task.FromResult(_batches.GetValueOrDefault(batchId));

        public Task<IReadOnlyList<StudentPhotoAcquisitionItem>> GetBatchItemsAsync(Guid batchId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<StudentPhotoAcquisitionItem>>(_items.Values.Where(i => i.BatchId == batchId).ToList());

        public Task UpdateItemAsync(StudentPhotoAcquisitionItem item, CancellationToken cancellationToken = default)
        {
            _items[item.Id] = item;
            return Task.CompletedTask;
        }

        public Task UpdateBatchAsync(StudentPhotoAcquisitionBatch batch, CancellationToken cancellationToken = default)
        {
            _batches[batch.Id] = batch;
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<StudentPhotoAcquisitionItem>> GetRetryReadyItemsAsync(Guid batchId, DateTime utcNow, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<StudentPhotoAcquisitionItem>>(_items.Values
                .Where(i => i.BatchId == batchId && i.Status == PhotoAcquisitionItemStatus.RetryQueued)
                .ToList());

        public Task<IReadOnlyList<StudentPhotoAcquisitionItem>> GetEnrollmentReadyItemsAsync(Guid batchId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<StudentPhotoAcquisitionItem>>(_items.Values
                .Where(i => i.BatchId == batchId && i.Status == PhotoAcquisitionItemStatus.ReadyForEnrollment)
                .ToList());
    }
}
