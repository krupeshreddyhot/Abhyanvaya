using Abhyanvaya.Domain.Entities;
using Abhyanvaya.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Abhyanvaya.Infrastructure.Persistence.Configurations;

public sealed class StudentEnrollmentItemConfiguration : IEntityTypeConfiguration<StudentEnrollmentItem>
{
    public void Configure(EntityTypeBuilder<StudentEnrollmentItem> builder)
    {
        builder.ToTable("StudentEnrollmentItem");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .ValueGeneratedNever();

        builder.Property(x => x.Status)
            .HasConversion<int>()
            .HasDefaultValue(EnrollmentStatus.Pending);

        builder.Property(x => x.FailureCategory)
            .HasConversion<int?>();

        builder.Property(x => x.SourceUrl)
            .HasMaxLength(1000)
            .HasColumnType("character varying(1000)")
            .IsRequired();

        builder.Property(x => x.PhotoKey)
            .HasMaxLength(500)
            .HasColumnType("character varying(500)");

        builder.Property(x => x.ContentType)
            .HasMaxLength(100)
            .HasColumnType("character varying(100)");

        builder.Property(x => x.Checksum)
            .HasMaxLength(64)
            .HasColumnType("character varying(64)");

        builder.Property(x => x.EmbeddingVersion)
            .HasMaxLength(64)
            .HasColumnType("character varying(64)");

        builder.Property(x => x.LastError)
            .HasMaxLength(1000)
            .HasColumnType("character varying(1000)");

        builder.Property(x => x.RetryCount)
            .HasDefaultValue(0);

        builder.Property(x => x.LastAttemptUtc)
            .HasColumnType("timestamp with time zone");

        builder.Property(x => x.NextAttemptUtc)
            .HasColumnType("timestamp with time zone");

        builder.Property(x => x.CreatedUtc)
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("timezone('utc', now())");

        builder.Property(x => x.DownloadStartedUtc).HasColumnType("timestamp with time zone");
        builder.Property(x => x.DownloadedUtc).HasColumnType("timestamp with time zone");
        builder.Property(x => x.ValidationStartedUtc).HasColumnType("timestamp with time zone");
        builder.Property(x => x.ValidatedUtc).HasColumnType("timestamp with time zone");
        builder.Property(x => x.EmbeddingStartedUtc).HasColumnType("timestamp with time zone");
        builder.Property(x => x.CompletedUtc).HasColumnType("timestamp with time zone");

        builder.Property(x => x.RowVersion)
            .IsConcurrencyToken()
            .HasColumnType("bytea")
            .IsRequired();

        builder.HasOne(x => x.Batch)
            .WithMany(x => x.Items)
            .HasForeignKey(x => x.BatchId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Student)
            .WithMany()
            .HasForeignKey(x => x.StudentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.StudentFaceEmbedding)
            .WithMany()
            .HasForeignKey(x => x.StudentFaceEmbeddingId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(x => new { x.BatchId, x.StudentId })
            .IsUnique()
            .HasDatabaseName("UX_StudentEnrollmentItem_Batch_Student");

        builder.HasIndex(x => new { x.BatchId, x.Status })
            .HasDatabaseName("IX_StudentEnrollmentItem_Batch_Status");

        builder.HasIndex(x => new { x.TenantId, x.StudentId })
            .HasDatabaseName("IX_StudentEnrollmentItem_Tenant_Student");

        builder.HasIndex(x => new { x.Status, x.LastAttemptUtc })
            .HasDatabaseName("IX_StudentEnrollmentItem_Status_LastAttempt");

        builder.HasIndex(x => new { x.Status, x.NextAttemptUtc })
            .HasDatabaseName("IX_StudentEnrollmentItem_Status_NextAttempt");
    }
}
