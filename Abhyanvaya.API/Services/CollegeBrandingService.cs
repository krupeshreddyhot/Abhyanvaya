using Abhyanvaya.API.Common;
using Abhyanvaya.Application.Common.Interfaces;
using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace Abhyanvaya.API.Services;

/// <summary>
/// Saves tenant college logos as WebP at three max-edge sizes for responsive UI.
/// </summary>
public class CollegeBrandingService
{
    private const string ProviderLocal = "local";
    private const string ProviderS3 = "s3";
    private const long MaxBytes = 5 * 1024 * 1024;
    private static readonly HashSet<string> AllowedTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/jpg",
        "image/pjpeg",
        "image/png",
        "image/gif",
        "image/webp",
    };
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg",
        ".jpeg",
        ".png",
        ".gif",
        ".webp",
    };

    private readonly IApplicationDbContext _context;
    private readonly IWebHostEnvironment _env;
    private readonly IConfiguration _configuration;
    private readonly ILogger<CollegeBrandingService> _logger;

    public CollegeBrandingService(
        IApplicationDbContext context,
        IWebHostEnvironment env,
        IConfiguration configuration,
        ILogger<CollegeBrandingService> logger)
    {
        _context = context;
        _env = env;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>Filesystem directory for local branding provider.</summary>
    public string ResolveBrandingDirectory()
    {
        var configured = _configuration["Branding:PhysicalRoot"]?.Trim();
        if (!string.IsNullOrEmpty(configured))
            return configured;
        var webRoot = _env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot");
        return Path.Combine(webRoot, "branding");
    }

    public static string? BuildLogoPath(Guid? accessKey, DateTime? updatedUtc, string variant, string? publicBaseUrl = null)
    {
        if (accessKey is null || updatedUtc is null)
            return null;
        var v = new DateTimeOffset(DateTime.SpecifyKind(updatedUtc.Value, DateTimeKind.Utc)).ToUnixTimeSeconds();
        if (string.IsNullOrWhiteSpace(publicBaseUrl))
            return $"/branding/{accessKey:D}/{variant}.webp?v={v}";
        var trimmed = publicBaseUrl.Trim().TrimEnd('/');
        return $"{trimmed}/{accessKey:D}/{variant}.webp?v={v}";
    }

    public async Task<(bool Ok, string? Error)> SaveLogoForTenantAsync(int tenantId, IFormFile file, CancellationToken cancellationToken)
    {
        if (file.Length == 0 || file.Length > MaxBytes)
            return (false, "Upload a non-empty image under 5 MB.");

        var contentType = file.ContentType ?? "";
        var extension = Path.GetExtension(file.FileName ?? string.Empty);
        var contentTypeAllowed = AllowedTypes.Contains(contentType);
        var extensionAllowed = !string.IsNullOrWhiteSpace(extension) && AllowedExtensions.Contains(extension);
        if (!contentTypeAllowed && !extensionAllowed)
            return (false, "Allowed types: JPEG, PNG, GIF, or WebP.");

        var college = await _context.Colleges
            .FirstOrDefaultAsync(c => c.TenantId == tenantId, cancellationToken);

        if (college is null)
            return (false, "College profile not found for this tenant.");

        var key = college.LogoAccessKey ?? Guid.NewGuid();

        try
        {
            await using var input = file.OpenReadStream();
            using var image = await Image.LoadAsync(input, cancellationToken);

            var smBytes = await BuildVariantBytesAsync(image, 64, cancellationToken);
            var mdBytes = await BuildVariantBytesAsync(image, 128, cancellationToken);
            var lgBytes = await BuildVariantBytesAsync(image, 256, cancellationToken);

            var provider = (BrandingSettingsResolver.Get(_configuration, "Branding:Provider") ?? ProviderLocal)
                .Trim()
                .ToLowerInvariant();
            if (provider == ProviderS3)
            {
                await UploadS3Async(key, "sm", smBytes, cancellationToken);
                await UploadS3Async(key, "md", mdBytes, cancellationToken);
                await UploadS3Async(key, "lg", lgBytes, cancellationToken);
            }
            else
            {
                var brandingRoot = ResolveBrandingDirectory();
                Directory.CreateDirectory(brandingRoot);
                var dir = Path.Combine(brandingRoot, key.ToString("D"));
                Directory.CreateDirectory(dir);
                await File.WriteAllBytesAsync(Path.Combine(dir, "sm.webp"), smBytes, cancellationToken);
                await File.WriteAllBytesAsync(Path.Combine(dir, "md.webp"), mdBytes, cancellationToken);
                await File.WriteAllBytesAsync(Path.Combine(dir, "lg.webp"), lgBytes, cancellationToken);
            }

            college.LogoAccessKey = key;
            college.LogoUpdatedUtc = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
            return (true, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process college logo for tenant {TenantId}", tenantId);
            return (false, "Could not read or resize the image. Try another file.");
        }
    }

    private static async Task<byte[]> BuildVariantBytesAsync(Image source, int maxEdge, CancellationToken cancellationToken)
    {
        using var clone = source.Clone(ctx =>
        {
            ctx.Resize(new ResizeOptions
            {
                Mode = ResizeMode.Max,
                Size = new Size(maxEdge, maxEdge),
            });
        });

        await using var ms = new MemoryStream();
        await clone.SaveAsWebpAsync(ms, cancellationToken);
        return ms.ToArray();
    }

    private async Task UploadS3Async(Guid key, string variant, byte[] content, CancellationToken cancellationToken)
    {
        var bucket = BrandingSettingsResolver.Get(_configuration, "Branding:S3:Bucket");
        if (string.IsNullOrWhiteSpace(bucket))
            throw new InvalidOperationException("Branding:S3:Bucket is required when Branding:Provider=s3.");

        var endpoint = BrandingSettingsResolver.Get(_configuration, "Branding:S3:Endpoint");
        var regionName = BrandingSettingsResolver.Get(_configuration, "Branding:S3:Region");
        var accessKey = BrandingSettingsResolver.Get(_configuration, "Branding:S3:AccessKeyId");
        var secretKey = BrandingSettingsResolver.Get(_configuration, "Branding:S3:SecretAccessKey");
        var forcePathStyleValue = BrandingSettingsResolver.Get(_configuration, "Branding:S3:ForcePathStyle");
        var forcePathStyle = bool.TryParse(forcePathStyleValue, out var fps) && fps;

        var cfg = new AmazonS3Config
        {
            ForcePathStyle = forcePathStyle,
        };
        if (!string.IsNullOrWhiteSpace(endpoint))
            cfg.ServiceURL = endpoint;
        if (!string.IsNullOrWhiteSpace(regionName))
            cfg.RegionEndpoint = RegionEndpoint.GetBySystemName(regionName);

        using var s3 = string.IsNullOrWhiteSpace(accessKey) || string.IsNullOrWhiteSpace(secretKey)
            ? new AmazonS3Client(cfg)
            : new AmazonS3Client(new BasicAWSCredentials(accessKey, secretKey), cfg);

        await using var ms = new MemoryStream(content);
        var keyPath = $"{key:D}/{variant}.webp";
        await s3.PutObjectAsync(new PutObjectRequest
        {
            BucketName = bucket,
            Key = keyPath,
            InputStream = ms,
            ContentType = "image/webp",
            Headers =
            {
                CacheControl = "public,max-age=86400",
            },
        }, cancellationToken);
    }
}
