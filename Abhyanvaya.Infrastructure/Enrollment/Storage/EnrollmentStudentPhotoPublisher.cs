using Abhyanvaya.Application;
using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace Abhyanvaya.Infrastructure.Enrollment.Storage;

public sealed class EnrollmentStudentPhotoPublisher : IEnrollmentStudentPhotoPublisher
{
    private static readonly IReadOnlyDictionary<string, int> PhotoVariantMaxEdges =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["original"] = 800,
            ["thumbnail"] = 200,
        };

    private readonly IObjectStorageProvider _objectStorage;
    private readonly IApplicationDbContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly TimeProvider _clock;

    public EnrollmentStudentPhotoPublisher(
        IObjectStorageProvider objectStorage,
        IApplicationDbContext context,
        IUnitOfWork unitOfWork,
        TimeProvider clock)
    {
        _objectStorage = objectStorage;
        _context = context;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<EnrollmentStudentPhotoPublishResult> PublishAsync(
        EnrollmentStudentPhotoPublishRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.PhotoBytes is not { Length: > 0 })
        {
            return EnrollmentStudentPhotoPublishResult.Failed("Downloaded photo bytes are empty.");
        }

        var item = await _context.StudentEnrollmentItems
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(i => i.Id == request.ItemId, cancellationToken);

        if (item is null)
        {
            return EnrollmentStudentPhotoPublishResult.Failed("Enrollment item not found.");
        }

        var student = await _context.Students
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.Id == request.StudentId && s.TenantId == request.TenantId, cancellationToken);

        if (student is null)
        {
            return EnrollmentStudentPhotoPublishResult.Failed("Student not found.");
        }

        var storagePath = StudentMediaPaths.BuildStoragePath(request.TenantId, request.StudentId);
        var uploadedUtc = _clock.GetUtcNow().UtcDateTime;

        try
        {
            var variants = await BuildWebpVariantsAsync(request.PhotoBytes, cancellationToken);
            foreach (var (variant, bytes) in variants)
            {
                var objectKey = $"{storagePath.Trim('/')}/{variant}.webp";
                await using var stream = new MemoryStream(bytes, writable: false);
                await _objectStorage.WriteObjectAsync(objectKey, stream, "image/webp", cancellationToken);
            }

            await _unitOfWork.ExecuteInTransactionAsync(async ct =>
            {
                student.PhotoKey = storagePath;
                student.PhotoUploadedUtc = uploadedUtc;
                student.PhotoVerified = false;
                student.UpdatedDate = uploadedUtc;
                item.PhotoKey = storagePath;
                await _unitOfWork.SaveChangesAsync(ct);
            }, cancellationToken);

            return EnrollmentStudentPhotoPublishResult.Succeeded(storagePath, uploadedUtc);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return EnrollmentStudentPhotoPublishResult.Failed(ex.Message);
        }
    }

    private static async Task<IReadOnlyDictionary<string, byte[]>> BuildWebpVariantsAsync(
        byte[] photoBytes,
        CancellationToken cancellationToken)
    {
        using var image = await Image.LoadAsync(new MemoryStream(photoBytes, writable: false), cancellationToken);
        var result = new Dictionary<string, byte[]>(PhotoVariantMaxEdges.Count, StringComparer.OrdinalIgnoreCase);

        foreach (var (variant, maxEdge) in PhotoVariantMaxEdges)
        {
            using var clone = image.Clone(ctx =>
            {
                ctx.Resize(new ResizeOptions
                {
                    Mode = ResizeMode.Max,
                    Size = new Size(maxEdge, maxEdge),
                });
            });

            await using var ms = new MemoryStream();
            await clone.SaveAsWebpAsync(ms, cancellationToken);
            result[variant] = ms.ToArray();
        }

        return result;
    }
}
