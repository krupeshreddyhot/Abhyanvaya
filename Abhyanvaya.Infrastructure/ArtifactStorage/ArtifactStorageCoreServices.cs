using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using Abhyanvaya.Application.ArtifactStorage;
using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Domain.Enums;
using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Abhyanvaya.Infrastructure.ArtifactStorage;

public sealed class ArtifactIntegrityService : IArtifactIntegrityService
{
    public string ComputeSha256(byte[] content) =>
        Convert.ToHexString(SHA256.HashData(content));

    public string ComputeSha256(Stream content)
    {
        using var sha = SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(content));
    }

    public bool ValidateChecksum(string expected, string actual) =>
        string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase);
}

public sealed class ArtifactVersionManager : IArtifactVersionManager
{
    private readonly ArtifactStorageOptions _options;

    public ArtifactVersionManager(IOptions<ArtifactStorageOptions> options)
    {
        _options = options.Value;
    }

    public int AssignArtifactVersion(string artifactType, int studentId) =>
        HashCode.Combine(artifactType, studentId, DateTime.UtcNow.Date);

    public string ResolveEmbeddingVersion(string configuredVersion) => configuredVersion;
    public string ResolveRecognitionVersion(string configuredVersion) => _options.RecognitionVersion;
    public string ResolveEnrollmentVersion(string configuredVersion) => _options.EnrollmentVersion;
    public int ResolveManifestVersion(string configuredVersion) => configuredVersion.GetHashCode(StringComparison.Ordinal);
    public int ResolveRetentionVersion(int configuredVersion) => _options.RetentionVersion;
}

public sealed class ArtifactUploadService : IArtifactUploadService
{
    private readonly IArtifactStorageProvider _storageProvider;
    private readonly IArtifactIntegrityService _integrityService;
    private readonly IArtifactRetryPolicy _retryPolicy;
    private readonly IArtifactRegistryRepository _registryRepository;
    private readonly ArtifactStorageOptions _options;
    private readonly ILogger<ArtifactUploadService> _logger;

    public ArtifactUploadService(
        IArtifactStorageProvider storageProvider,
        IArtifactIntegrityService integrityService,
        IArtifactRetryPolicy retryPolicy,
        IArtifactRegistryRepository registryRepository,
        IOptions<ArtifactStorageOptions> options,
        ILogger<ArtifactUploadService> logger)
    {
        _storageProvider = storageProvider;
        _integrityService = integrityService;
        _retryPolicy = retryPolicy;
        _registryRepository = registryRepository;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<ArtifactUploadResult> UploadItemAsync(
        ArtifactStorageContext context,
        ArtifactUploadItem item,
        CancellationToken cancellationToken = default)
    {
        var content = _options.EnableCompression && item.Content.Length > 1024
            ? Compress(item.Content)
            : item.Content;

        var metadata = new ArtifactMetadata
        {
            ArtifactType = item.ArtifactType,
            ContentType = item.ContentType,
            FileSize = content.LongLength,
            Checksum = item.Checksum,
            Compression = _options.EnableCompression && content.Length != item.Content.Length,
            Version = item.Version,
            CreatedUtc = DateTime.UtcNow,
            RetentionPolicy = _options.StorageClass,
            StorageClass = _options.StorageClass,
        };

        var attempt = 0;
        while (true)
        {
            attempt++;
            try
            {
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                linkedCts.CancelAfter(_retryPolicy.UploadTimeout);

                await using var stream = new MemoryStream(content);
                await _storageProvider.UploadAsync(context.StorageKey, stream, metadata, linkedCts.Token);

                return new ArtifactUploadResult
                {
                    ArtifactId = context.ArtifactId,
                    StorageKey = context.StorageKey,
                    Checksum = item.Checksum,
                    FileSize = content.LongLength,
                    Verified = false,
                    FinalState = ArtifactUploadState.Uploaded,
                };
            }
            catch (Exception ex) when (_retryPolicy.ShouldRetry(ex, attempt))
            {
                _logger.LogWarning(
                    ex,
                    "Artifact upload retry artifactId={ArtifactId} attempt={Attempt}",
                    context.ArtifactId,
                    attempt);
                await Task.Delay(_retryPolicy.GetDelay(attempt), cancellationToken);
            }
            catch (Exception ex)
            {
                return new ArtifactUploadResult
                {
                    ArtifactId = context.ArtifactId,
                    StorageKey = context.StorageKey,
                    Checksum = item.Checksum,
                    FileSize = content.LongLength,
                    Verified = false,
                    FinalState = ArtifactUploadState.Failed,
                    FailureReason = ex.Message,
                };
            }
        }
    }

    private static byte[] Compress(byte[] content)
    {
        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionLevel.Fastest, leaveOpen: true))
        {
            gzip.Write(content, 0, content.Length);
        }

        return output.ToArray();
    }
}

public sealed class ArtifactVerificationService : IArtifactVerificationService
{
    private readonly IArtifactStorageProvider _storageProvider;
    private readonly IArtifactIntegrityService _integrityService;
    private readonly IArtifactVerificationPolicy _policy;

    public ArtifactVerificationService(
        IArtifactStorageProvider storageProvider,
        IArtifactIntegrityService integrityService,
        IArtifactVerificationPolicy policy)
    {
        _storageProvider = storageProvider;
        _integrityService = integrityService;
        _policy = policy;
    }

    public async Task<ArtifactVerificationResult> VerifyAsync(
        ArtifactStorageContext context,
        ArtifactMetadata metadata,
        byte[] sourceContent,
        CancellationToken cancellationToken = default)
    {
        var errors = new List<string>();

        if (_policy.ChecksumValidation &&
            !_integrityService.ValidateChecksum(context.Checksum, metadata.Checksum))
        {
            errors.Add("Checksum mismatch.");
        }

        if (_policy.ContentLengthValidation && metadata.FileSize != sourceContent.LongLength)
        {
            errors.Add("Content length mismatch.");
        }

        if (_policy.MetadataValidation && string.IsNullOrWhiteSpace(metadata.ArtifactType))
        {
            errors.Add("Metadata artifact type missing.");
        }

        if (_policy.VersionValidation && string.IsNullOrWhiteSpace(metadata.Version))
        {
            errors.Add("Version missing.");
        }

        if (_policy.ChecksumValidation)
        {
            var computed = _integrityService.ComputeSha256(sourceContent);
            if (!_integrityService.ValidateChecksum(metadata.Checksum, computed))
            {
                errors.Add("Source checksum mismatch.");
            }
        }

        var exists = await _storageProvider.VerifyExistsAsync(context.StorageKey, metadata.FileSize, cancellationToken);
        if (!exists)
        {
            errors.Add("Storage object verification failed.");
        }

        return new ArtifactVerificationResult
        {
            Passed = errors.Count == 0,
            Errors = errors.Count == 0 ? null : errors,
        };
    }
}
