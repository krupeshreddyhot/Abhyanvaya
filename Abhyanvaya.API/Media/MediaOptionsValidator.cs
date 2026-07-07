using Microsoft.Extensions.Options;

namespace Abhyanvaya.API.Media;

/// <summary>Validates <see cref="MediaOptions"/> at application startup.</summary>
public sealed class MediaOptionsValidator : IValidateOptions<MediaOptions>
{
    public ValidateOptionsResult Validate(string? name, MediaOptions options)
    {
        if (options.GetActiveProviderName() != S3StorageProvider.Id)
            return ValidateOptionsResult.Success;

        if (string.IsNullOrWhiteSpace(options.S3.Bucket))
        {
            return ValidateOptionsResult.Fail(
                "Media:S3:Bucket (or Branding:S3:Bucket) is required when Media:Provider (or Branding:Provider) is 's3'.");
        }

        return ValidateOptionsResult.Success;
    }
}
