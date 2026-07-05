using Abhyanvaya.API.Media;
using Abhyanvaya.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Abhyanvaya.API.Services;

/// <summary>
/// Saves tenant college logos as WebP at three max-edge sizes for responsive UI.
/// </summary>
public class CollegeBrandingService
{
    private const long MaxBytes = 5 * 1024 * 1024;

    private static readonly IReadOnlyDictionary<string, int> LogoVariantMaxEdges =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["sm"] = 64,
            ["md"] = 128,
            ["lg"] = 256,
        };

    private readonly IApplicationDbContext _context;
    private readonly Abhyanvaya.API.Media.IMediaStorageService _imageStorage;
    private readonly LocalStorageProvider _localStorage;
    private readonly ILogger<CollegeBrandingService> _logger;

    public CollegeBrandingService(
        IApplicationDbContext context,
        Abhyanvaya.API.Media.IMediaStorageService imageStorage,
        LocalStorageProvider localStorage,
        ILogger<CollegeBrandingService> logger)
    {
        _context = context;
        _imageStorage = imageStorage;
        _localStorage = localStorage;
        _logger = logger;
    }

    /// <summary>Filesystem directory for local branding provider.</summary>
    public string ResolveBrandingDirectory() => _localStorage.ResolveRootDirectory();

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
        var validation = _imageStorage.ValidateRasterUpload(file, MaxBytes);
        if (!validation.Ok)
            return validation;

        var college = await _context.Colleges
            .FirstOrDefaultAsync(c => c.TenantId == tenantId, cancellationToken);

        if (college is null)
            return (false, "College profile not found for this tenant.");

        var key = college.LogoAccessKey ?? Guid.NewGuid();

        try
        {
            await using var input = file.OpenReadStream();
            var variants = await _imageStorage.BuildWebpVariantsAsync(input, LogoVariantMaxEdges, cancellationToken);
            await _imageStorage.SaveVariantsAsync($"{key:D}", variants, cancellationToken);

            college.LogoAccessKey = key;
            college.LogoUpdatedUtc = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
            return (true, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process college logo for tenant {TenantId}", tenantId);
            if (_imageStorage.IsStorageOrNetworkFailure(ex))
            {
                return (false, "Storage upload failed. Verify Branding S3 endpoint/region/bucket credentials on server.");
            }

            return (false, "Could not read or resize the image. Try another file.");
        }
    }

    public Task<(bool Ok, string Provider, string Message)> CheckStorageHealthAsync(CancellationToken cancellationToken) =>
        _imageStorage.CheckStorageHealthAsync(cancellationToken);
}
