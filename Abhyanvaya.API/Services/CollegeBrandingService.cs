using Abhyanvaya.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace Abhyanvaya.API.Services;

/// <summary>
/// Saves tenant college logos as WebP at three max-edge sizes for responsive UI.
/// </summary>
public class CollegeBrandingService
{
    private const long MaxBytes = 5 * 1024 * 1024;
    private static readonly HashSet<string> AllowedTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/png",
        "image/gif",
        "image/webp",
    };

    private readonly IApplicationDbContext _context;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<CollegeBrandingService> _logger;

    public CollegeBrandingService(
        IApplicationDbContext context,
        IWebHostEnvironment env,
        ILogger<CollegeBrandingService> logger)
    {
        _context = context;
        _env = env;
        _logger = logger;
    }

    public static string? BuildLogoPath(Guid? accessKey, DateTime? updatedUtc, string variant)
    {
        if (accessKey is null || updatedUtc is null)
            return null;
        var v = new DateTimeOffset(DateTime.SpecifyKind(updatedUtc.Value, DateTimeKind.Utc)).ToUnixTimeSeconds();
        return $"/branding/{accessKey:D}/{variant}.webp?v={v}";
    }

    public async Task<(bool Ok, string? Error)> SaveLogoForTenantAsync(int tenantId, IFormFile file, CancellationToken cancellationToken)
    {
        if (file.Length == 0 || file.Length > MaxBytes)
            return (false, "Upload a non-empty image under 5 MB.");

        var contentType = file.ContentType ?? "";
        if (!AllowedTypes.Contains(contentType))
            return (false, "Allowed types: JPEG, PNG, GIF, or WebP.");

        var college = await _context.Colleges
            .FirstOrDefaultAsync(c => c.TenantId == tenantId, cancellationToken);

        if (college is null)
            return (false, "College profile not found for this tenant.");

        var webRoot = _env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot");
        Directory.CreateDirectory(webRoot);

        var key = college.LogoAccessKey ?? Guid.NewGuid();
        var dir = Path.Combine(webRoot, "branding", key.ToString("D"));
        Directory.CreateDirectory(dir);

        try
        {
            await using var input = file.OpenReadStream();
            using var image = await Image.LoadAsync(input, cancellationToken);

            await SaveVariantAsync(image, Path.Combine(dir, "sm.webp"), 64, cancellationToken);
            await SaveVariantAsync(image, Path.Combine(dir, "md.webp"), 128, cancellationToken);
            await SaveVariantAsync(image, Path.Combine(dir, "lg.webp"), 256, cancellationToken);

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

    private static async Task SaveVariantAsync(Image source, string path, int maxEdge, CancellationToken cancellationToken)
    {
        using var clone = source.Clone(ctx =>
        {
            ctx.Resize(new ResizeOptions
            {
                Mode = ResizeMode.Max,
                Size = new Size(maxEdge, maxEdge),
            });
        });

        await clone.SaveAsWebpAsync(path, cancellationToken);
    }
}
