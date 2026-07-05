using Abhyanvaya.API.Common;
using Microsoft.Extensions.Options;

namespace Abhyanvaya.API.Media;

/// <summary>Binds <see cref="MediaOptions"/> from <c>Media:*</c>, falling back to legacy <c>Branding:*</c>.</summary>
public sealed class ConfigureMediaOptions : IConfigureOptions<MediaOptions>
{
    private readonly IConfiguration _configuration;

    public ConfigureMediaOptions(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public void Configure(MediaOptions options)
    {
        options.Provider = Get("Provider") ?? LocalStorageProvider.Id;
        options.PhysicalRoot = Get("PhysicalRoot");
        options.PublicBaseUrl = Get("PublicBaseUrl");

        options.S3.Bucket = Get("S3:Bucket");
        options.S3.Region = Get("S3:Region");
        options.S3.Endpoint = Get("S3:Endpoint");
        options.S3.AccessKeyId = Get("S3:AccessKeyId");
        options.S3.SecretAccessKey = Get("S3:SecretAccessKey");
        options.S3.ForcePathStyle = bool.TryParse(Get("S3:ForcePathStyle"), out var forcePathStyle) && forcePathStyle;
    }

    private string? Get(string propertyPath) =>
        MediaConfigurationResolver.Get(_configuration, propertyPath);
}

internal static class MediaConfigurationResolver
{
    /// <summary>Prefers <c>Media:{propertyPath}</c>, then <c>Branding:{propertyPath}</c> (including env var fallbacks).</summary>
    public static string? Get(IConfiguration configuration, string propertyPath) =>
        BrandingSettingsResolver.Get(configuration, $"{MediaOptions.SectionName}:{propertyPath}")
        ?? BrandingSettingsResolver.Get(configuration, $"Branding:{propertyPath}");
}
