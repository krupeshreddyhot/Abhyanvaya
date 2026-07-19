using Abhyanvaya.Application.Common.Interfaces;
using Abhyanvaya.Application.Enrollment.Storage;
using Abhyanvaya.Domain.Entities;

namespace Abhyanvaya.Infrastructure.Enrollment.Storage;

internal static class EnrollmentStorageMappers
{
    internal static EnrollmentStoredArtifactEntry MapStoredEntry(EnrollmentStorageRecord record, bool isDuplicate) =>
        new()
        {
            ArtifactId = record.Id,
            ArtifactType = record.ArtifactType,
            ObjectKey = record.ObjectKey,
            Checksum = record.Checksum,
            ArtifactVersion = record.ArtifactVersion,
            FileSize = record.FileSize,
            ContentType = record.ContentType,
            ImageWidth = record.ImageWidth,
            ImageHeight = record.ImageHeight,
            Persisted = true,
            IsDuplicate = isDuplicate,
        };

    internal static EnrollmentStorageManifestEntry MapManifestEntry(
        EnrollmentStorageRecord record,
        string? validationProfile) =>
        new()
        {
            ArtifactId = record.Id,
            ArtifactType = record.ArtifactType,
            StorageProvider = record.StorageProvider,
            ObjectKey = record.ObjectKey,
            Checksum = record.Checksum,
            Version = record.ArtifactVersion,
            CreatedUtc = record.CreatedUtc,
            PipelineVersion = record.PipelineVersion,
            ValidationProfile = validationProfile,
            ContentType = record.ContentType,
            ImageMetadata = new EnrollmentStorageImageMetadata
            {
                Width = record.ImageWidth,
                Height = record.ImageHeight,
                FileSize = record.FileSize,
            },
        };

    internal static EnrollmentStorageManifest BuildManifest(
        EnrollmentStoragePipelineContext context,
        int maxArtifactVersion) =>
        new()
        {
            ManifestId = context.ManifestId,
            StorageGroupId = context.StorageGroupId,
            Entries = context.ManifestEntries,
            CreatedUtc = context.CreatedUtc,
            ManifestVersion = EnrollmentStorageVersions.CurrentManifestVersion,
            SchemaVersion = EnrollmentStorageVersions.ManifestSchemaVersion,
            PipelineVersion = context.Request.PipelineVersion,
            ValidationVersion = EnrollmentStorageVersions.ValidationSchemaVersion,
            StorageVersion = EnrollmentStorageVersions.StorageSchemaVersion,
            ArtifactVersion = maxArtifactVersion,
            ValidationProfile = context.Request.ValidationProfile?.ToString(),
            CorrelationId = context.Request.Artifact.CorrelationId,
        };
}
