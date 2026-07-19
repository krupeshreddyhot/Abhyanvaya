using Abhyanvaya.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Abhyanvaya.Infrastructure.Persistence.Configurations;

public sealed class StudentEnrollmentProgressSnapshotConfiguration
    : IEntityTypeConfiguration<StudentEnrollmentProgressSnapshot>
{
    public void Configure(EntityTypeBuilder<StudentEnrollmentProgressSnapshot> builder)
    {
        builder.ToTable("StudentEnrollmentProgressSnapshot");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.CapturedUtc)
            .HasColumnType("timestamp with time zone");

        builder.Property(x => x.SnapshotJson)
            .HasColumnType("jsonb")
            .IsRequired();

        builder.HasOne(x => x.Batch)
            .WithMany()
            .HasForeignKey(x => x.BatchId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.BatchId, x.CapturedUtc })
            .HasDatabaseName("IX_StudentEnrollmentProgressSnapshot_Batch_CapturedUtc");
    }
}
