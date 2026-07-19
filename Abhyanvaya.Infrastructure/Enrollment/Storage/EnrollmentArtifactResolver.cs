using System.Diagnostics;
using System.Security.Cryptography;
using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.Enrollment.Storage;
using Microsoft.Extensions.Logging;

namespace Abhyanvaya.Infrastructure.Enrollment.Storage;

public sealed class EnrollmentArtifactResolver : IEnrollmentArtifactResolver
{
    private readonly IEnrollmentArtifactTypeRegistry _artifactTypeRegistry;
    private readonly IObjectStorageProvider _objectStorage;
    private readonly IChecksumService _checksumService;
    private readonly IEnrollmentArtifactCache _artifactCache;
    private readonly IStorageMetricsCollector _metrics;
    private readonly ILogger<EnrollmentArtifactResolver> _logger;

    public EnrollmentArtifactResolver(
        IEnrollmentArtifactTypeRegistry artifactTypeRegistry,
        IObjectStorageProvider objectStorage,
        IChecksumService checksumService,
        IEnrollmentArtifactCache artifactCache,
        IStorageMetricsCollector metrics,
        ILogger<EnrollmentArtifactResolver> logger)
    {
        _artifactTypeRegistry = artifactTypeRegistry;
        _objectStorage = objectStorage;
        _checksumService = checksumService;
        _artifactCache = artifactCache;
        _metrics = metrics;
        _logger = logger;
    }

    public async Task<EnrollmentArtifactResolveResult> ResolveAsync(
        EnrollmentArtifactResolveRequest request,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var manifest = request.Manifest;
        var correlationId = request.CorrelationId ?? manifest.CorrelationId;
        var pipelineVersion = request.PipelineVersion ?? manifest.PipelineVersion;

        if (!EnrollmentManifestCompatibility.IsCompatible(manifest))
        {
            stopwatch.Stop();
            var reason = EnrollmentManifestCompatibility.GetIncompatibilityReason(manifest)
                         ?? "Manifest version incompatible.";
            return EnrollmentArtifactResolveResult.Failed(
                EnrollmentArtifactResolveCodes.IncompatibleManifest,
                reason,
                stopwatch.Elapsed);
        }

        if (_artifactTypeRegistry.Get(request.ArtifactType) is null)
        {
            stopwatch.Stop();
            return EnrollmentArtifactResolveResult.Failed(
                EnrollmentArtifactResolveCodes.UnsupportedArtifact,
                $"Unsupported artifact type '{request.ArtifactType}'.",
                stopwatch.Elapsed);
        }

        var cached = await _artifactCache.LookupAsync(manifest.ManifestId, request.ArtifactType, cancellationToken);
        if (cached is not null)
        {
            stopwatch.Stop();
            _logger.LogInformation(
                "Artifact resolved from cache. ArtifactType={ArtifactType} CorrelationId={CorrelationId} PipelineVersion={PipelineVersion} DurationMs={DurationMs}",
                request.ArtifactType,
                correlationId,
                pipelineVersion,
                stopwatch.ElapsedMilliseconds);
            return EnrollmentArtifactResolveResult.Succeeded(cached, stopwatch.Elapsed);
        }

        var entry = manifest.Entries
            .Where(e => string.Equals(e.ArtifactType, request.ArtifactType, StringComparison.Ordinal))
            .OrderByDescending(e => e.Version)
            .FirstOrDefault();

        if (entry is null)
        {
            stopwatch.Stop();
            return EnrollmentArtifactResolveResult.Failed(
                EnrollmentArtifactResolveCodes.ArtifactMissing,
                $"Artifact '{request.ArtifactType}' not found in manifest.",
                stopwatch.Elapsed);
        }

        var downloadSw = Stopwatch.StartNew();
        var rawStream = await _objectStorage.ReadObjectAsync(entry.ObjectKey, cancellationToken);
        downloadSw.Stop();

        if (rawStream is null)
        {
            stopwatch.Stop();
            _metrics.RecordDownload(downloadSw.ElapsedMilliseconds, 0, _objectStorage.ProviderName, success: false);
            return EnrollmentArtifactResolveResult.Failed(
                EnrollmentArtifactResolveCodes.StreamUnavailable,
                $"Artifact stream unavailable for type '{request.ArtifactType}'.",
                stopwatch.Elapsed);
        }

        var fileSize = entry.ImageMetadata?.FileSize ?? 0;
        Stream contentStream;
        try
        {
            contentStream = await MaterializeVerifiedStreamAsync(rawStream, entry, cancellationToken)
                            ?? throw new InvalidOperationException("Checksum validation failed.");
        }
        catch (InvalidOperationException)
        {
            await rawStream.DisposeAsync();
            stopwatch.Stop();
            _metrics.RecordDownload(downloadSw.ElapsedMilliseconds, fileSize, _objectStorage.ProviderName, success: false);
            _metrics.RecordFailure("ResolveArtifact", EnrollmentArtifactResolveCodes.ChecksumMismatch);
            return EnrollmentArtifactResolveResult.Failed(
                EnrollmentArtifactResolveCodes.ChecksumMismatch,
                $"Checksum verification failed for artifact '{request.ArtifactType}'.",
                stopwatch.Elapsed);
        }

        if (contentStream is null)
        {
            await rawStream.DisposeAsync();
            stopwatch.Stop();
            _metrics.RecordDownload(downloadSw.ElapsedMilliseconds, fileSize, _objectStorage.ProviderName, success: false);
            _metrics.RecordFailure("ResolveArtifact", EnrollmentArtifactResolveCodes.ChecksumMismatch);
            return EnrollmentArtifactResolveResult.Failed(
                EnrollmentArtifactResolveCodes.ChecksumMismatch,
                $"Checksum verification failed for artifact '{request.ArtifactType}'.",
                stopwatch.Elapsed);
        }

        _metrics.RecordDownload(downloadSw.ElapsedMilliseconds, fileSize, _objectStorage.ProviderName, success: true);
        _metrics.RecordStorageSize(fileSize, request.ArtifactType);

        var artifact = new EnrollmentArtifact
        {
            ArtifactType = request.ArtifactType,
            Content = contentStream,
            ContentType = entry.ContentType,
            Checksum = entry.Checksum,
            ImageWidth = entry.ImageMetadata?.Width,
            ImageHeight = entry.ImageMetadata?.Height,
            Version = entry.Version,
            CreatedUtc = entry.CreatedUtc,
            Metadata = new EnrollmentArtifactMetadata
            {
                ArtifactId = entry.ArtifactId,
                ManifestId = manifest.ManifestId,
                ManifestVersion = manifest.ManifestVersion,
                PipelineVersion = manifest.PipelineVersion,
                StorageVersion = manifest.StorageVersion,
                ValidationVersion = manifest.ValidationVersion,
                ValidationProfile = entry.ValidationProfile,
                FileSize = entry.ImageMetadata?.FileSize,
            },
        };

        stopwatch.Stop();
        _logger.LogInformation(
            "Artifact resolved. ArtifactType={ArtifactType} CorrelationId={CorrelationId} PipelineVersion={PipelineVersion} DurationMs={DurationMs}",
            request.ArtifactType,
            correlationId,
            pipelineVersion,
            stopwatch.ElapsedMilliseconds);

        return EnrollmentArtifactResolveResult.Succeeded(artifact, stopwatch.Elapsed);
    }

    private async Task<Stream?> MaterializeVerifiedStreamAsync(
        Stream rawStream,
        EnrollmentStorageManifestEntry entry,
        CancellationToken cancellationToken)
    {
        var fileSize = entry.ImageMetadata?.FileSize;
        if (fileSize is > 10_485_760)
        {
            return new ChecksumValidatingStream(rawStream, entry.Checksum);
        }

        await using (rawStream)
        {
            using var buffer = new MemoryStream();
            await rawStream.CopyToAsync(buffer, cancellationToken);
            var bytes = buffer.ToArray();
            var actual = _checksumService.ComputeSha256Hex(bytes);
            if (!string.Equals(actual, entry.Checksum, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return new MemoryStream(bytes, writable: false);
        }
    }

    private sealed class ChecksumValidatingStream : Stream
    {
        private readonly Stream _inner;
        private readonly string _expectedChecksum;
        private readonly IncrementalHash _hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        private bool _validated;

        public ChecksumValidatingStream(Stream inner, string expectedChecksum)
        {
            _inner = inner;
            _expectedChecksum = expectedChecksum;
        }

        public override bool CanRead => _inner.CanRead;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => _inner.Length;
        public override long Position { get => _inner.Position; set => throw new NotSupportedException(); }

        public override void Flush() => _inner.Flush();

        public override int Read(byte[] buffer, int offset, int count)
        {
            var read = _inner.Read(buffer, offset, count);
            if (read > 0)
            {
                _hash.AppendData(buffer.AsSpan(offset, read));
            }

            if (read == 0)
            {
                ValidateIfComplete();
            }

            return read;
        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            var read = await _inner.ReadAsync(buffer, offset, count, cancellationToken);
            if (read > 0)
            {
                _hash.AppendData(buffer.AsSpan(offset, read));
            }

            if (read == 0)
            {
                ValidateIfComplete();
            }

            return read;
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ValidateIfComplete();
                _hash.Dispose();
                _inner.Dispose();
            }

            base.Dispose(disposing);
        }

        private void ValidateIfComplete()
        {
            if (_validated)
            {
                return;
            }

            _validated = true;
            var actual = Convert.ToHexString(_hash.GetHashAndReset()).ToLowerInvariant();
            if (!string.Equals(actual, _expectedChecksum, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Checksum validation failed.");
            }
        }
    }
}
