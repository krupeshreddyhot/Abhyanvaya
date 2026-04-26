using Abhyanvaya.API.Common;
using Abhyanvaya.Application.Common.Interfaces;
using Amazon;
using Amazon.S3;
using Amazon.Runtime;
using Amazon.S3.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Net.Http;
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
            if (IsStorageOrNetworkFailure(ex))
            {
                return (false, "Storage upload failed. Verify Branding S3 endpoint/region/bucket credentials on server.");
            }
            return (false, "Could not read or resize the image. Try another file.");
        }
    }

    public async Task<(bool Ok, string Provider, string Message)> CheckStorageHealthAsync(CancellationToken cancellationToken)
    {
        var provider = (BrandingSettingsResolver.Get(_configuration, "Branding:Provider") ?? ProviderLocal)
            .Trim()
            .ToLowerInvariant();

        if (provider != ProviderS3)
        {
            try
            {
                var root = ResolveBrandingDirectory();
                Directory.CreateDirectory(root);
                return (true, provider, $"Local branding directory is accessible: {root}");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Local branding directory health check failed.");
                return (false, provider, "Local branding directory is not accessible.");
            }
        }

        var bucket = GetRequiredS3Bucket();
        var (s3, endpoint, regionName, forcePathStyle) = BuildS3Client();
        using var _ = s3;

        var key = $"__healthcheck/{Guid.NewGuid():D}.txt";
        using var payload = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("ok"));

        try
        {
            await s3.PutObjectAsync(new PutObjectRequest
            {
                BucketName = bucket,
                Key = key,
                InputStream = payload,
                ContentType = "text/plain",
                DisablePayloadSigning = true,
                DisableDefaultChecksumValidation = true,
            }, cancellationToken);

            await s3.DeleteObjectAsync(new DeleteObjectRequest
            {
                BucketName = bucket,
                Key = key,
            }, cancellationToken);

            return (true, provider, "Storage upload and delete check succeeded.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Branding storage health check failed. Bucket={Bucket}, Endpoint={Endpoint}, Region={Region}, ForcePathStyle={ForcePathStyle}",
                bucket,
                endpoint,
                regionName,
                forcePathStyle);
            return (false, provider, "Storage health check failed. Verify S3/R2 endpoint, region, bucket, and credentials.");
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
        var bucket = GetRequiredS3Bucket();
        var (s3, endpoint, regionName, forcePathStyle) = BuildS3Client();
        using var _ = s3;

        await using var ms = new MemoryStream(content);
        var keyPath = $"{key:D}/{variant}.webp";
        try
        {
            // R2 does not support the streaming SigV4 payload signing / default checksum path used by AWSSDK.S3.
            // https://developers.cloudflare.com/r2/examples/aws/aws-sdk-net/
            await s3.PutObjectAsync(new PutObjectRequest
            {
                BucketName = bucket,
                Key = keyPath,
                InputStream = ms,
                ContentType = "image/webp",
                DisablePayloadSigning = true,
                DisableDefaultChecksumValidation = true,
                Headers =
                {
                    CacheControl = "public,max-age=86400",
                },
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "S3 upload failed for branding key {KeyPath}. Bucket={Bucket}, Endpoint={Endpoint}, Region={Region}, ForcePathStyle={ForcePathStyle}",
                keyPath,
                bucket,
                string.IsNullOrWhiteSpace(endpoint) ? "<aws-default>" : NormalizeServiceUrl(endpoint),
                string.IsNullOrWhiteSpace(regionName) ? "<none>" : regionName,
                forcePathStyle);
            throw;
        }
    }

    private static string NormalizeServiceUrl(string endpoint)
    {
        var trimmed = endpoint.Trim();
        if (trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return trimmed;
        return $"https://{trimmed}";
    }

    /// <summary>If endpoint was pasted as .../bucket-name, strip the trailing bucket segment for S3 API base URL.</summary>
    private static string StripOptionalBucketPath(string endpoint, string bucket)
    {
        var trimmed = endpoint.Trim().TrimEnd('/');
        if (string.IsNullOrWhiteSpace(bucket))
            return trimmed;
        var suffix = "/" + bucket.Trim().Trim('/');
        if (trimmed.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            return trimmed[..^suffix.Length];
        return trimmed;
    }

    private static bool IsStorageOrNetworkFailure(Exception ex)
    {
        for (var e = ex; e != null; e = e.InnerException)
        {
            if (e is AmazonS3Exception || e is HttpRequestException)
                return true;
            if (e is System.Net.Sockets.SocketException)
                return true;
        }
        return false;
    }

    private string GetRequiredS3Bucket()
    {
        var bucket = BrandingSettingsResolver.Get(_configuration, "Branding:S3:Bucket");
        if (string.IsNullOrWhiteSpace(bucket))
            throw new InvalidOperationException("Branding:S3:Bucket is required when Branding:Provider=s3.");
        return bucket;
    }

    private (IAmazonS3 Client, string Endpoint, string Region, bool ForcePathStyle) BuildS3Client()
    {
        var bucket = GetRequiredS3Bucket();
        var endpointRaw = BrandingSettingsResolver.Get(_configuration, "Branding:S3:Endpoint");
        var endpoint = string.IsNullOrWhiteSpace(endpointRaw)
            ? "<aws-default>"
            : NormalizeServiceUrl(StripOptionalBucketPath(endpointRaw, bucket));
        var regionName = BrandingSettingsResolver.Get(_configuration, "Branding:S3:Region");
        var accessKey = BrandingSettingsResolver.Get(_configuration, "Branding:S3:AccessKeyId");
        var secretKey = BrandingSettingsResolver.Get(_configuration, "Branding:S3:SecretAccessKey");
        var forcePathStyleValue = BrandingSettingsResolver.Get(_configuration, "Branding:S3:ForcePathStyle");
        var forcePathStyle = bool.TryParse(forcePathStyleValue, out var fps) && fps;

        var cfg = new AmazonS3Config
        {
            ForcePathStyle = forcePathStyle,
        };

        if (!string.IsNullOrWhiteSpace(endpointRaw))
        {
            cfg.ServiceURL = endpoint;
        }
        else if (!string.IsNullOrWhiteSpace(regionName)
                 && !string.Equals(regionName.Trim(), "auto", StringComparison.OrdinalIgnoreCase))
        {
            cfg.RegionEndpoint = RegionEndpoint.GetBySystemName(regionName.Trim());
        }

        IAmazonS3 client = string.IsNullOrWhiteSpace(accessKey) || string.IsNullOrWhiteSpace(secretKey)
            ? new AmazonS3Client(cfg)
            : new AmazonS3Client(new BasicAWSCredentials(accessKey, secretKey), cfg);

        return (client, endpoint, string.IsNullOrWhiteSpace(regionName) ? "<none>" : regionName, forcePathStyle);
    }
}
