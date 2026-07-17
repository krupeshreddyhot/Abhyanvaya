using Abhyanvaya.Domain.Entities;
using Abhyanvaya.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Abhyanvaya.Infrastructure.Persistence.Configurations;

public sealed class StudentEnrollmentBatchConfiguration : IEntityTypeConfiguration<StudentEnrollmentBatch>
{
    public void Configure(EntityTypeBuilder<StudentEnrollmentBatch> builder)
    {
        builder.ToTable("StudentEnrollmentBatch");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .ValueGeneratedNever();

        builder.Property(x => x.Status)
            .HasConversion<int>()
            .HasDefaultValue(BatchStatus.Created);

        builder.Property(x => x.TotalStudents).HasDefaultValue(0);
        builder.Property(x => x.PendingCount).HasDefaultValue(0);
        builder.Property(x => x.DownloadingCount).HasDefaultValue(0);
        builder.Property(x => x.ValidatingCount).HasDefaultValue(0);
        builder.Property(x => x.EmbeddingCount).HasDefaultValue(0);
        builder.Property(x => x.CompletedCount).HasDefaultValue(0);
        builder.Property(x => x.FailedCount).HasDefaultValue(0);
        builder.Property(x => x.RetryRequiredCount).HasDefaultValue(0);
        builder.Property(x => x.CancelledCount).HasDefaultValue(0);

        builder.Property(x => x.CancellationRequestedUtc)
            .HasColumnType("timestamp with time zone");

        builder.Property(x => x.CreatedUtc)
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("timezone('utc', now())");

        builder.Property(x => x.StartedUtc)
            .HasColumnType("timestamp with time zone");

        builder.Property(x => x.CompletedUtc)
            .HasColumnType("timestamp with time zone");

        builder.Property(x => x.RowVersion)
            .IsConcurrencyToken()
            .HasColumnType("bytea")
            .IsRequired();

        builder.Property(x => x.PipelineVersion)
            .HasDefaultValue(1);

        builder.Property(x => x.ConfigurationSnapshotJson)
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(x => x.CorrelationId)
            .IsRequired();

        builder.Property(x => x.PhotoProviderName)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(x => x.Priority)
            .HasDefaultValue(0);

        builder.HasOne(x => x.College)
            .WithMany()
            .HasForeignKey(x => x.CollegeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.University)
            .WithMany()
            .HasForeignKey(x => x.UniversityId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.Items)
            .WithOne(x => x.Batch)
            .HasForeignKey(x => x.BatchId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.TenantId, x.Status })
            .HasDatabaseName("IX_StudentEnrollmentBatch_Tenant_Status");

        builder.HasIndex(x => new { x.UniversityId, x.CollegeId, x.AcademicYear })
            .HasDatabaseName("IX_StudentEnrollmentBatch_University_College_Year");
    }
}
