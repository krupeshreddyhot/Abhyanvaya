using Abhyanvaya.Domain.Entities;
using Abhyanvaya.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Abhyanvaya.Infrastructure.Persistence.Configurations;

public sealed class StudentPhotoAcquisitionBatchConfiguration : IEntityTypeConfiguration<StudentPhotoAcquisitionBatch>
{
    public void Configure(EntityTypeBuilder<StudentPhotoAcquisitionBatch> builder)
    {
        builder.ToTable("StudentPhotoAcquisitionBatch");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.ProviderName).HasMaxLength(128).IsRequired();
        builder.Property(x => x.Status).HasConversion<int>();
        builder.Property(x => x.ManifestJson).HasColumnType("jsonb");
        builder.Property(x => x.CreatedUtc).HasColumnType("timestamp with time zone");
        builder.Property(x => x.CompletedUtc).HasColumnType("timestamp with time zone");
        builder.HasMany(x => x.Items).WithOne(x => x.Batch).HasForeignKey(x => x.BatchId);
    }
}

public sealed class StudentPhotoAcquisitionItemConfiguration : IEntityTypeConfiguration<StudentPhotoAcquisitionItem>
{
    public void Configure(EntityTypeBuilder<StudentPhotoAcquisitionItem> builder)
    {
        builder.ToTable("StudentPhotoAcquisitionItem");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.StudentNumber).HasMaxLength(64).IsRequired();
        builder.Property(x => x.CollegeCode).HasMaxLength(32).IsRequired();
        builder.Property(x => x.Status).HasConversion<int>().HasDefaultValue(PhotoAcquisitionItemStatus.Pending);
        builder.Property(x => x.SourceReference).HasMaxLength(1024);
        builder.Property(x => x.ContentType).HasMaxLength(128);
        builder.Property(x => x.ContentHash).HasMaxLength(128);
        builder.Property(x => x.ValidationReportJson).HasColumnType("jsonb");
        builder.Property(x => x.QualityReportJson).HasColumnType("jsonb");
        builder.Property(x => x.PhotoBytes).HasColumnType("bytea");
        builder.Property(x => x.CreatedUtc).HasColumnType("timestamp with time zone");
        builder.Property(x => x.CompletedUtc).HasColumnType("timestamp with time zone");
        builder.Property(x => x.NextAttemptUtc).HasColumnType("timestamp with time zone");
        builder.HasIndex(x => new { x.BatchId, x.StudentId }).IsUnique();
        builder.HasIndex(x => x.ContentHash);
    }
}
