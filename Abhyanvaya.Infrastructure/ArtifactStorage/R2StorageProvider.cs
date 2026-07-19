using System.Net;
using Abhyanvaya.Application.ArtifactStorage;
using Abhyanvaya.Application.Common.Interfaces;
using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Abhyanvaya.Infrastructure.ArtifactStorage;

public sealed class R2StorageProvider : IR2StorageProvider
{
    public const string ProviderId = "r2";

    private readonly R2StorageOptions _r2Options;
    private readonly ArtifactStorageOptions _storageOptions;
    private readonly ILogger<R2StorageProvider> _logger;

    public R2StorageProvider(
        IOptions<R2StorageOptions> r2Options,
        IOptions<ArtifactStorageOptions> storageOptions,
        ILogger<R2StorageProvider> logger)
    {
        _r2Options = r2Options.Value;
        _storageOptions = storageOptions.Value;
        _logger = logger;
    }

    public string ProviderName => ProviderId;

    public string Bucket => string.IsNullOrWhiteSpace(_r2Options.Bucket) ? _storageOptions.Bucket : _r2Options.Bucket;

    public async Task UploadAsync(
        string storageKey,
        Stream content,
        ArtifactMetadata metadata,
        CancellationToken cancellationToken = default)
    {
        var bucket = GetRequiredBucket();
        var key = NormalizeKey(storageKey);
        var (client, _) = CreateClient();

        using (client)
        {
            if (metadata.FileSize >= _storageOptions.MultipartThresholdBytes)
            {
                await UploadMultipartAsync(client, bucket, key, content, metadata, cancellationToken);
                return;
            }

            var request = new PutObjectRequest
            {
                BucketName = bucket,
                Key = key,
                InputStream = content,
                ContentType = metadata.ContentType,
                DisablePayloadSigning = true,
                DisableDefaultChecksumValidation = true,
            };

            request.Metadata.Add("artifact-type", metadata.ArtifactType);
            request.Metadata.Add("checksum", metadata.Checksum);
            request.Metadata.Add("version", metadata.Version);

            await client.PutObjectAsync(request, cancellationToken);
        }
    }

    public async Task<bool> VerifyExistsAsync(string storageKey, long expectedLength, CancellationToken cancellationToken = default)
    {
        var bucket = GetRequiredBucket();
        var key = NormalizeKey(storageKey);
        var (client, _) = CreateClient();

        using (client)
        {
            try
            {
                var response = await client.GetObjectMetadataAsync(bucket, key, cancellationToken);
                return response.ContentLength == expectedLength;
            }
            catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                return false;
            }
        }
    }

    public async Task DeleteAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        var bucket = GetRequiredBucket();
        var key = NormalizeKey(storageKey);
        var (client, _) = CreateClient();

        using (client)
        {
            await client.DeleteObjectAsync(bucket, key, cancellationToken);
        }
    }

    public Task ArchiveAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Artifact archived storageKey={StorageKey}", storageKey);
        return Task.CompletedTask;
    }

    private async Task UploadMultipartAsync(
        IAmazonS3 client,
        string bucket,
        string key,
        Stream content,
        ArtifactMetadata metadata,
        CancellationToken cancellationToken)
    {
        var initiate = await client.InitiateMultipartUploadAsync(new InitiateMultipartUploadRequest
        {
            BucketName = bucket,
            Key = key,
            ContentType = metadata.ContentType,
        }, cancellationToken);

        var uploadId = initiate.UploadId;
        var partSize = Math.Max(5 * 1024 * 1024, _storageOptions.PartSizeBytes);
        var partNumber = 1;
        var etags = new List<PartETag>();
        var buffer = new byte[partSize];

        try
        {
            while (true)
            {
                var read = await content.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);
                if (read == 0)
                {
                    break;
                }

                await using var partStream = new MemoryStream(buffer, 0, read, writable: false);
                var uploadPart = await client.UploadPartAsync(new UploadPartRequest
                {
                    BucketName = bucket,
                    Key = key,
                    UploadId = uploadId,
                    PartNumber = partNumber,
                    InputStream = partStream,
                    DisablePayloadSigning = true,
                }, cancellationToken);

                etags.Add(new PartETag(partNumber, uploadPart.ETag));
                partNumber++;
            }

            await client.CompleteMultipartUploadAsync(new CompleteMultipartUploadRequest
            {
                BucketName = bucket,
                Key = key,
                UploadId = uploadId,
                PartETags = etags,
            }, cancellationToken);
        }
        catch
        {
            await client.AbortMultipartUploadAsync(new AbortMultipartUploadRequest
            {
                BucketName = bucket,
                Key = key,
                UploadId = uploadId,
            }, cancellationToken);
            throw;
        }
    }

    private (IAmazonS3 Client, string Endpoint) CreateClient()
    {
        var endpoint = _r2Options.Endpoint?.Trim() ?? string.Empty;
        var config = new AmazonS3Config
        {
            ForcePathStyle = _r2Options.ForcePathStyle,
            AuthenticationRegion = string.IsNullOrWhiteSpace(_r2Options.Region) ? "auto" : _r2Options.Region,
        };

        if (!string.IsNullOrWhiteSpace(endpoint))
        {
            config.ServiceURL = endpoint.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                ? endpoint
                : $"https://{endpoint}";
        }
        else if (!string.IsNullOrWhiteSpace(_r2Options.Region))
        {
            config.RegionEndpoint = RegionEndpoint.GetBySystemName(_r2Options.Region);
        }

        var credentials = new BasicAWSCredentials(_r2Options.AccessKeyId, _r2Options.SecretAccessKey);
        return (new AmazonS3Client(credentials, config), endpoint);
    }

    private string GetRequiredBucket()
    {
        if (string.IsNullOrWhiteSpace(Bucket))
        {
            throw new InvalidOperationException("Artifact storage bucket is not configured.");
        }

        return Bucket;
    }

    private static string NormalizeKey(string storageKey) =>
        storageKey.Replace('\\', '/').TrimStart('/');
}
