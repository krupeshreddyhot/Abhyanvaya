using Abhyanvaya.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Abhyanvaya.Infrastructure.Persistence.Configurations;

internal sealed class EnrollmentStorageRecordConfiguration : IEntityTypeConfiguration<EnrollmentStorageRecord>
{
    public void Configure(EntityTypeBuilder<EnrollmentStorageRecord> builder)
    {
        builder.ToTable("EnrollmentStorageRecord");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.ArtifactType).HasMaxLength(64).IsRequired();
        builder.Property(x => x.ObjectKey).HasMaxLength(1000).IsRequired();
        builder.Property(x => x.StorageProvider).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Checksum).HasMaxLength(64).IsRequired();
        builder.Property(x => x.ContentType).HasMaxLength(128).IsRequired();
        builder.Property(x => x.ValidationProfile).HasMaxLength(64);

        builder.HasIndex(x => new { x.TenantId, x.StudentId, x.ArtifactType, x.Checksum })
            .HasDatabaseName("IX_EnrollmentStorageRecord_Tenant_Student_Type_Checksum");

        builder.HasIndex(x => new { x.StorageGroupId })
            .HasDatabaseName("IX_EnrollmentStorageRecord_StorageGroupId");

        builder.HasIndex(x => new { x.TenantId, x.StudentId, x.ArtifactType, x.ArtifactVersion })
            .IsUnique()
            .HasDatabaseName("UX_EnrollmentStorageRecord_Tenant_Student_Type_Version");
    }
}
