using Abhyanvaya.Application.Common.Interfaces;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;

namespace Abhyanvaya.Infrastructure.Validation;

/// <summary>
/// Validates classroom attendance photos before upload and AI processing.
/// </summary>
public sealed class ClassroomImageValidator : IClassroomImageValidator
{
    public const long MaxBytes = 15 * 1024 * 1024;
    public const int MinWidth = 640;
    public const int MinHeight = 480;

    private static readonly HashSet<string> SupportedExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".webp" };

    public async Task<ClassroomImageValidationResult> ValidateAsync(
        Stream imageStream,
        string fileName,
        long fileSizeBytes,
        CancellationToken cancellationToken = default)
    {
        var formatResult = ValidateSupportedFormat(fileName, fileSizeBytes);
        if (!formatResult.IsValid)
        {
            return formatResult;
        }

        return await ValidateImageIntegrityAndResolutionAsync(imageStream, cancellationToken);
    }

    public ClassroomImageValidationResult ValidateSupportedFormat(string fileName, long fileSizeBytes)
    {
        if (fileSizeBytes <= 0)
        {
            return ClassroomImageValidationResult.Failure("Image file is required.");
        }

        if (fileSizeBytes > MaxBytes)
        {
            return ClassroomImageValidationResult.Failure("Classroom photo must be 15 MB or smaller.");
        }

        var extension = Path.GetExtension(fileName);
        if (string.IsNullOrWhiteSpace(extension) || !SupportedExtensions.Contains(extension))
        {
            return ClassroomImageValidationResult.Failure("Supported formats: JPG, JPEG, PNG, WebP.");
        }

        return ClassroomImageValidationResult.Success(0, 0);
    }

    public async Task<ClassroomImageValidationResult> ValidateImageIntegrityAndResolutionAsync(
        Stream imageStream,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (imageStream.CanSeek)
            {
                imageStream.Position = 0;
            }

            var info = await Image.IdentifyAsync(imageStream, cancellationToken);
            if (info == null)
            {
                return ClassroomImageValidationResult.Failure("The image file appears to be corrupt or unreadable.");
            }

            if (info.Width < MinWidth || info.Height < MinHeight)
            {
                return ClassroomImageValidationResult.Failure(
                    $"Classroom photo must be at least {MinWidth}×{MinHeight} pixels.");
            }

            return ClassroomImageValidationResult.Success(info.Width, info.Height);
        }
        catch (UnknownImageFormatException)
        {
            return ClassroomImageValidationResult.Failure("The image file appears to be corrupt or uses an unsupported format.");
        }
        catch (InvalidImageContentException)
        {
            return ClassroomImageValidationResult.Failure("The image file appears to be corrupt or unreadable.");
        }
        catch (Exception)
        {
            return ClassroomImageValidationResult.Failure("The image file could not be validated.");
        }
    }

    public Task<ClassroomImageValidationResult> ValidateOrientationAsync(Stream imageStream, CancellationToken cancellationToken = default) =>
        Task.FromResult(ClassroomImageValidationResult.Failure("Orientation validation is not implemented yet."));

    public Task<ClassroomImageValidationResult> ValidateBrightnessAsync(Stream imageStream, CancellationToken cancellationToken = default) =>
        Task.FromResult(ClassroomImageValidationResult.Failure("Brightness validation is not implemented yet."));

    public Task<ClassroomImageValidationResult> ValidateBlurAsync(Stream imageStream, CancellationToken cancellationToken = default) =>
        Task.FromResult(ClassroomImageValidationResult.Failure("Blur detection is not implemented yet."));

    public Task<ClassroomImageValidationResult> ValidateCameraQualityAsync(Stream imageStream, CancellationToken cancellationToken = default) =>
        Task.FromResult(ClassroomImageValidationResult.Failure("Camera quality validation is not implemented yet."));

    public Task<ClassroomImageValidationResult> ValidateFaceCountAsync(Stream imageStream, CancellationToken cancellationToken = default) =>
        Task.FromResult(ClassroomImageValidationResult.Failure("Face count validation is not implemented yet."));
}
