using Abhyanvaya.API.Media;
using Abhyanvaya.Application;
using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.DTOs.Student;
using Microsoft.Extensions.Options;

namespace Abhyanvaya.API.Services;

/// <summary>
/// Uploads student photos as WebP originals (max edge 800px) and thumbnails (200px).
/// </summary>
public sealed class StudentPhotoService : IStudentPhotoService
{
    private const long MaxBytes = 5 * 1024 * 1024;

    private static readonly IReadOnlyDictionary<string, int> PhotoVariantMaxEdges =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["original"] = 800,
            ["thumbnail"] = 200,
        };

    private static readonly IReadOnlyList<string> PhotoVariantNames =
        PhotoVariantMaxEdges.Keys.ToList();

    private readonly IApplicationDbContext _context;
    private readonly IStudentRepository _studentRepository;
    private readonly Abhyanvaya.API.Media.IMediaStorageService _mediaStorage;
    private readonly IOptions<MediaOptions> _mediaOptions;
    private readonly IStudentPhotoEmbeddingQueue _embeddingQueue;
    private readonly ILogger<StudentPhotoService> _logger;

    public StudentPhotoService(
        IApplicationDbContext context,
        IStudentRepository studentRepository,
        Abhyanvaya.API.Media.IMediaStorageService mediaStorage,
        IOptions<MediaOptions> mediaOptions,
        IStudentPhotoEmbeddingQueue embeddingQueue,
        ILogger<StudentPhotoService> logger)
    {
        _context = context;
        _studentRepository = studentRepository;
        _mediaStorage = mediaStorage;
        _mediaOptions = mediaOptions;
        _embeddingQueue = embeddingQueue;
        _logger = logger;
    }

    public async Task<(bool Ok, string? Error, StudentPhotoUploadResult? Result)> UploadPhotoAsync(
        int tenantId,
        int studentId,
        IFormFile file,
        CancellationToken cancellationToken)
    {
        var validation = _mediaStorage.ValidateRasterUpload(file, MaxBytes);
        if (!validation.Ok)
            return (false, validation.Error, null);

        var student = await _studentRepository.GetByIdForTenantAsync(studentId, tenantId, cancellationToken);

        if (student is null)
            return (false, "Student not found for this tenant.", null);

        var storagePath = StudentMediaPaths.BuildStoragePath(tenantId, studentId);

        try
        {
            await using var input = file.OpenReadStream();
            var variants = await _mediaStorage.BuildWebpVariantsAsync(input, PhotoVariantMaxEdges, cancellationToken);
            await _mediaStorage.SaveVariantsAsync(storagePath, variants, cancellationToken);

            var uploadedUtc = DateTime.UtcNow;
            student.PhotoKey = storagePath;
            student.PhotoUploadedUtc = uploadedUtc;
            student.PhotoVerified = false;
            student.UpdatedDate = uploadedUtc;
            await _context.SaveChangesAsync(cancellationToken);

            await _embeddingQueue.EnqueueAsync(
                new StudentPhotoUploadedMessage(
                    tenantId,
                    studentId,
                    storagePath,
                    null,
                    uploadedUtc,
                    Regenerate: true),
                cancellationToken);

            _logger.LogInformation(
                "Student photo uploaded; embedding job enqueued. StudentId={StudentId} TenantId={TenantId}",
                studentId,
                tenantId);

            var publicBaseUrl = _mediaOptions.Value.PublicBaseUrl;
            var result = new StudentPhotoUploadResult
            {
                PhotoKey = storagePath,
                PhotoUploadedUtc = uploadedUtc,
                OriginalUrl = StudentMediaPaths.BuildVariantPath(storagePath, uploadedUtc, "original", publicBaseUrl),
                ThumbnailUrl = StudentMediaPaths.BuildVariantPath(storagePath, uploadedUtc, "thumbnail", publicBaseUrl),
            };

            return (true, null, result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process student photo for tenant {TenantId}, student {StudentId}", tenantId, studentId);
            if (_mediaStorage.IsStorageOrNetworkFailure(ex))
            {
                return (false, "Storage upload failed. Verify media storage endpoint/region/bucket credentials on server.", null);
            }

            return (false, "Could not read or resize the image. Try another file.", null);
        }
    }

    public async Task<(bool Ok, string? Error)> DeletePhotoAsync(
        int tenantId,
        int studentId,
        CancellationToken cancellationToken)
    {
        var student = await _studentRepository.GetByIdForTenantAsync(studentId, tenantId, cancellationToken);

        if (student is null)
            return (false, "Student not found for this tenant.");

        try
        {
            if (!string.IsNullOrWhiteSpace(student.PhotoKey))
            {
                await _mediaStorage.DeleteVariantsAsync(student.PhotoKey, PhotoVariantNames, cancellationToken);
            }

            student.PhotoKey = null;
            student.PhotoUploadedUtc = null;
            student.PhotoVerified = false;
            student.UpdatedDate = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);

            return (true, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete student photo for tenant {TenantId}, student {StudentId}", tenantId, studentId);
            if (_mediaStorage.IsStorageOrNetworkFailure(ex))
            {
                return (false, "Storage delete failed. Verify media storage endpoint/region/bucket credentials on server.");
            }

            throw;
        }
    }

    public async Task<StudentPhotoDto?> GetPhotoAsync(
        int studentId,
        int? tenantId,
        CancellationToken cancellationToken)
    {
        var student = tenantId is null
            ? await _studentRepository.GetByIdAsync(studentId, cancellationToken)
            : await _studentRepository.GetByIdForTenantAsync(studentId, tenantId.Value, cancellationToken);

        if (student is null)
            return null;

        var hasPhoto = !string.IsNullOrWhiteSpace(student.PhotoKey) && student.PhotoUploadedUtc is not null;
        if (!hasPhoto)
        {
            return new StudentPhotoDto
            {
                HasPhoto = false,
                PhotoKey = null,
                PhotoUploadedUtc = null,
                PhotoVerified = false,
                OriginalUrl = null,
                ThumbnailUrl = null,
            };
        }

        var publicBaseUrl = _mediaOptions.Value.PublicBaseUrl;
        return new StudentPhotoDto
        {
            HasPhoto = true,
            PhotoKey = student.PhotoKey,
            PhotoUploadedUtc = student.PhotoUploadedUtc,
            PhotoVerified = student.PhotoVerified,
            OriginalUrl = StudentMediaPaths.BuildVariantPath(
                student.PhotoKey, student.PhotoUploadedUtc, "original", publicBaseUrl),
            ThumbnailUrl = StudentMediaPaths.BuildVariantPath(
                student.PhotoKey, student.PhotoUploadedUtc, "thumbnail", publicBaseUrl),
        };
    }
}
