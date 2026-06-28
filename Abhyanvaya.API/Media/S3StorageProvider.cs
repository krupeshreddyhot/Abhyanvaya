using System.Net;
using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;

namespace Abhyanvaya.API.Media;

public sealed class S3StorageProvider : IStorageProvider
{
    public const string ProviderName = "s3";

    private readonly MediaOptions _mediaOptions;
    private readonly ILogger<S3StorageProvider> _logger;

    public S3StorageProvider(IOptions<MediaOptions> mediaOptions, ILogger<S3StorageProvider> logger)
    {
        _mediaOptions = mediaOptions.Value;
        _logger = logger;
    }

    public string Name => ProviderName;

    public async Task WriteObjectAsync(
        string relativeKey,
        ReadOnlyMemory<byte> content,
        StorageWriteOptions options,
        CancellationToken cancellationToken)
    {
        var bucket = GetRequiredBucket();
        var (s3, endpoint, regionName, forcePathStyle) = BuildS3Client();
        using var _ = s3;

        var keyPath = NormalizeKey(relativeKey);
        await using var ms = new MemoryStream(content.ToArray());

        try
        {
            // R2 does not support the streaming SigV4 payload signing / default checksum path used by AWSSDK.S3.
            // https://developers.cloudflare.com/r2/examples/aws/aws-sdk-net/
            var request = new PutObjectRequest
            {
                BucketName = bucket,
                Key = keyPath,
                InputStream = ms,
                ContentType = options.ContentType,
                DisablePayloadSigning = true,
                DisableDefaultChecksumValidation = true,
            };

            if (!string.IsNullOrWhiteSpace(options.CacheControl))
                request.Headers.CacheControl = options.CacheControl;

            await s3.PutObjectAsync(request, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "S3 upload failed for key {KeyPath}. Bucket={Bucket}, Endpoint={Endpoint}, Region={Region}, ForcePathStyle={ForcePathStyle}",
                keyPath,
                bucket,
                string.IsNullOrWhiteSpace(endpoint) ? "<aws-default>" : NormalizeServiceUrl(endpoint),
                string.IsNullOrWhiteSpace(regionName) ? "<none>" : regionName,
                forcePathStyle);
            throw;
        }
    }

    public async Task<Stream> ReadObjectAsync(string relativeKey, CancellationToken cancellationToken)
    {
        var bucket = GetRequiredBucket();
        var (s3, _, _, _) = BuildS3Client();
        using var _ = s3;

        var keyPath = NormalizeKey(relativeKey);
        try
        {
            using var response = await s3.GetObjectAsync(new GetObjectRequest
            {
                BucketName = bucket,
                Key = keyPath,
            }, cancellationToken).ConfigureAwait(false);

            var buffer = new MemoryStream();
            await response.ResponseStream.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
            buffer.Position = 0;
            return buffer;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            throw new FileNotFoundException($"Object not found: {keyPath}", keyPath, ex);
        }
    }

    public async Task<bool> ExistsAsync(string relativeKey, CancellationToken cancellationToken)
    {
        var bucket = GetRequiredBucket();
        var (s3, _, _, _) = BuildS3Client();
        using var _ = s3;

        var keyPath = NormalizeKey(relativeKey);
        try
        {
            await s3.GetObjectMetadataAsync(new GetObjectMetadataRequest
            {
                BucketName = bucket,
                Key = keyPath,
            }, cancellationToken).ConfigureAwait(false);

            return true;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    public async Task DeleteObjectAsync(string relativeKey, CancellationToken cancellationToken)
    {
        var bucket = GetRequiredBucket();
        var (s3, _, _, _) = BuildS3Client();
        using var _ = s3;

        var keyPath = NormalizeKey(relativeKey);
        await s3.DeleteObjectAsync(new DeleteObjectRequest
        {
            BucketName = bucket,
            Key = keyPath,
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task<StorageHealthResult> CheckHealthAsync(CancellationToken cancellationToken)
    {
        var bucket = GetRequiredBucket();
        var (s3, endpoint, regionName, forcePathStyle) = BuildS3Client();
        using var _ = s3;

        var key = $"__healthcheck/{Guid.NewGuid():D}.txt";
        await using var payload = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("ok"));

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

            return new StorageHealthResult(true, "Storage upload and delete check succeeded.");
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
            return new StorageHealthResult(false, "Storage health check failed. Verify S3/R2 endpoint, region, bucket, and credentials.");
        }
    }

    private string GetRequiredBucket()
    {
        var bucket = _mediaOptions.S3.Bucket;
        if (string.IsNullOrWhiteSpace(bucket))
            throw new InvalidOperationException("Branding:S3:Bucket is required when Branding:Provider=s3.");
        return bucket;
    }

    private (IAmazonS3 Client, string Endpoint, string Region, bool ForcePathStyle) BuildS3Client()
    {
        var bucket = GetRequiredBucket();
        var s3Options = _mediaOptions.S3;
        var endpointRaw = s3Options.Endpoint;
        var endpoint = string.IsNullOrWhiteSpace(endpointRaw)
            ? "<aws-default>"
            : NormalizeServiceUrl(StripOptionalBucketPath(endpointRaw, bucket));
        var regionName = s3Options.Region;
        var accessKey = s3Options.AccessKeyId;
        var secretKey = s3Options.SecretAccessKey;
        var forcePathStyle = s3Options.ForcePathStyle;

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

    private static string NormalizeKey(string relativeKey) =>
        StorageKeyHelper.NormalizeRelativeKey(relativeKey);

    private static string NormalizeServiceUrl(string endpoint)
    {
        var trimmed = endpoint.Trim();
        if (trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return trimmed;
        return $"https://{trimmed}";
    }

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
}
