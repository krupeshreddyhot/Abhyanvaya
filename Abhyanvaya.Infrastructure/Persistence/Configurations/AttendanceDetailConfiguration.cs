using Abhyanvaya.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Abhyanvaya.Infrastructure.Persistence.Configurations;

public sealed class AttendanceDetailConfiguration : IEntityTypeConfiguration<AttendanceDetail>
{
    public void Configure(EntityTypeBuilder<AttendanceDetail> builder)
    {
        builder.ToTable("AttendanceDetail");

        builder.HasOne(d => d.Attendance)
            .WithOne(a => a.Detail)
            .HasForeignKey<AttendanceDetail>(d => d.AttendanceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(d => d.AttendanceRecognition)
            .WithMany()
            .HasForeignKey(d => d.AttendanceRecognitionId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(d => d.RecognitionSnapshotJson)
            .HasColumnType("jsonb");

        builder.HasIndex(d => d.AttendanceId)
            .IsUnique()
            .HasDatabaseName("IX_AttendanceDetail_AttendanceId");

        builder.HasIndex(d => d.AttendanceRecognitionId)
            .IsUnique()
            .HasDatabaseName("IX_AttendanceDetail_AttendanceRecognitionId")
            .HasFilter("\"AttendanceRecognitionId\" IS NOT NULL");

        builder.HasIndex(d => new { d.TenantId, d.AttendanceId })
            .HasDatabaseName("IX_AttendanceDetail_Tenant_Attendance");
    }
}
