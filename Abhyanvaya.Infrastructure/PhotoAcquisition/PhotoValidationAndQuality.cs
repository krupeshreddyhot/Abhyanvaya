using System.Security.Cryptography;
using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.PhotoAcquisition;
using Microsoft.Extensions.Options;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;
using SixLabors.ImageSharp.PixelFormats;

namespace Abhyanvaya.Infrastructure.PhotoAcquisition;

public sealed class PhotoRetryPolicy : IPhotoRetryPolicy
{
    private readonly PhotoAcquisitionOptions _options;

    public PhotoRetryPolicy(IOptions<PhotoAcquisitionOptions> options)
    {
        _options = options.Value;
    }

    public bool ShouldRetry(int attemptCount, PhotoDownloadResult result)
        => result.IsRetryable && attemptCount < _options.MaxRetryAttempts;

    public TimeSpan GetDelay(int attemptCount)
        => TimeSpan.FromSeconds(Math.Pow(2, Math.Max(0, attemptCount)));
}

public sealed class PhotoValidationService : IPhotoValidationService
{
    private static readonly HashSet<string> SupportedFormats = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/jpg",
        "image/png",
        "image/webp",
    };

    private readonly PhotoAcquisitionOptions _options;

    public PhotoValidationService(IOptions<PhotoAcquisitionOptions> options)
    {
        _options = options.Value;
    }

    public PhotoValidationResult Validate(
        byte[] photoBytes,
        string? contentType,
        IReadOnlySet<string>? existingHashes = null)
    {
        var errors = new List<string>();

        if (photoBytes is not { Length: > 0 })
        {
            return Invalid(errors, "Image does not exist or is empty.");
        }

        if (photoBytes.LongLength > _options.MaximumByteSize)
        {
            errors.Add($"Image exceeds maximum size of {_options.MaximumByteSize} bytes.");
        }

        if (!string.IsNullOrWhiteSpace(contentType) && !SupportedFormats.Contains(contentType))
        {
            errors.Add($"Unsupported format: {contentType}.");
        }

        string? detectedFormat;
        int width;
        int height;
        try
        {
            using var image = Image.Load(photoBytes);
            width = image.Width;
            height = image.Height;
            detectedFormat = image.Metadata.DecodedImageFormat?.Name;
        }
        catch (Exception ex)
        {
            return Invalid(errors, $"Corrupt or invalid image: {ex.Message}");
        }

        if (width < _options.MinimumWidth || height < _options.MinimumHeight)
        {
            errors.Add($"Resolution {width}x{height} is below minimum {_options.MinimumWidth}x{_options.MinimumHeight}.");
        }

        var hash = ComputeHash(photoBytes);
        var isDuplicate = existingHashes?.Contains(hash) == true;
        if (isDuplicate)
        {
            errors.Add("Duplicate image detected.");
        }

        return new PhotoValidationResult
        {
            IsValid = errors.Count == 0,
            Errors = errors,
            Width = width,
            Height = height,
            DetectedFormat = detectedFormat,
            IsDuplicate = isDuplicate,
        };
    }

    internal static string ComputeHash(byte[] photoBytes)
    {
        var hash = SHA256.HashData(photoBytes);
        return Convert.ToHexString(hash);
    }

    private static PhotoValidationResult Invalid(List<string> errors, string message)
    {
        errors.Add(message);
        return new PhotoValidationResult
        {
            IsValid = false,
            Errors = errors,
        };
    }
}

public sealed class PhotoQualityAssessmentService : IPhotoQualityAssessmentService
{
    public PhotoQualityReport Assess(byte[] photoBytes)
    {
        using var image = Image.Load<Rgb24>(photoBytes);
        var lumaValues = ExtractLumaValues(image);
        var brightness = lumaValues.Count == 0 ? 0m : (decimal)(lumaValues.Average() / 255d);
        var contrast = ComputeNormalizedContrast(lumaValues);
        var blurScore = ComputeVarianceOfLaplacian(image);
        var rotation = ReadRotationDegrees(image);

        var overall = Math.Clamp(
            (blurScore * 0.4m) + (brightness * 0.2m) + (contrast * 0.2m) + 0.2m,
            0m,
            1m);

        return new PhotoQualityReport
        {
            BlurScore = blurScore,
            Brightness = brightness,
            Contrast = contrast,
            FaceVisibilityScore = 0.5m,
            RotationDegrees = rotation,
            OcclusionScore = 0m,
            OverallScore = overall,
        };
    }

    private static List<double> ExtractLumaValues(Image<Rgb24> image)
    {
        var values = new List<double>(image.Width * image.Height);
        image.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < accessor.Height; y++)
            {
                var row = accessor.GetRowSpan(y);
                foreach (ref var pixel in row)
                {
                    values.Add(0.299 * pixel.R + 0.587 * pixel.G + 0.114 * pixel.B);
                }
            }
        });

        return values;
    }

    private static decimal ComputeNormalizedContrast(IReadOnlyList<double> lumaValues)
    {
        if (lumaValues.Count == 0)
        {
            return 0m;
        }

        var mean = lumaValues.Average();
        var variance = lumaValues.Select(v => Math.Pow(v - mean, 2)).Average();
        return (decimal)Math.Clamp(Math.Sqrt(variance) / 128d, 0d, 1d);
    }

    private static decimal ComputeVarianceOfLaplacian(Image<Rgb24> image)
    {
        var width = image.Width;
        var height = image.Height;
        if (width < 3 || height < 3)
        {
            return 0m;
        }

        var laplacianValues = new List<double>((width - 2) * (height - 2));
        for (var y = 1; y < height - 1; y++)
        {
            for (var x = 1; x < width - 1; x++)
            {
                var center = GetLuma(image[x, y]);
                var laplacian = (-4 * center)
                                + GetLuma(image[x - 1, y])
                                + GetLuma(image[x + 1, y])
                                + GetLuma(image[x, y - 1])
                                + GetLuma(image[x, y + 1]);
                laplacianValues.Add(laplacian * laplacian);
            }
        }

        if (laplacianValues.Count == 0)
        {
            return 0m;
        }

        var mean = laplacianValues.Average();
        var variance = laplacianValues.Select(v => Math.Pow(v - mean, 2)).Average();
        return (decimal)Math.Clamp(variance / 5000d, 0d, 1d);
    }

    private static double GetLuma(Rgb24 pixel)
        => 0.299 * pixel.R + 0.587 * pixel.G + 0.114 * pixel.B;

    private static decimal ReadRotationDegrees(Image image)
    {
        if (image.Metadata.ExifProfile?.TryGetValue(ExifTag.Orientation, out var orientation) != true
            || orientation is null)
        {
            return 0m;
        }

        return orientation.Value switch
        {
            6 => 90m,
            3 => 180m,
            8 => 270m,
            _ => 0m,
        };
    }
}
