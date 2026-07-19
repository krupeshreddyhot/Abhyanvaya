using Abhyanvaya.Domain.Entities;
using Abhyanvaya.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Abhyanvaya.Infrastructure.Persistence.Configurations;

public sealed class FaceEnrollmentBatchConfiguration : IEntityTypeConfiguration<FaceEnrollmentBatch>
{
    public void Configure(EntityTypeBuilder<FaceEnrollmentBatch> builder)
    {
        builder.ToTable("FaceEnrollmentBatch");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.State).HasConversion<int>();
        builder.Property(x => x.ManifestJson).HasColumnType("jsonb");
        builder.Property(x => x.CreatedUtc).HasColumnType("timestamp with time zone");
        builder.Property(x => x.CompletedUtc).HasColumnType("timestamp with time zone");
        builder.HasMany(x => x.Jobs).WithOne().HasForeignKey(x => x.BatchId);
    }
}

public sealed class FaceEnrollmentJobConfiguration : IEntityTypeConfiguration<FaceEnrollmentJob>
{
    public void Configure(EntityTypeBuilder<FaceEnrollmentJob> builder)
    {
        builder.ToTable("FaceEnrollmentJob");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.StudentNumber).HasMaxLength(64).IsRequired();
        builder.Property(x => x.State).HasConversion<int>().HasDefaultValue(EnrollmentState.Queued);
        builder.Property(x => x.ArtifactJson).HasColumnType("jsonb");
        builder.Property(x => x.CreatedUtc).HasColumnType("timestamp with time zone");
        builder.Property(x => x.StartedUtc).HasColumnType("timestamp with time zone");
        builder.Property(x => x.CompletedUtc).HasColumnType("timestamp with time zone");
        builder.Property(x => x.LastStateChangeUtc).HasColumnType("timestamp with time zone");
        builder.HasIndex(x => new { x.BatchId, x.StudentId }).IsUnique();
    }
}
