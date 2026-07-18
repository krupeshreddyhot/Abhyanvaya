using Abhyanvaya.Domain.Entities;
using Abhyanvaya.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Abhyanvaya.Infrastructure.Persistence.Configurations;

public sealed class ArtifactRegistryEntryConfiguration : IEntityTypeConfiguration<ArtifactRegistryEntry>
{
    public void Configure(EntityTypeBuilder<ArtifactRegistryEntry> builder)
    {
        builder.ToTable("ArtifactRegistryEntry");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.ArtifactType).HasMaxLength(64).IsRequired();
        builder.Property(x => x.StorageProvider).HasMaxLength(32).IsRequired();
        builder.Property(x => x.Bucket).HasMaxLength(256).IsRequired();
        builder.Property(x => x.StorageKey).HasMaxLength(1024).IsRequired();
        builder.Property(x => x.Checksum).HasMaxLength(128).IsRequired();
        builder.Property(x => x.Status).HasConversion<int>();
        builder.Property(x => x.VerificationResultJson).HasColumnType("jsonb");
        builder.Property(x => x.FailureReason).HasMaxLength(2048);
        builder.Property(x => x.CreatedUtc).HasColumnType("timestamp with time zone");
        builder.Property(x => x.VerifiedUtc).HasColumnType("timestamp with time zone");
        builder.Property(x => x.ArchivedUtc).HasColumnType("timestamp with time zone");
        builder.HasIndex(x => new { x.BatchId, x.EnrollmentId, x.ArtifactType }).IsUnique();
        builder.HasIndex(x => x.Checksum);
    }
}

public sealed class ArtifactStorageManifestConfiguration : IEntityTypeConfiguration<ArtifactStorageManifest>
{
    public void Configure(EntityTypeBuilder<ArtifactStorageManifest> builder)
    {
        builder.ToTable("ArtifactStorageManifest");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.ManifestJson).HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.Status).HasConversion<int>();
        builder.Property(x => x.CreatedUtc).HasColumnType("timestamp with time zone");
        builder.Property(x => x.VerifiedUtc).HasColumnType("timestamp with time zone");
        builder.HasIndex(x => x.BatchId);
    }
}
